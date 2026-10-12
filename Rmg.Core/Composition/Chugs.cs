using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     How a chugging riff plays its chords, by how fast it goes in each appearance: a fast one, as a single string's
///     16ths, its pedal single notes and its accents, its beats, power chords where its section plays them; a slow one, a
///     big chug across the strings, every note a power chord; its notes off its accents palm-muted either way
///     (<see cref="Articulations" />), and its weak notes on its pedal (<see cref="Line.PedalShare" />). A stage of the
///     song before its notes are made (<see cref="SongStages" />), a genre's, whose songs alone chug.
/// </summary>
internal static class Chugs
{
    /// <summary>How many notes a beat a riff plays, from which its chugs are a single string's, fast, rather than big.</summary>
    public const double FastNotes = 2;

    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var riff = song.Track(TrackRole.Riff).Key;
        if (!draft.Timeline.TrackTimelineMap.TryGetValue(riff, out var track) || !song.Sections.Any(x => x.Chugs))
            return draft;

        // the sections, and an intro's window before the first, as the first
        var spans = song.Form.Map.PlayedSections.Select(span => (span, song.Sections[span == song.Form.Map.Window ? 0 : song.Form.Map.Sections.IndexOf(span)])).Where(x => x.Item2.Chugs).ToArray();
        var notes = track.EventTimeline;
        // how many notes a beat the riff plays in each span, over the bars it plays in, a texture, a late entry or a call
        // and response resting it in the others
        var density = spans.ToDictionary(x => x.span, x =>
        {
            var bars = x.span.Bars.Select(b => (Length: b.End - b.Start, Count: notes.Count(n => n.IsWithin(b.Start, b.End)))).Where(b => b.Count > 0).ToArray();
            return bars.Length == 0 ? 0 : bars.Sum(b => b.Count) / bars.Sum(b => b.Length);
        });
        var chorded = notes.Select(note =>
        {
            var (span, section) = spans.FirstOrDefault(x => note.IsWithin(x.span.Start, x.span.End));
            if (span is null)
                return note;
            var isFast = density[span] >= FastNotes;
            var isAccent = note.Value.GetStateValue(CompositionStateKinds.BeatRank) == 0;
            return !isFast || isAccent && section.RiffPowerChords ? note.Value.With(CompositionStateKinds.PowerChord, 1).ToTimelineItem(note.Position) : note;
        });
        return draft with
        {
            Timeline = draft.Timeline.MapTrackEvents(new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>>
            {
                [riff] = timeline => EventTimeline.Create(timeline.Duration, chorded)
            })
        };
    }
}
