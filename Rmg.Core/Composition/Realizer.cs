using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     Decides the song's notes from its state, the last stage of its generation: every note's chord, pitches and
///     length, in the song's order: the notes the state decides, in the register that follows from the notes before, as
///     the chords are led, the bass line takes the octave nearest its note before and leads into the next chord, and
///     the melody's bars, placed where they were made, start in the octave nearest the note before; and every drum hit's
///     sound. It sees the whole song, because a
///     track's line goes on across its sections, and a section that comes back plays on from where the song is.
/// </summary>
internal static class Realizer
{
    private const int OctaveNoteCount = 12;
    private const int ZeroOctaveOffset = 5;

    // drum sounds are mostly one-shots that play out whatever the note length, so every drum note is a sixteenth
    private const double PercussionNoteDuration = 0.25;

    // a note is held towards the next note of its track, but a gap longer than a bar is silence, such as a bar in
    // which the track does not play, and the note is not held through it
    private const double MaxNextNoteDuration = 4;

    private static readonly ImmutableArray<int> ChromaticScaleOffsets = [..Enumerable.Range(0, OctaveNoteCount)];

    /// <summary>The notes of every track that plays, by its number, from the song's state.</summary>
    public static ImmutableSortedDictionary<int, EventTimeline<RealizedNote>> Realize(
        ImmutableSortedDictionary<int, IInstrumentTrack> trackDefinitions,
        TrackEventStateTimelineMap<StateMap> song
    )
    {
        // a note reads only the state Render would; a track's definition also holds what its generation used
        var commonStateTimelineMap = song.CommonStateTimelineMap.OfScope(StateScope.Render);
        var tracks = ImmutableSortedDictionary.CreateBuilder<int, EventTimeline<RealizedNote>>();
        foreach (var (trackNumber, track) in trackDefinitions)
        {
            var trackEventStateTimelineMap = song.TrackTimelineMap.GetValueOrDefault(trackNumber);
            if (trackEventStateTimelineMap == null || trackEventStateTimelineMap.EventTimeline.Count == 0)
                continue;

            var notes = GetNoteStates(trackEventStateTimelineMap, track, commonStateTimelineMap);
            tracks[trackNumber] = track switch
            {
                PitchInstrumentTrack pitchInstrumentTrack => RealizePitchTrack(pitchInstrumentTrack, notes),
                PercussionInstrumentTrack percussionInstrumentTrack => RealizePercussionTrack(percussionInstrumentTrack, notes),
                _ => throw new ArgumentException($"Track {trackNumber} is of no kind that plays.", nameof(trackDefinitions))
            };
        }

        return tracks.ToImmutable();
    }

    /// <summary>Every note's state, as a note reads it: its own, over its track's, its definition's and the common state.</summary>
    internal static EventTimeline<StateMap> GetNoteStates(
        EventStateTimelineMap<StateMap> track,
        IInstrumentTrack definition,
        StateTimelineMap commonStateTimelineMap
    )
    {
        return track
            .MergeStateMap(definition.StateMap.OfScope(StateScope.Render))
            .MergeStateTimelineMap(commonStateTimelineMap)
            .ToMappedEventTimeline((s1, s2) => StateMap.Aggregate([s1, s2]));
    }

    /// <summary>The lowest and the highest note of a pitched track's range.</summary>
    internal static (int Low, int High) GetRange(PitchInstrumentTrack track)
    {
        var absoluteMinOctave = track.MinOctaveOffset + ZeroOctaveOffset;
        var octaveCount = track.MaxOctaveOffset - track.MinOctaveOffset + 1;
        return (absoluteMinOctave * OctaveNoteCount, (absoluteMinOctave + octaveCount) * OctaveNoteCount - 1);
    }

