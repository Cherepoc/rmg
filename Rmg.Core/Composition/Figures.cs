using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>Who plays a figure: the kick, the snare, the riff, the chords, the claves or the bass, in the order a section draws them.</summary>
public enum FigurePlayer
{
    Ground,
    Backbeat,
    Riff,
    Claves,
    Chords,
    Bass
}

/// <summary>
///     A figure a player plays in place of its own rhythm: where its notes start, in 16ths, over as many bars as it lasts,
///     or the riff's, wherever the riff's notes start; and how a section's energy leans it, a loud section the likelier
///     above 0.
/// </summary>
/// <param name="Sixteenths">How many 16ths it lasts, a bar of four's or two; none for the riff's, which lasts as the riff does.</param>
/// <param name="AnswersKick">Whether it plays around the kick, between its beats, and so only in a bar the kick plays in, as a rolling bass does.</param>
public sealed record Figure(string Name, string Label, FigurePlayer Player, ImmutableArray<int> Onsets, int Sixteenths, bool FollowsRiff, double EnergyLean, bool AnswersKick);

/// <summary>
///     The figures a genre gives its players in its sections: a groove or thrash band's kick with the riff, or in 16ths, a
///     double kick; a black metal band's blast beat and its tremolo-picked riff; a breakbeat's kick and snare; a heavy
///     band's galloping riff, and a breakdown's kick with the riff always (<see cref="Breakdowns" />); a clave's three and two; a trance's chords in 16ths, its arpeggio's steady run, and its bass
///     rolling in the 16ths after the kick or on the offbeats. A section draws each player's, its own rhythm or one of its
///     figures, from a sequence of its own, by the feel facet and the genre's pool, leaned by its energy, a figure two
///     players play, a blast's or a breakbeat's, drawn by the first and played by both; the songs play their own rhythms
///     always. The player plays the figure in every bar it plays in, its own notes
///     there given up, each struck as its own was at or before it. A stage of the song before its fills, which play over
///     it (<see cref="SongStages" />), in a meter of four for a figure of 16ths.
/// </summary>
internal static class Figures
{
    /// <summary>A player's own rhythm, as its pool names it.</summary>
    public const string Own = "own";

    /// <summary>Every figure, by its player.</summary>
    public static ImmutableArray<Figure> All { get; } =
    [
        new("withRiff", "With the riff, wherever it plays", FigurePlayer.Ground, [], 0, true, 1, false),
        new("doubleKick", "A double kick, every 16th", FigurePlayer.Ground, [..Enumerable.Range(0, 16)], 16, false, 1, false),
        new("blast", "A blast beat, with the snare on every 8th", FigurePlayer.Ground, [0, 2, 4, 6, 8, 10, 12, 14], 16, false, 1, false),
        new("blast", "A blast beat, with the kick on every 8th", FigurePlayer.Backbeat, [0, 2, 4, 6, 8, 10, 12, 14], 16, false, 1, false),
        new("breakbeat", "A breakbeat, with the snare broken around 2 and 4", FigurePlayer.Ground, [0, 2, 10, 11], 16, false, 0, false),
        new("breakbeat", "A breakbeat, with the kick broken around 1 and 3", FigurePlayer.Backbeat, [4, 7, 9, 12, 15], 16, false, 0, false),
        new("gallop", "A gallop, an 8th and two 16ths a beat", FigurePlayer.Riff, [0, 2, 3, 4, 6, 7, 8, 10, 11, 12, 14, 15], 16, false, 0, false),
        new("tremolo", "Tremolo-picked, every 16th", FigurePlayer.Riff, [..Enumerable.Range(0, 16)], 16, false, 0, false),
        new("sixteenths", "Every 16th, an arpeggio's steady run or stabs", FigurePlayer.Chords, [..Enumerable.Range(0, 16)], 16, false, 0, false),
        new("rolling", "Rolling, the three 16ths after every beat's kick", FigurePlayer.Bass, [1, 2, 3, 5, 6, 7, 9, 10, 11, 13, 14, 15], 16, false, 0, true),
        new("offbeat", "On the offbeats, the 8th between the kicks", FigurePlayer.Bass, [2, 6, 10, 14], 16, false, 0, true),
        new("clave32", "The clave's 3-2", FigurePlayer.Claves, [0, 6, 12, 20, 24], 32, false, 0, false),
        new("clave23", "The clave's 2-3", FigurePlayer.Claves, [4, 8, 16, 22, 28], 32, false, 0, false)
    ];

