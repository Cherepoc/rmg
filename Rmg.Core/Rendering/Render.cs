using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Core.Rendering;

public static class Render
{
    private const int OctaveNoteCount = 12;
    private const int ZeroOctaveOffset = 5;

    // drum sounds are mostly one-shots that play out whatever the note length, so every drum note is a sixteenth
    private const double PercussionNoteDuration = 0.25;

    // a note is held towards the next note of its track, but a gap longer than a bar is silence, such as a bar in
    // which the track does not play, and the note is not held through it
    private const double MaxNextNoteDuration = 4;

    // the velocities of most notes of a song, between these percentiles, spread over the typical velocities, and the
    // few quieter and louder ones over the ranges either side, so a handful of extreme notes cannot squeeze the rest
    // into the middle
    private static readonly (double From, double To) TypicalVelocityPercentiles = (0.05, 0.95);
    private static readonly (double From, double To) QuietVelocities = (0.2, 0.3);
    private static readonly (double From, double To) TypicalVelocities = (0.3, 0.9);
    private static readonly (double From, double To) LoudVelocities = (0.9, 1);

    private static readonly ImmutableArray<int> ChromaticScaleOffsets = [..Enumerable.Range(0, OctaveNoteCount)];

    public static RenderedSong RenderSong(Song song)
    {
        var renderedTracks = new List<RenderedTrack>();

        // Render reads only its own state; a track's definition also holds what its generation used
        var commonStateTimelineMap = song.TrackEventStateTimelineMap.CommonStateTimelineMap.OfScope(StateScope.Render);
        var trackEventStateTimelineMapDictionary = song.TrackEventStateTimelineMap.TrackTimelineMap;

        var percussionTrackNotes = new List<IEnumerable<TimelineItem<RenderedNote>>>();
        foreach (var (trackNumber, track) in song.TrackDefinitions)
        {
            var trackEventStateTimelineMap = trackEventStateTimelineMapDictionary.GetValueOrDefault(trackNumber);
            if (trackEventStateTimelineMap == null || trackEventStateTimelineMap.EventTimeline.Count == 0)
                continue;

            var combinedEventStateTimelineMap = trackEventStateTimelineMap
                .MergeStateMap(track.StateMap.OfScope(StateScope.Render))
                .MergeStateTimelineMap(commonStateTimelineMap)
                .ToMappedEventTimeline((s1, s2) => StateMap.Aggregate([s1, s2]));

            if (track is PitchInstrumentTrack pitchInstrumentTrack)
            {
                var renderedTrack = RenderPitchInstrumentTrack(pitchInstrumentTrack, combinedEventStateTimelineMap);
                renderedTracks.Add(renderedTrack);
            }
            else if (track is PercussionInstrumentTrack percussionInstrumentTrack)
            {
                var percussionTrackNotesTimeline =
                    RenderPercussionTrackNotes(percussionInstrumentTrack, combinedEventStateTimelineMap);
                percussionTrackNotes.Add(percussionTrackNotesTimeline);
            }
        }

        var percussionEventTimeline = EventTimeline.Create(song.Duration, percussionTrackNotes.SelectMany(x => x));
        if (percussionEventTimeline.Count > 0)
        {
            var percussionTrack = new RenderedTrack(true, 0, percussionEventTimeline);
            renderedTracks.Add(percussionTrack);
        }

        var fixedVolumeTracks = FixVolume(renderedTracks);

        var tempoTimeline = commonStateTimelineMap.GetStateTimeline(StateKinds.Tempo);

        return new RenderedSong(song.Duration, tempoTimeline, fixedVolumeTracks);
    }