    /// <summary>
    ///     The chord a note plays over, and its notes, in scale steps above its root, and as pitch classes; a note
    ///     without a chord plays its root alone.
    /// </summary>
    internal static (ChordContext Chord, ImmutableArray<int> Steps, IReadOnlySet<int> Classes) GetChordNotes(StateMap stateMap)
    {
        var (scaleOffsets, chordRootNoteIndex, chord) = GetChord(stateMap);
        var chordPitchOffsets = stateMap.GetStateValue(StateKinds.ChordNotePitchOffsets);
        var chordSteps = SnapChordToScale(
            scaleOffsets,
            chordRootNoteIndex,
            chordPitchOffsets.IsEmpty ? [0] : chordPitchOffsets.Select(x => x * OctaveNoteCount)
        );
        return (chord, chordSteps, chordSteps.Select(x => chord.GetPitch(x).Mod(OctaveNoteCount)).ToHashSet());
    }

    /// <summary>A pitched track's notes, each placed from the one before, as its chords or bass line lead.</summary>
    private static EventTimeline<RealizedNote> RealizePitchTrack(PitchInstrumentTrack track, EventTimeline<StateMap> eventStateTimelineMap)
    {
        var absoluteMinOctave = track.MinOctaveOffset + ZeroOctaveOffset;
        var octaveCount = track.MaxOctaveOffset - track.MinOctaveOffset + 1;

        // the chords are placed in order, each following from the one before
        var voiceLeader = new VoiceLeader(
            absoluteMinOctave * OctaveNoteCount,
            (absoluteMinOctave + octaveCount) * OctaveNoteCount - 1,
            notes => FitChordIntoRange(absoluteMinOctave, octaveCount, notes)
        );
        // and a bass line's notes, each following from the one before and leading into the next chord
        var bassLine = new BassLine(
            absoluteMinOctave * OctaveNoteCount,
            (absoluteMinOctave + octaveCount) * OctaveNoteCount - 1,
            note => FixNoteOffset(absoluteMinOctave, octaveCount, note)
        );
        var items = eventStateTimelineMap.WithDurations().ToArray();
        var notes = new TimelineItem<RealizedNote>[items.Length];
        for (var i = 0; i < items.Length; i++)
        {
            TimelineItem<WithDuration<StateMap>>? next = i + 1 < items.Length ? items[i + 1] : null;
            notes[i] = RealizeNote(items[i], next, voiceLeader, bassLine, track.Role);
        }

        return EventTimeline.Create(eventStateTimelineMap.Duration, notes);
    }

    /// <summary>A drum's hits, each of the sound its note names or its articulation's walk picks.</summary>
    private static EventTimeline<RealizedNote> RealizePercussionTrack(PercussionInstrumentTrack track, EventTimeline<StateMap> noteTimeline)
    {
        if (track.ArticulationCodes.IsEmpty)
            throw new ArgumentException("Articulation codes cannot be empty", nameof(track));

        return EventTimeline.Create(noteTimeline.Duration, noteTimeline.Select(RealizeHit));

        TimelineItem<RealizedNote> RealizeHit(TimelineItem<StateMap> timelineItem)
        {
            var stateMap = timelineItem.Value;
            var articulationOffset = stateMap.GetStateValue(StateKinds.ArticulationOffset);
            var noteVelocity = stateMap.GetStateValue(StateKinds.Velocity);

            var fixedIndex = stateMap.GetStateValue(StateKinds.ArticulationIndex);
            // a note may name its sound, as a fill does; a drum that strikes plays its stroke; otherwise the walk of the
            // articulation picks it
            var stroke = stateMap.GetStateValue(StateKinds.DrumStroke);
            var articulationIndex = fixedIndex > 0 ? Math.Min(fixedIndex, track.ArticulationCodes.Length) - 1
                : stroke.IsSet ? stroke.Value
                : articulationOffset.ToIndex(track.ArticulationCodes.Length);
            // and plays as loud as its sound is over the drum
            var sound = track.Sounds[articulationIndex];
            var velocity = noteVelocity + VelocityLayers.SoundLevel * sound.Loudness;
            return new RealizedNote([sound.Code], velocity, PercussionNoteDuration, stateMap).ToTimelineItem(timelineItem.Position);
        }
    }

