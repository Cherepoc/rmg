using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>A snare's ghost notes, a stage of the song after its notes are made (<see cref="SongStages" />).</summary>
internal static class Ghosts
{
    /// <summary>The chance a section's snare plays ghost notes around its backbeat, by its groove facet: the songs' never; a genre's where it names it.</summary>
    public static ByConvention Chance { get; } = new(0, 0, 0);

    /// <summary>How likely a 16th off the beat is ghosted next to a backbeat, before or after it, and elsewhere in the bar.</summary>
    public const double NearChance = 0.5, FarChance = 0.15;

    /// <summary>How loud a ghost note plays against the bar's loudest snare note: under it, felt more than heard.</summary>
    public const double Loudness = 0.45;

    /// <summary>How long a ghost note is held at most, in beats: a 16th.</summary>
    public const double Length = 0.25;

    /// <summary>
    ///     A snare's ghost notes, a funk or soul drummer's: in a ghosting section, its snares keeping the backbeat play soft
    ///     16ths off the beat between their backbeats, the likelier next to one, each its bar's snare note played softly. A
    ///     section's decision, the 16ths it ghosts the same in each of its bars, as a drummer keeps a groove, from a sequence
    ///     of its own; never in a bar a fill or stop time plays in, nor in a drum solo.
    /// </summary>
    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var notes = SongStages.NotesOf(draft);
        var snares = notes.Keys.Where(x => song.Tracks.Definitions[x] is PercussionInstrumentTrack definition && DrumRoles.Of(definition.StateMap) == DrumRole.Backbeat
                                           && (x == DrumGroups.GetTrackNumber(DrumDefinitions.AcousticSnare) || x == DrumGroups.GetTrackNumber(DrumDefinitions.ElectricSnare))).ToArray();
        // every section's draw of each 16th of its bar, none where it does not ghost, drawn for every section so that the
        // draws are as many
        var ghosting = song.SectionIds.Distinct().ToDictionary(id => id, id =>
        {
            var section = song.SectionOf(id);
            var context = song.Stream(SongStream.Ghosts, id);
            var ghosts = context.TestProbability(Chance.At(section.Facets[Facet.Groove], song.Genre.Chance(GenreChoices.Ghosts, "sections")));
            var draws = Enumerable.Range(0, (int)Math.Round(song.SectionMeters[id].BarDuration / Length)).Select(_ => context.GenerateDouble()).ToArray();
            StateTrace.Record(TracePoints.Ghosts, SectionGenerator.SectionTrace, id, 0, StateMap.Default, 0, $"{ghosts}", ghosts);
            return ghosts ? draws : null;
        });
        if (snares.Length == 0 || ghosting.Values.All(x => x is null))
            return draft;

        // but where a fill plays, from its first note to its line, and in stop time
        var fills = draft.Runs.Where(x => !x.Positions.IsEmpty).Select(x => (From: x.Positions.Min(), To: x.Line)).ToArray();
        var stops = song.StopTime;
        bool Busy(double from, double to) => fills.Any(x => x.From < to - 1e-9 && x.To > from + 1e-9) || stops.Any(x => x.Start < to - 1e-9 && x.End > from + 1e-9);

        var result = notes.ToBuilder();
        foreach (var track in snares)
        {
            var timeline = notes[track];
            var played = timeline.ToList();
            foreach (var (span, index) in song.Form.Map.Sections.Select((x, i) => (x, i)))
            {
                if (ghosting[span.SectionId] is not { } draws || song.Solos.TryGetValue(index, out var solo) && solo.IsDrumSolo)
                    continue;
                var bar = span.Meter.BarDuration;
                for (var start = span.Start; start < span.End - 1e-9; start += bar)
                {
                    var end = Math.Min(span.End, start + bar);
                    var own = timeline.Where(x => x.Position >= start - 1e-9 && x.Position < end - 1e-9).ToArray();
                    if (own.Length == 0 || Busy(start, end))
                        continue;
                    var loudest = own.MaxBy(x => x.Value.Velocity);
                    for (var k = 0; k < draws.Length; k++)
                    {
                        var at = start + k * Length;
                        // off the beat, where the snare does not play, the likelier next to its note
                        if (at >= end - 1e-9 || k % 4 == 0 || own.Any(x => Math.Abs(x.Position - at) < Length - 1e-9))
                            continue;
                        var near = own.Any(x => Math.Abs(x.Position - at) < 2 * Length - 1e-9);
                        if (draws[k] < (near ? NearChance : FarChance))
                            played.Add((loudest.Value with { Velocity = loudest.Value.Velocity * Loudness, Duration = Math.Min(loudest.Value.Duration, Length) }).ToTimelineItem(at));
                    }
                }
            }

            result[track] = EventTimeline.Create(timeline.Duration, played.OrderBy(x => x.Position));
        }

        return draft with { Notes = result.ToImmutable() };
    }
}