    /// <summary>A player's options, its own rhythm first, as its pool shows them, the songs' weight of each: their own always.</summary>
    public static IEnumerable<(string Name, string Label, ByConvention Weight)> OptionsOf(FigurePlayer player) =>
    [
        (Own, "Its own rhythm", new ByConvention(1, 1, 1)),
        ..All.Where(x => x.Player == player).Select(x => (x.Name, x.Label, new ByConvention(0, 0, 0)))
    ];

    /// <summary>A player as its pool's page shows it.</summary>
    public static string LabelOf(FigurePlayer player) => player switch
    {
        FigurePlayer.Ground => "Kick's figure",
        FigurePlayer.Backbeat => "Snare's figure",
        FigurePlayer.Riff => "Riff's figure",
        FigurePlayer.Chords => "Chords' figure",
        FigurePlayer.Bass => "Bass's figure",
        _ => "Claves' figure"
    };

    /// <summary>A player's figure of the name, none for its own rhythm or one it does not play.</summary>
    private static Figure? Of(FigurePlayer player, string name) => All.FirstOrDefault(x => x.Player == player && x.Name == name);

    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var players = Enum.GetValues<FigurePlayer>();
        // every section's figure of each player, none for its own rhythm, each player drawn in turn
        var drawn = song.SectionIds.Distinct().ToDictionary(id => id, id =>
        {
            var section = song.SectionOf(id);
            var context = song.Stream(SongStream.Figures, id);
            // a figure of 16ths in a bar of them alone
            var isFour = song.SectionMeters[id].Sixteenths == 16;
            var energy = SectionEnergy.Tilt(section.Energy, section.Rhythm.Coupling);
            var figures = new Dictionary<FigurePlayer, Figure?>();
            foreach (var player in players)
            {
                var options = ByConvention.Weigh(OptionsOf(player).Select(x => (x.Name, x.Weight)), section.Facets[Facet.Feel],
                    song.Genre.Pool<string>(GenreChoices.Figures[player], x => x)).Where(x => isFour || Of(player, x.Value) is not { FollowsRiff: false });
                var name = context.Pick(energy.Weigh(options, x => Of(player, x)?.EnergyLean ?? 0));
                // a figure an earlier player drew that this one plays too, a blast's snare with its kick, drawn all the same
                // so that the draws are as many
                var shared = figures.Values.FirstOrDefault(x => x is not null && Of(player, x.Name) is not null);
                figures[player] = shared is not null && isFour ? Of(player, shared.Name) : Of(player, name);
                // a breakdown's kick with its riff, drawn all the same
                if (player == FigurePlayer.Ground && section.Breakdown)
                    figures[player] = Of(player, "withRiff");
            }
            return figures;
        });
        // the claves keeping the song's clave, the first section's that plays one, as a clave's direction runs through a
        // song, every section drawn all the same
        if (drawn.Values.Select(x => x[FigurePlayer.Claves]).FirstOrDefault(x => x is not null) is { } clave)
            foreach (var figures in drawn.Values.Where(x => x[FigurePlayer.Claves] is not null))
                figures[FigurePlayer.Claves] = clave;
        foreach (var (id, figures) in drawn)
        {
            var played = figures.Where(x => x.Value is not null).ToImmutableDictionary(x => x.Key, x => x.Value!.Name);
            StateTrace.Record(TracePoints.Figures, SectionGenerator.SectionTrace, id, 0, StateMap.Default, 0, string.Join(", ", played.Select(x => $"{x.Key} {x.Value}")), played);
        }