    /// <summary>A pitched note: its chord, its length, and its pitches, as the track's line places them.</summary>
    private static TimelineItem<RealizedNote> RealizeNote(
        TimelineItem<WithDuration<StateMap>> timelineItemWithDuration,
        TimelineItem<WithDuration<StateMap>>? nextItem,
        VoiceLeader voiceLeader,
        BassLine bassLine,
        TrackRole role
    )
    {
        var position = timelineItemWithDuration.Position;
        var nextNoteDuration = Math.Min(timelineItemWithDuration.Value.Duration, MaxNextNoteDuration);
        var stateMap = timelineItemWithDuration.Value.Value;

        var (scaleOffsets, chordRootNoteIndex, chord) = GetChord(stateMap);

        // the chord notes, in scale steps above the root; a note without a chord plays its root alone
        var chordPitchOffsets = stateMap.GetStateValue(StateKinds.ChordNotePitchOffsets);
        var chordSteps = SnapChordToScale(
            scaleOffsets,
            chordRootNoteIndex,
            chordPitchOffsets.IsEmpty ? [0] : chordPitchOffsets.Select(x => x * OctaveNoteCount)
        );

        var noteVelocity = stateMap.GetStateValue(StateKinds.Velocity);
        var quarterNoteDuration = stateMap.GetStateValue(StateKinds.QuarterNoteDurationPower)
            .BounceInBounds(-2, 2)
            .Pow2();
        var nextNoteDurationFactor = stateMap.GetStateValue(StateKinds.NextNoteDurationFactor)
            .BounceInBounds(0, 1);
        var duration = nextNoteDuration.WeightedAverage(nextNoteDurationFactor, quarterNoteDuration);
        // a melody sings one note at a time, so a note ends by the next, unless it is held, as a phrase's last note is
        var heldDuration = stateMap.GetStateValue(StateKinds.HeldDuration);
        if (heldDuration > 0)
            duration = heldDuration;
        else if (role == TrackRole.Melody)
            duration = Math.Min(duration, nextNoteDuration);

        int ToNote(int stepAboveRoot) => chord.GetPitch(stepAboveRoot);

        // how a track plays its chord is its role's: the melody the note placed where it was made, as its scale step
        // above the chord's root; the bass a line of the chord's notes; the chords the whole chord, led from the one before
        ImmutableArray<int> notes = role switch
        {
            TrackRole.Melody => [ToNote(stateMap.GetStateValue(StateKinds.ScaleStep))],
            TrackRole.Bass => [PlaceBassNote()],
            TrackRole.Chords => voiceLeader.Place(
                [..chordSteps.Select(ToNote)],
                ToNote(0),
                stateMap.GetStateValue(StateKinds.ChordVoicingFixed) > 0,
                stateMap.GetStateValue(StateKinds.VoiceLeading),
                stateMap.GetStateValue(StateKinds.ChordVoicingReset)
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "A pitched track plays the chords, the melody or the bass.")
        };

        return new RealizedNote(notes, noteVelocity, duration, stateMap).ToTimelineItem(position);

        // the bass's note: the chord's note its chord note offset picks, going round the chord's scale notes from the
        // root up and an octave up or down for every lap around it, the root for none, as its line places it
        int PlaceBassNote()
        {
            var chordDegrees = chordSteps
                .Select(x => x.Mod(scaleOffsets.Length))
                .Distinct()
                .Order()
                .ToImmutableArray();
            var (selectedOctave, selectedIndex) = stateMap.GetStateValue(StateKinds.ChordNoteOffset)
                .ToIndexOverLength(chordDegrees.Length)
                .ToPeriodRemainder(chordDegrees.Length);
            return bassLine.Place(
                ToNote(chordDegrees[selectedIndex] + selectedOctave * scaleOffsets.Length),
                chord,
                position,
                nextItem is { } next ? GetChord(next.Value.Value).Chord : null,
                nextItem?.Position,
                (ChordArrival)stateMap.GetStateValue(StateKinds.ChordArrival),
                (ChordApproach)stateMap.GetStateValue(StateKinds.ChordApproach)
            );
        }
    }

