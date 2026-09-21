using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Core.Rendering;

public static class Render
{
    private const int OctaveNoteCount = 12;
    private const int ZeroOctaveOffset = 5;

    private static readonly ImmutableArray<int> ChromaticScaleOffsets = [..Enumerable.Range(0, OctaveNoteCount)];

    public static RenderedSong RenderSong(Song song)
    {
        var renderedTracks = new List<RenderedTrack>();

        var commonStateTimelineMap = song.TrackEventStateTimelineMap.CommonStateTimelineMap;
        var trackEventStateTimelineMapDictionary = song.TrackEventStateTimelineMap.TrackTimelineMap;

        var percussionTrackNotes = new List<IEnumerable<TimelineItem<RenderedNote>>>();
        foreach (var (trackNumber, track) in song.TrackDefinitions)
        {
            var trackEventStateTimelineMap = trackEventStateTimelineMapDictionary.GetValueOrDefault(trackNumber);
            if (trackEventStateTimelineMap == null || trackEventStateTimelineMap.EventTimeline.IsEmpty)
                continue;

            var combinedEventStateTimelineMap = trackEventStateTimelineMap
                .MergeStateMap(track.StateMap)
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
        if (!percussionEventTimeline.IsEmpty)
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
        var minVelocity = double.MaxValue;
        var maxVelocity = double.MinValue;
        foreach (var renderedTrack in renderedTracks)
        {
            foreach (var renderedNoteTimelineItem in renderedTrack.NoteTimeline)
            {
                var renderedNoteVelocity = renderedNoteTimelineItem.Value.Velocity;
                if (renderedNoteVelocity < minVelocity)
                    minVelocity = renderedNoteVelocity;
                if (renderedNoteVelocity > maxVelocity)
                    maxVelocity = renderedNoteVelocity;
            }
        }

        var velocityRange = maxVelocity - minVelocity;
        // with no velocity spread there is nothing to scale - use the middle of the target range
        var hasVelocitySpread = velocityRange > 0;

        var result = ImmutableArray.CreateBuilder<RenderedTrack>(renderedTracks.Count);
        foreach (var renderedTrack in renderedTracks)
        {
            var fixedVelocityTimeline = renderedTrack.NoteTimeline
                .MapValues(x => x with { Velocity = hasVelocitySpread ? (x.Velocity - minVelocity) / velocityRange / 2 + 0.5 : 0.75 });
            var fixedVelocityTrack = new RenderedTrack(
                renderedTrack.IsPercussionInstrument,
                renderedTrack.PitchInstrumentCode,
                fixedVelocityTimeline
            );
            result.Add(fixedVelocityTrack);
        }

        return result.ToImmutable();
    }

    private static RenderedTrack RenderPitchInstrumentTrack(
        PitchInstrumentTrack track,
        EventTimeline<StateMap> eventStateTimelineMap
    )
    {
        var absoluteMinOctave = track.MinOctaveOffset + ZeroOctaveOffset;
        var octaveCount = track.MaxOctaveOffset - track.MinOctaveOffset + 1;

        var renderedNotes = eventStateTimelineMap
            .WithDurations()
            .SelectMany(x => RenderPitchNotes(x, absoluteMinOctave, octaveCount))
            .ToImmutableArray();
        var renderedNoteTimeline = EventTimeline.Create(eventStateTimelineMap.Duration, renderedNotes);
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
            var noteDuration = stateMap.GetStateValue(StateKinds.QuarterNoteDurationPower);

            var articulationIndex = articulationOffset.ToIndex(track.ArticulationCodes.Length);
            var articulationCode = track.ArticulationCodes[articulationIndex];
            var renderedNote = new RenderedNote(articulationCode, noteVelocity, noteDuration);
            yield return renderedNote.ToTimelineItem(timelineItem.Position);
        }
    }

    private static IEnumerable<TimelineItem<RenderedNote>> RenderPitchNotes(
        TimelineItem<WithDuration<StateMap>> timelineItemWithDuration,
        int absoluteMinOctave,
        int octaveCount
    )
    {
        var position = timelineItemWithDuration.Position;
        var nextNoteDuration = timelineItemWithDuration.Value.Duration;
        var stateMap = timelineItemWithDuration.Value.Value;

        var scaleOffsets = stateMap.GetStateValue(StateKinds.ScaleOffsets);
        if (scaleOffsets.IsEmpty)
            scaleOffsets = ChromaticScaleOffsets;

        // OutOfChordNoteOffset is an in-scale note that's going to be added to the chord notes
        // the only reason it exists along with the ChordRootNoteOffset is to be able to
        // generate it along with the root note but in a separate manner
        var chordRootNoteIndex = stateMap.GetStateValue(StateKinds.ChordRootNoteOffset)
            .ToIndexOverLength(scaleOffsets.Length);

        // chord offsets are chosen from effective scale offsets
        var chordNoteInScaleIndexes = stateMap.GetStateValue(StateKinds.ChordNoteInScaleOffsets)
            .Select(x => x.ToIndexOverLength(scaleOffsets.Length))
            .Prepend(0)
            .Distinct()
            .Order()
            .ToImmutableArray();

        // next we decide if we're going to choose a note from the chord
        var chordNoteOffset = stateMap.GetStateValue(StateKinds.ChordNoteOffset);
        var filteredChordNoteInScaleIndexes = chordNoteInScaleIndexes;
        if (!chordNoteOffset.IsEmpty && !chordNoteInScaleIndexes.IsEmpty)
        {
            var (selectedOctave, selectedIndex) = chordNoteOffset
                .ToIndexOverLength(chordNoteInScaleIndexes.Length)
                .ToPeriodRemainder(chordNoteInScaleIndexes.Length);
            var chordNoteInScaleIndex = chordNoteInScaleIndexes[selectedIndex];
            filteredChordNoteInScaleIndexes = [chordNoteInScaleIndex + selectedOctave * scaleOffsets.Length];
        }

        var noteOctaveOffset = stateMap.GetStateValue(StateKinds.OctaveOffset);
        var noteKeyOffset = stateMap.GetStateValue(StateKinds.KeyOffset);
        var noteVelocity = stateMap.GetStateValue(StateKinds.Velocity);
        var quarterNoteDuration = stateMap.GetStateValue(StateKinds.QuarterNoteDurationPower)
            .BounceInBounds(-2, 2)
            .Pow2();
        var nextNoteDurationFactor = stateMap.GetStateValue(StateKinds.NextNoteDurationFactor)
            .BounceInBounds(0, 1);
        var duration = nextNoteDuration.WeightedAverage(nextNoteDurationFactor, quarterNoteDuration);

        foreach (var chordNoteInScaleIndex in filteredChordNoteInScaleIndexes)
        {
            var (chordNoteOctaveOffset, scaleNoteIndex) = (chordRootNoteIndex + chordNoteInScaleIndex)
                .ToPeriodRemainder(scaleOffsets.Length);
            var scaleKeyOffset = scaleOffsets[scaleNoteIndex];
            var octaveOffset = noteOctaveOffset + chordNoteOctaveOffset;
            var keyOffset = noteKeyOffset + scaleKeyOffset;
            var unfixedOffset = keyOffset + octaveOffset * OctaveNoteCount;
            var offset = FixNoteOffset(absoluteMinOctave, octaveCount, unfixedOffset);

            var renderedNote = new RenderedNote(offset, noteVelocity, duration);
            yield return renderedNote.ToTimelineItem(position);
        }
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

        return (int)Math.Floor(offset * length);
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
