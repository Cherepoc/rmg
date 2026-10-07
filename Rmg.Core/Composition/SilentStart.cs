using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A song never silent at its start: where its first note comes a bar or more in, its parts resting through their first
///     phrase's start, the earliest of its pitched parts comes in on its first beat, its first note held from there into
///     where it was, a held chord rather than a line where one is near as early, as an ambient piece's pad opens it. A
///     stage of the song after it is put together (<see cref="SongStages" />).
/// </summary>
internal static class SilentStart
{
    /// <summary>The parts that hold a song's first chord, the likeliest first, before a line's.</summary>
    private static readonly TrackRole[] Holders = [TrackRole.Pad, TrackRole.Chords, TrackRole.Rhythm, TrackRole.Bass];

    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var notes = SongStages.NotesOf(draft);
        var bar = song.Form.Map.MeterAt(0).BarDuration;
        var first = notes.Values.Where(x => x.Count > 0).Select(x => x[0].Position).DefaultIfEmpty(0).Min();
        if (first < bar - 1e-9)
            return draft;
        // the pitched parts by their first notes, a holder within a bar of the earliest before a line
        var pitched = notes.Where(x => x.Value.Count > 0 && song.Tracks.Definitions[x.Key] is PitchInstrumentTrack).ToArray();
        if (pitched.Length == 0)
            return draft;
        var earliest = pitched.Min(x => x.Value[0].Position);
        var (track, timeline) = pitched.Where(x => x.Value[0].Position < earliest + bar - 1e-9)
            .OrderBy(x => Array.IndexOf(Holders, song.Tracks.Definitions[x.Key].Role) is var i and >= 0 ? i : Holders.Length).ThenBy(x => x.Value[0].Position).First();
        var opening = timeline[0];
        var held = (opening.Value with { Duration = opening.Position + opening.Value.Duration }).ToTimelineItem(0);
        return draft with { Notes = notes.SetItem(track, EventTimeline.Create(timeline.Duration, timeline.Skip(1).Prepend(held))) };
    }
}
