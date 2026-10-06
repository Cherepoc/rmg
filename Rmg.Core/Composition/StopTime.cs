using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>Stop time, a stage of the song after it is put together (<see cref="SongStages" />).</summary>
internal static class StopTime
{
    /// <summary>
    ///     The chance a section plays its appearances' first phrase in stop time, by its form facet: never in the plainest
    ///     songs, now and then past them; a genre's where it names it, the blues' and rock's.
    /// </summary>
    public static ByConvention Chance { get; } = new(0, 0.03, 0.03);

    /// <summary>How a section's energy leans the chance, through its odds: a quiet section the likelier, a verse more than a chorus.</summary>
    public const double EnergyLean = -1;

    /// <summary>How long a stop-time hit is held at most, in beats.</summary>
    public const double Hit = 1;

    /// <summary>Where the song plays in stop time: every stop-time section's appearances' first phrase, its start, end and bar.</summary>
    internal static (double Start, double End, double BarDuration)[] Phrases(SongDecisions song)
    {
        var stops = song.SectionIds.Distinct().ToDictionary(id => id, id =>
        {
            var section = song.SectionOf(id);
            var chance = SectionEnergy.Tilt(section.Energy, section.Rhythm.Coupling)
                .Chance(Chance.At(section.Facets[Facet.Form], song.Genre.Chance(GenreChoices.StopTime, "sections")), EnergyLean);
            return song.Stream(SongStream.StopTime, id).TestProbability(chance);
        });
        // but the first section where an intro brings the band in part by part over it, whose parts not in yet would leave
        // its hits to none
        var intro = song.Form.Map.Intro;
        var built = intro.Kind == IntroKind.Entries && !intro.Window.IsBefore ? song.Form.Map.Sections[0] : null;
        return
        [
            ..song.Form.Map.Sections.Where(x => stops[x.SectionId] && x != built)
                .Select(x => (x.Start, Math.Min(x.End, x.Start + Meter.PhraseBarCount * x.Meter.BarDuration), x.Meter.BarDuration))
        ];
    }

    /// <summary>
    ///     Stop time: a section's appearances' first phrase, now and then, its band hitting the bar's first beat and resting
    ///     the rest of the bar, a hit held a beat at most, under the lines, the melody's, the counter-melody's and a solo's,
    ///     which go on. A section's decision, from a sequence of its own.
    /// </summary>
    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var phrases = Phrases(song);
        foreach (var id in song.SectionIds.Distinct())
        {
            var stops = song.Form.Map.Sections.Any(x => x.SectionId == id && phrases.Any(p => Math.Abs(p.Start - x.Start) < 1e-9));
            StateTrace.Record(TracePoints.StopTime, SectionGenerator.SectionTrace, id, 0, StateMap.Default, 0, $"{stops}", stops);
        }

        if (phrases.Length == 0)
            return draft;

        // where a note falls in stop time: off the bar's first beat, or on it, a hit
        (bool In, bool OnTheBar) Place(double position)
        {
            foreach (var (start, end, bar) in phrases)
                if (position >= start - 1e-9 && position < end - 1e-9)
                    return (true, (position - start + 1e-6) % bar < 2e-6);
            return (false, false);
        }

        // the drums' fills out of it, from a run's first note to its line, which go on as a drummer fills into the next
        // section, a drum never heard otherwise landing there
        var fills = draft.Runs.Where(x => !x.Positions.IsEmpty).Select(x => (From: x.Positions.Min(), To: x.Line)).ToArray();
        bool InAFill(double position) => fills.Any(x => position >= x.From - 1e-9 && position < x.To - 1e-9);

        var notes = SongStages.NotesOf(draft);
        var result = notes.ToBuilder();
        foreach (var (track, timeline) in notes)
        {
            var role = song.Tracks.Definitions[track].Role;
            if (role is TrackRole.Melody or TrackRole.CounterMelody or TrackRole.Solo)
                continue;
            result[track] = EventTimeline.Create(timeline.Duration, timeline
                .Where(note => Place(note.Position) is not (true, false) || role == TrackRole.Drum && InAFill(note.Position))
                .Select(note => Place(note.Position).In && !(role == TrackRole.Drum && InAFill(note.Position))
                    ? (note.Value with { Duration = Math.Min(note.Value.Duration, Hit) }).ToTimelineItem(note.Position)
                    : note));
        }

        return draft with { Notes = result.ToImmutable() };
    }
}
