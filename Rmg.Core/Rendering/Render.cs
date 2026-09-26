using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Core.Rendering;

public static class Render
{
    // the velocities of most notes of a song, between these percentiles, spread over the typical velocities, and the
    // few quieter and louder ones over the ranges either side, so a handful of extreme notes cannot squeeze the rest
    // into the middle
    private static readonly (double From, double To) TypicalVelocityPercentiles = (0.05, 0.95);
    private static readonly (double From, double To) QuietVelocities = (0.2, 0.3);
    private static readonly (double From, double To) TypicalVelocities = (0.3, 0.9);
    private static readonly (double From, double To) LoudVelocities = (0.9, 1);

    /// <summary>
    ///     The song as MIDI plays it: its notes, as its generation decided them, or decided here for a song that comes
    ///     without them; a chord's pitches as notes together, the drums on the percussion channel, and the velocities
    ///     spread over the MIDI range.
    /// </summary>
    public static RenderedSong RenderSong(Song song)
    {
        var notes = song.Notes ?? Realizer.Realize(song.TrackDefinitions, song.TrackEventStateTimelineMap);
        var renderedTracks = new List<RenderedTrack>();
        var percussionTrackNotes = new List<IEnumerable<TimelineItem<RenderedNote>>>();
        foreach (var (trackNumber, trackNotes) in notes)
        {
            var renderedNotes = trackNotes.SelectMany(note =>
                note.Value.Pitches.Select(pitch => new RenderedNote(pitch, note.Value.Velocity, note.Value.Duration).ToTimelineItem(note.Position))
            );
            if (song.TrackDefinitions[trackNumber] is PitchInstrumentTrack pitchInstrumentTrack)
                renderedTracks.Add(
                    new RenderedTrack(false, pitchInstrumentTrack.InstrumentCode, EventTimeline.Create(trackNotes.Duration, renderedNotes))
                );
            else
                percussionTrackNotes.Add(renderedNotes);
        }

        var percussionEventTimeline = EventTimeline.Create(song.Duration, percussionTrackNotes.SelectMany(x => x));
        if (percussionEventTimeline.Count > 0)
        {
            var percussionTrack = new RenderedTrack(true, 0, percussionEventTimeline);
            renderedTracks.Add(percussionTrack);
        }

        var fixedVolumeTracks = FixVolume(renderedTracks);

        var tempoTimeline = song.TrackEventStateTimelineMap.CommonStateTimelineMap.OfScope(StateScope.Render).GetStateTimeline(StateKinds.Tempo);

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
}
