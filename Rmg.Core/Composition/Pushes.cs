using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>Pushes, the band's changes an eighth early, a stage of the song after it is put together (<see cref="SongStages" />).</summary>
internal static class Pushes
{
    /// <summary>The chance a section pushes its chord changes, by its groove facet: never in the plainest songs; a genre's where it names it.</summary>
    public static ByConvention Chance { get; } = new(0, 0.08, 0.15);

    /// <summary>How early a push plays its change, in beats: an eighth.</summary>
    public const double Early = 0.5;

    /// <summary>
    ///     Pushes: in a pushing section, every chord change on a bar's first beat but the section's first played an eighth
    ///     early by the band's harmony, as an anticipation: a part's notes there moved ahead and held to where they ended, a
    ///     bar at most, what they sound over in the eighth before taken out, where the part played the bar before; the kick
    ///     and the crash pushing with it, the other drums keeping time under it and the lines going on; never in a solo or in stop time. A section's decision, from a
    ///     sequence of its own.
    /// </summary>
    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var pushing = song.SectionIds.Distinct().ToDictionary(id => id, id =>
        {
            var section = song.SectionOf(id);
            var pushes = song.Stream(SongStream.Pushes, id).TestProbability(Chance.At(section.Facets[Facet.Groove], song.Genre.Chance(GenreChoices.Push, "sections")));
            StateTrace.Record(TracePoints.Push, SectionGenerator.SectionTrace, id, 0, StateMap.Default, 0, $"{pushes}", pushes);
            return pushes;
        });
        // but in a solo, and in stop time and out of it, whose hits stay on the bar
        var changes = draft.Timeline.CommonStateTimelineMap.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position).ToArray();
        var stopTime = song.StopTime;
        var pushes = song.Form.Map.Sections.Where((x, i) => pushing[x.SectionId] && !song.Solos.ContainsKey(i))
            .SelectMany(span => changes.Where(x => x > span.Start + 1e-9 && x < span.End - 1e-9 && Math.Abs(Math.IEEERemainder(x - span.Start, span.Meter.BarDuration)) < 1e-6)
                .Select(x => (At: x, span.Meter.BarDuration)))
            .Where(x => !stopTime.Any(p => x.At >= p.Start - 1e-9 && x.At <= p.End + 1e-9))
            .ToArray();
        if (pushes.Length == 0)
            return draft;

        var notes = SongStages.NotesOf(draft);
        var result = notes.ToBuilder();
        // the kick and the crash push with the band where they played the bar before, their notes on a pushed change struck
        // an eighth early as their notes in that bar sound, where none strikes there already
        // where the drums keep time: drums that only mark the form mark it where it moves
        var pushers = song.DrumJob == DrumJob.Groove ? DrumGroups.Kick.Drums.Append(DrumDefinitions.Cymbal).Select(DrumGroups.GetTrackNumber).ToHashSet() : [];
        foreach (var track in pushers.Where(notes.ContainsKey))
        {
            var timeline = notes[track];
            var own = pushes.Where(x => timeline.Any(n => n.Position >= x.At - x.BarDuration - 1e-9 && n.Position < x.At - 1e-9)).Select(x => x.At).ToArray();
            bool On(double position) => own.Any(x => Math.Abs(x - position) < 1e-6);
            result[track] = EventTimeline.Create(timeline.Duration, timeline
                .Where(note => !(On(note.Position) && timeline.Any(n => Math.Abs(n.Position - (note.Position - Early)) < 1e-6)))
                .Select(note => On(note.Position)
                    ? (note.Value with { Pitches = timeline.Last(n => n.Position < note.Position - 1e-6).Value.Pitches }).ToTimelineItem(note.Position - Early)
                    : note)
                .OrderBy(x => x.Position));
        }

        foreach (var (track, timeline) in notes)
        {
            // the other drums keep time under the push, and the lines go on
            if (song.Tracks.Definitions[track].Role is TrackRole.Melody or TrackRole.CounterMelody or TrackRole.Solo or TrackRole.Drum)
                continue;
            // a part pushes a change where it played in the bar before, not where it comes in
            var own = pushes.Where(x => timeline.Any(n => n.Position >= x.At - x.BarDuration - 1e-9 && n.Position < x.At - 1e-9)).Select(x => x.At).ToArray();
            bool On(double position) => own.Any(x => Math.Abs(x - position) < 1e-6);
            bool Before(double position) => own.Any(x => position > x - Early - 1e-6 && position < x - 1e-6);
            var bar = pushes.Length > 0 ? pushes.Min(x => x.BarDuration) : 0;
            result[track] = EventTimeline.Create(timeline.Duration, timeline
                .Where(note => !Before(note.Position))
                .Select(note => On(note.Position) ? (note.Value with { Duration = Math.Min(note.Value.Duration + Early, bar) }).ToTimelineItem(note.Position - Early) : note)
                .Select(note => own.FirstOrDefault(x => note.Position < x - Early - 1e-6 && note.Position + note.Value.Duration > x - Early + 1e-6) is var cut && cut > 0
                    ? (note.Value with { Duration = cut - Early - note.Position }).ToTimelineItem(note.Position)
                    : note)
                .OrderBy(x => x.Position));
        }

        return draft with { Notes = result.ToImmutable() };
    }
}