    /// <summary>
    ///     The chord a note is played over: the scale, with the steps the note's bar raises, the chord root's step in
    ///     it, and the pitch of every step counted from the root, in the key and the octave.
    /// </summary>
    internal static (ImmutableArray<int> ScaleOffsets, int RootIndex, ChordContext Chord) GetChord(StateMap stateMap)
    {
        var scaleOffsets = stateMap.GetStateValue(StateKinds.ScaleOffsets);
        if (scaleOffsets.IsEmpty)
            scaleOffsets = ChromaticScaleOffsets;
        scaleOffsets = RaiseScaleSteps(scaleOffsets, stateMap.GetStateValue(StateKinds.RaisedScaleSteps));

        // the chord root, in scale steps
        var rootIndex = stateMap.GetStateValue(StateKinds.ChordRootNoteOffset).ToIndexOverLength(scaleOffsets.Length);
        var basePitch = stateMap.GetStateValue(StateKinds.KeyOffset) + stateMap.GetStateValue(StateKinds.OctaveOffset) * OctaveNoteCount;
        return (scaleOffsets, rootIndex, new ChordContext(step => basePitch + GetScalePitch(scaleOffsets, rootIndex + step)));
    }

    /// <summary>
    ///     The scale with every listed step raised a semitone, as many times as it is listed. The scale must stay in
    ///     order within the octave: a step cannot be raised onto the next one.
    /// </summary>
    internal static ImmutableArray<int> RaiseScaleSteps(ImmutableArray<int> scaleOffsets, ImmutableArray<int> raisedSteps)
    {
        if (raisedSteps.IsEmpty)
            return scaleOffsets;

        var offsets = scaleOffsets.ToArray();
        foreach (var step in raisedSteps)
            offsets[step.Mod(offsets.Length)]++;

        for (var i = 0; i < offsets.Length; i++)
            if (offsets[i] >= OctaveNoteCount || i > 0 && offsets[i] <= offsets[i - 1])
                throw new ArgumentException(
                    $"Raising steps {string.Join(", ", raisedSteps)} of the scale {string.Join(", ", scaleOffsets)} puts it out of order.",
                    nameof(raisedSteps)
                );

        return [..offsets];
    }

    /// <summary>
    ///     The chord's notes, in scale steps above its root, ascending. Every target, in semitones above the root's
    ///     pitch, goes to the scale note nearest to it, the lowest target first and the lower note on a tie, so the
    ///     same chord takes the scale's own shape in any scale. A target whose nearest note is already taken goes to the
    ///     free one of that note's two neighbours that is nearer, and a target with neither left is dropped.
    /// </summary>
    internal static ImmutableArray<int> SnapChordToScale(
        ImmutableArray<int> scaleOffsets,
        int rootIndex,
        IEnumerable<double> targets
    )
    {
        if (scaleOffsets.IsEmpty)
            throw new ArgumentException("Scale offsets cannot be empty.", nameof(scaleOffsets));

        var rootPitch = GetScalePitch(scaleOffsets, rootIndex);
        double Distance(int step, double target) =>
            Math.Abs(GetScalePitch(scaleOffsets, rootIndex + step) - rootPitch - target);

        var steps = new SortedSet<int>();
        foreach (var target in targets.Order())
        {
            var nearest = FindNearestStep(scaleOffsets, rootIndex, rootPitch, target);
            if (steps.Add(nearest))
                continue;

            var (closer, further) = Distance(nearest - 1, target) <= Distance(nearest + 1, target)
                ? (nearest - 1, nearest + 1)
                : (nearest + 1, nearest - 1);
            if (!steps.Add(closer))
                steps.Add(further);
        }

        return [..steps];
    }

