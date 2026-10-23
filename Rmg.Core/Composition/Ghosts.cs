using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>A snare's ghost notes, a stage of the song after its notes are made (<see cref="SongStages" />).</summary>
internal static class Ghosts
{
    /// <summary>What a ghost keeps out of: a fill, stop time, a drum solo and the snare stopped by the form.</summary>
    internal const Hold KeepsOutOf = Hold.Fill | Hold.Build | Hold.Stop | Hold.DrumSolo | Hold.Cut | Hold.Polymeter;

    /// <summary>The chance a section's snare plays ghost notes around its backbeat, by its groove facet: the songs' never; a genre's where it names it.</summary>
    public static ByConvention Chance { get; } = new(0, 0, 0);

    /// <summary>How likely a 16th off the beat is ghosted next to a backbeat, before or after it, and elsewhere in the bar.</summary>
    public const double NearChance = 0.5, FarChance = 0.15;

    /// <summary>How far a genre's ghost notes lean those chances at its fewest and its most, as odds (<see cref="GenreChoices.GhostNotes" />).</summary>
    public const double GenreOdds = 4;

    /// <summary>How loud a ghost note is heard against the bar's loudest snare note, as a share of its velocity: well under it, felt more than heard.</summary>
    public const double Loudness = 0.4;

    /// <summary>How long a ghost note is held at most, in beats: a 16th.</summary>
    public const double Length = 0.25;

    /// <summary>
    ///     A snare's ghost notes, a funk or soul drummer's: in a ghosting section, its snares keeping the backbeat play soft
    ///     16ths off the beat between their backbeats, the likelier next to one, each on the snare's head, as soft as heard
    ///     against its bar's loudest note. A section's decision, the 16ths it ghosts the same in each of its bars, as a
    ///     drummer keeps a groove, from a sequence of its own; never in a bar a fill or stop time plays in, a tuplet's bar nor a drum solo.
    ///     The snares keeping the backbeat are the song's, as their roles are drawn for it, a wild section's redrawn roles
    ///     aside.
    /// </summary>
    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var notes = SongStages.NotesOf(draft);
        var snares = song.Tracks.DrumsOf(DrumRole.Backbeat).Where(x => notes.ContainsKey(x) && DrumGroups.SnareHeads.Contains(x)).ToArray();
        // every section's draw of each 16th of its bar, none where it does not ghost, drawn for every section so that the
        // draws are as many
        var ghosting = song.SectionIds.Distinct().ToDictionary(id => id, id =>
        {
            var section = song.SectionOf(id);
            var context = song.Stream(SongStream.Ghosts, id);
            var ghosts = context.TestProbability(Chance.At(section.Facets[Facet.Groove], song.Genre.Chance(GenreChoices.Ghosts, "sections")));
            var draws = Enumerable.Range(0, (int)Math.Round(song.SectionMeters[id].BarDuration / Length)).Select(_ => context.GenerateDouble()).ToArray();
            StateTrace.RecordSection(TracePoints.Ghosts, SectionGenerator.SectionTrace, id, $"{ghosts}", ghosts);
            return ghosts ? draws : null;
        });
        if (snares.Length == 0 || ghosting.Values.All(x => x is null))
            return draft;

        // but where a fill plays, from its first note to its line, in stop time and in a drum solo
        var holds = draft.Holds(song);
        var many = Tilt.Of(GenreOdds, song.Genre.Shift(GenreChoices.GhostNotes, "many"));
        var (nearChance, farChance) = (many.Chance(NearChance, 1), many.Chance(FarChance, 1));

        var result = notes.ToBuilder();
        foreach (var track in snares)
        {
            var timeline = notes[track];
            var played = timeline.ToList();
            foreach (var span in song.Form.Map.PlayedSections)
            {
                if (ghosting[span.SectionId] is not { } draws)
                    continue;
                foreach (var (_, start, end) in span.Bars)
                {
                    var own = timeline.Where(x => x.IsWithin(start, end)).ToArray();
                    // nor in a bar whose snare plays off the 16ths, a tuplet's, whose grid the ghosts would fall between
                    if (own.Length == 0 || holds.Meets(KeepsOutOf, start, end, track) || own.Any(x => Math.Abs((x.Position - start) / Length - Math.Round((x.Position - start) / Length)) > 1e-6))
                        continue;
                    var loudest = own.MaxBy(x => x.Value.Velocity);
                    // a ghost weaker than any beat of the bar
                    var weakest = own.Max(x => x.Value.State.GetStateValue(CompositionStateKinds.BeatRank)) + 1;
                    for (var k = 0; k < draws.Length; k++)
                    {
                        var at = start + k * Length;
                        // off the meter's pulses, where the snare does not play, the likelier next to its note
                        if (at >= end - 1e-9 || span.Meter.Pulses.Any(x => Math.Abs(x - k * Length) < 1e-9) || own.Any(x => Math.Abs(x.Position - at) < Length - 1e-9))
                            continue;
                        var near = own.Any(x => Math.Abs(x.Position - at) < 2 * Length - 1e-9);
                        if (draws[k] < (near ? nearChance : farChance))
                            played.Add((loudest.Value with
                            {
                                // on the drum's head, never a cross-stick's click
                                Pitches = [DrumGroups.GetDrum(track).Sounds[0].Code],
                                Duration = Math.Min(loudest.Value.Duration, Length),
                                State = loudest.Value.State.With(CompositionStateKinds.BeatRank, weakest)
                            }).WithVelocity(Render.ScaleLoudness(loudest.Value.Velocity, Loudness)).ToTimelineItem(at));
                    }
                }
            }

            result[track] = EventTimeline.Create(timeline.Duration, played.OrderBy(x => x.Position));
        }

        return draft with { Notes = result.ToImmutable() };
    }
}