    private static ImmutableArray<RenderedTrack> FixVolume(List<RenderedTrack> renderedTracks)
    {
        var velocities = renderedTracks
            .SelectMany(x => x.NoteTimeline)
            .Select(x => x.Value.Velocity)
            .Order()
            .ToArray();
        if (velocities.Length == 0)
            return [..renderedTracks];

        var minVelocity = velocities[0];
        var typicalMinVelocity = GetPercentile(velocities, TypicalVelocityPercentiles.From);
        var typicalMaxVelocity = GetPercentile(velocities, TypicalVelocityPercentiles.To);
        var maxVelocity = velocities[^1];

        double FixVelocity(double velocity)
        {
            if (velocity < typicalMinVelocity)
                return Scale(velocity, minVelocity, typicalMinVelocity, QuietVelocities);
            if (velocity > typicalMaxVelocity)
                return Scale(velocity, typicalMaxVelocity, maxVelocity, LoudVelocities);
            return Scale(velocity, typicalMinVelocity, typicalMaxVelocity, TypicalVelocities);
        }

        return
        [
            ..renderedTracks.Select(x => new RenderedTrack(
                    x.IsPercussionInstrument,
                    x.PitchInstrumentCode,
                    x.NoteTimeline.MapValues(note => note with { Velocity = FixVelocity(note.Velocity) })
                )
            )
        ];
    }

    /// <summary>The value below which <paramref name="percentile" /> of the sorted values lie, between the two nearest.</summary>
    private static double GetPercentile(double[] sortedValues, double percentile)
    {
        var index = percentile * (sortedValues.Length - 1);
        var lowerIndex = (int)Math.Floor(index);
        var upperIndex = Math.Min(lowerIndex + 1, sortedValues.Length - 1);
        return sortedValues[lowerIndex] + (sortedValues[upperIndex] - sortedValues[lowerIndex]) * (index - lowerIndex);
    }

    /// <summary>The value moved from between <paramref name="from" /> and <paramref name="to" /> into the range, in proportion.</summary>
    private static double Scale(double value, double from, double to, (double From, double To) range)
    {
        // with nothing to scale by, the middle of the range
        if (to <= from)
            return (range.From + range.To) / 2;

        return range.From + (value - from) / (to - from) * (range.To - range.From);
    }

    private static RenderedTrack RenderPitchInstrumentTrack(
        PitchInstrumentTrack track,
        EventTimeline<StateMap> eventStateTimelineMap
    )
    {
        var absoluteMinOctave = track.MinOctaveOffset + ZeroOctaveOffset;
        var octaveCount = track.MaxOctaveOffset - track.MinOctaveOffset + 1;

        // the chords are placed in order, each following from the one before
        var voiceLeader = new VoiceLeader(
            absoluteMinOctave * OctaveNoteCount,
            (absoluteMinOctave + octaveCount) * OctaveNoteCount - 1,
            notes => FitChordIntoRange(absoluteMinOctave, octaveCount, notes)
        );
        var renderedNotes = ImmutableArray.CreateBuilder<TimelineItem<RenderedNote>>();
        foreach (var item in eventStateTimelineMap.WithDurations())
            renderedNotes.AddRange(RenderPitchNotes(item, absoluteMinOctave, octaveCount, voiceLeader));
        var renderedNoteTimeline = EventTimeline.Create(eventStateTimelineMap.Duration, renderedNotes.ToImmutable());
        return new RenderedTrack(false, track.InstrumentCode, renderedNoteTimeline);
    }

    private static IEnumerable<TimelineItem<RenderedNote>> RenderPercussionTrackNotes(
        PercussionInstrumentTrack track,
        EventTimeline<StateMap> noteTimeline
    )
    {
        if (track.ArticulationCodes.IsEmpty)
            throw new ArgumentException("Articulation codes cannot be empty", nameof(track));

        foreach (var timelineItem in noteTimeline)
        {
            var stateMap = timelineItem.Value;
            var articulationOffset = stateMap.GetStateValue(StateKinds.ArticulationOffset);
            var noteVelocity = stateMap.GetStateValue(StateKinds.Velocity);

            var articulationIndex = articulationOffset.ToIndex(track.ArticulationCodes.Length);
            var articulationCode = track.ArticulationCodes[articulationIndex];
            var renderedNote = new RenderedNote(articulationCode, noteVelocity, PercussionNoteDuration);
            yield return renderedNote.ToTimelineItem(timelineItem.Position);
        }
    }