    private static int FindNearestStep(ImmutableArray<int> scaleOffsets, int rootIndex, int rootPitch, double target)
    {
        // the scale's pitches rise with its steps, so the nearest step is within a scale's length of the step an even
        // division of the octave would give
        var length = scaleOffsets.Length;
        var guess = (int)Math.Floor(target / OctaveNoteCount * length);
        var nearest = guess - length;
        var nearestDistance = double.MaxValue;
        for (var step = guess - length; step <= guess + length; step++)
        {
            var distance = Math.Abs(GetScalePitch(scaleOffsets, rootIndex + step) - rootPitch - target);
            if (distance < nearestDistance)
            {
                nearest = step;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    /// <summary>The pitch of a scale step, in semitones above the scale's first note in octave 0.</summary>
    private static int GetScalePitch(ImmutableArray<int> scaleOffsets, int step)
    {
        var (octave, degree) = step.ToPeriodRemainder(scaleOffsets.Length);
        return scaleOffsets[degree] + octave * OctaveNoteCount;
    }

    /// <summary>
    ///     The chord moved into the track's range as a whole, by the octaves that bring its lowest note where
    ///     <see cref="FixNoteOffset" /> would, so it keeps its voicing. A note still above the range comes down by
    ///     octaves, and one that lands on another note of the chord is dropped.
    /// </summary>
    internal static ImmutableArray<int> FitChordIntoRange(int absoluteMinOctave, int octaveCount, ImmutableArray<int> notes)
    {
        if (notes.IsEmpty)
            return [];

        var lowestNote = notes.Min();
        var shift = FixNoteOffset(absoluteMinOctave, octaveCount, lowestNote) - lowestNote;
        var maxNote = (absoluteMinOctave + octaveCount) * OctaveNoteCount - 1;

        var result = new SortedSet<int>();
        foreach (var note in notes.Order())
        {
            var fittedNote = note + shift;
            while (fittedNote > maxNote)
                fittedNote -= OctaveNoteCount;
            result.Add(fittedNote);
        }

        return [..result];
    }

    private static int ToIndex(this double offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        return length > 1
            ? offset
                .ToIndexOverLength(length)
                .Mod(length)
            : 0;
    }

    private static int ToIndexOverLength(this double offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        // to the nearest step, halves away from zero, so that small offsets either way stay put
        return (int)Math.Round(offset * length, MidpointRounding.AwayFromZero);
    }

    private static int ToIndex(this ImmutableArray<double> offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        return length > 1
            ? offset
                .Sum(x => x.ToIndex(length))
                .Mod(length)
            : 0;
    }

    // each value is rounded before summing, so every layer shifts a pattern by a whole step - see README "How it works"
    private static int ToIndexOverLength(this ImmutableArray<double> offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        return offset.Sum(x => x.ToIndexOverLength(length));
    }

    private static (int periodIndex, int remainder) ToPeriodRemainder(this int offset, int periodLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(periodLength);

        var remainder = offset.Mod(periodLength);
        var periodIndex = (offset - remainder) / periodLength;
        return (periodIndex, remainder);
    }

    internal static int FixNoteOffset(int absoluteMinOctave, int octaveCount, int noteOffset)
    {
        if (absoluteMinOctave is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(absoluteMinOctave), "Min octave must be between 0 and 10");

        var absoluteMaxOctave = absoluteMinOctave + octaveCount - 1;
        if (octaveCount < 1 || absoluteMaxOctave > 10)
            throw new ArgumentOutOfRangeException(
                nameof(octaveCount),
                "Octave count must be between 1 and 11-absoluteMinOctave."
            );

        var octaveNote = noteOffset.Mod(OctaveNoteCount);
        if (octaveCount == 1)
            return octaveNote + absoluteMinOctave * OctaveNoteCount;

        var noteOctave = (noteOffset - octaveNote) / OctaveNoteCount;
        var fixedOctaveOffset = noteOctave.BounceInBounds(absoluteMinOctave, absoluteMaxOctave);
        return fixedOctaveOffset * OctaveNoteCount + octaveNote;
    }
}