        draft = draft with { Figures = drawn.ToImmutableDictionary(x => x.Key, x => x.Value.Where(f => f.Value is not null).ToImmutableDictionary(f => f.Key, f => f.Value!.Name)) };
        if (drawn.Values.All(x => x.Values.All(f => f is null)))
            return draft;

        var timelines = draft.Timeline.TrackTimelineMap;
        int? TrackOf(TrackRole role) => song.Playing.Where(x => x.Value == role).Select(x => (int?)x.Key).FirstOrDefault(x => timelines.ContainsKey(x!.Value));
        var riff = TrackOf(TrackRole.Riff) ?? TrackOf(TrackRole.Bass);
        var tracks = new Dictionary<FigurePlayer, int[]>
        {
            [FigurePlayer.Ground] = [..DrumGroups.Kick.Drums.Select(DrumGroups.GetTrackNumber).Where(timelines.ContainsKey)],
            [FigurePlayer.Backbeat] = [..DrumGroups.Snare.Drums.Select(DrumGroups.GetTrackNumber).Where(timelines.ContainsKey)],
            [FigurePlayer.Riff] = TrackOf(TrackRole.Riff) is { } own ? [own] : [],
            [FigurePlayer.Claves] = [..new[] { DrumGroups.GetTrackNumber(DrumDefinitions.Claves) }.Where(timelines.ContainsKey)],
            [FigurePlayer.Chords] = TrackOf(TrackRole.Chords) is { } chords ? [chords] : [],
            [FigurePlayer.Bass] = TrackOf(TrackRole.Bass) is { } bass ? [bass] : []
        };

        var edits = new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>>();
        foreach (var player in players)
        foreach (var track in tracks[player])
            edits[track] = notes =>
            {
                var played = notes.ToList();
                foreach (var span in song.Form.Map.Sections)
                {
                    if (drawn[span.SectionId][player] is not { } figure)
                        continue;
                    // the riff's where the song has a riff or a bass
                    if (figure.FollowsRiff && riff is null)
                        continue;
                    var bars = (int)Math.Round(span.Duration / span.Meter.BarDuration);
                    for (var index = 0; index < bars; index++)
                    {
                        var bar = span.Start + index * span.Meter.BarDuration;
                        var end = bar + span.Meter.BarDuration;
                        bool In(double position) => position >= bar - 1e-9 && position < end - 1e-9;
                        var own = played.Where(x => In(x.Position)).ToArray();
                        double[] onsets = figure.FollowsRiff
                            ? [..timelines[riff!.Value].EventTimeline.Where(x => In(x.Position)).Select(x => x.Position).Distinct()]
                            : [..figure.Onsets.Select(x => x - index % (figure.Sixteenths / 16) * 16).Where(x => x is >= 0 and < 16).Select(x => bar + x * 0.25)];
                        // in a bar both play, each struck as the player's own note at or before it was; one around the kick
                        // where the kick plays, the player's own rhythm where it rests
                        if (own.Length == 0 || onsets.Length == 0 || figure.AnswersKick && !tracks[FigurePlayer.Ground].Any(t => timelines[t].EventTimeline.Any(x => In(x.Position))))
                            continue;
                        played.RemoveAll(x => In(x.Position));
                        played.AddRange(onsets.Select(at => own.Where(x => x.Position <= at + 1e-9).DefaultIfEmpty(own[0]).Last().Value.ToTimelineItem(at)));
                    }
                }

                return EventTimeline.Create(notes.Duration, played.OrderBy(x => x.Position));
            };

        return draft with { Timeline = draft.Timeline.MapTrackEvents(edits) };
    }
}