    private static IEnumerable<TimelineItem<RenderedNote>> RenderPitchNotes(
        TimelineItem<WithDuration<StateMap>> timelineItemWithDuration,
        int absoluteMinOctave,
        int octaveCount,
        VoiceLeader voiceLeader
    )
    {
        var position = timelineItemWithDuration.Position;
        var nextNoteDuration = Math.Min(timelineItemWithDuration.Value.Duration, MaxNextNoteDuration);
        var stateMap = timelineItemWithDuration.Value.Value;

        var scaleOffsets = stateMap.GetStateValue(StateKinds.ScaleOffsets);
        if (scaleOffsets.IsEmpty)
            scaleOffsets = ChromaticScaleOffsets;
        scaleOffsets = RaiseScaleSteps(scaleOffsets, stateMap.GetStateValue(StateKinds.RaisedScaleSteps));

        // the chord root, in scale steps
        var chordRootNoteIndex = stateMap.GetStateValue(StateKinds.ChordRootNoteOffset)
            .ToIndexOverLength(scaleOffsets.Length);

        // the chord notes, in scale steps above the root; a note without a chord plays its root alone
        var chordPitchOffsets = stateMap.GetStateValue(StateKinds.ChordNotePitchOffsets);
        var chordSteps = SnapChordToScale(
            scaleOffsets,
            chordRootNoteIndex,
            chordPitchOffsets.IsEmpty ? [0] : chordPitchOffsets.Select(x => x * OctaveNoteCount)
        );

        var noteOctaveOffset = stateMap.GetStateValue(StateKinds.OctaveOffset);
        var noteKeyOffset = stateMap.GetStateValue(StateKinds.KeyOffset);
        var noteVelocity = stateMap.GetStateValue(StateKinds.Velocity);
        var quarterNoteDuration = stateMap.GetStateValue(StateKinds.QuarterNoteDurationPower)
            .BounceInBounds(-2, 2)
            .Pow2();
        var nextNoteDurationFactor = stateMap.GetStateValue(StateKinds.NextNoteDurationFactor)
            .BounceInBounds(0, 1);
        var duration = nextNoteDuration.WeightedAverage(nextNoteDurationFactor, quarterNoteDuration);

        int ToNote(int stepAboveRoot) =>
            noteKeyOffset + noteOctaveOffset * OctaveNoteCount + GetScalePitch(scaleOffsets, chordRootNoteIndex + stepAboveRoot);

        // a chord note offset picks one note of the chord, whatever its voicing, going round the chord's scale notes
        // from the root up and an octave up or down for every lap around it; without one the whole chord plays
        var chordNoteOffset = stateMap.GetStateValue(StateKinds.ChordNoteOffset);
        ImmutableArray<int> notes;
        if (!chordNoteOffset.IsEmpty)
        {
            var chordDegrees = chordSteps
                .Select(x => x.Mod(scaleOffsets.Length))
                .Distinct()
                .Order()
                .ToImmutableArray();
            var (selectedOctave, selectedIndex) = chordNoteOffset
                .ToIndexOverLength(chordDegrees.Length)
                .ToPeriodRemainder(chordDegrees.Length);
            var note = ToNote(chordDegrees[selectedIndex] + selectedOctave * scaleOffsets.Length);
            notes = [FixNoteOffset(absoluteMinOctave, octaveCount, note)];
        }
        else
        {
            notes = voiceLeader.Place(
                [..chordSteps.Select(ToNote)],
                ToNote(0),
                stateMap.GetStateValue(StateKinds.ChordVoicingFixed) > 0,
                stateMap.GetStateValue(StateKinds.VoiceLeading),
                stateMap.GetStateValue(StateKinds.ChordVoicingReset)
            );
        }

        foreach (var note in notes)
        {
            var renderedNote = new RenderedNote(note, noteVelocity, duration);
            yield return renderedNote.ToTimelineItem(position);
        }
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
