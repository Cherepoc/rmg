using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>Who plays a figure: the kick, the snare, the riff, the chords, the claves, the bass or the drum keeping time, in the order a section draws them.</summary>
public enum FigurePlayer
{
    Ground,
    Backbeat,
    Riff,
    Claves,
    Chords,
    Bass,
    Time
}

/// <summary>
///     A figure a player plays in place of its own rhythm: where its notes start, in 16ths, over as many bars as it lasts,
///     or another player's, wherever that player's notes start, the kick's with the riff, the bass's with the kick; and
///     how a section's energy leans it, a loud section the likelier above 0.
/// </summary>
/// <param name="Sixteenths">How many 16ths it lasts, a bar of four's or two; none for one that follows, which lasts as what it follows does.</param>
/// <param name="Follows">The player whose notes' starts it strikes, played before it, none for one of its own onsets.</param>
/// <param name="AnswersKick">Whether it plays around the kick, between its beats, and so only in a bar the kick plays in, as a rolling bass does.</param>
public sealed record Figure(string Name, string Label, FigurePlayer Player, ImmutableArray<int> Onsets, int Sixteenths, FigurePlayer? Follows, double EnergyLean, bool AnswersKick)
{
    /// <summary>The 16ths between its notes where it strikes at a steady step from the bar's start, every 16th or 8th, so that it plays in any meter; 0 for one of its own shape.</summary>
    public int Step { get; } = Sixteenths == 16 && Onsets.Length > 0 && 16 % Onsets.Length == 0 && Onsets.Select((x, i) => x == i * (16 / Onsets.Length)).All(x => x)
        ? 16 / Onsets.Length
        : 0;
}

/// <summary>
///     The figures a genre gives its players in its sections: a groove or thrash band's kick with the riff, or in 16ths, a
///     double kick; a black metal band's blast beat, its cymbal riding it, and its tremolo-picked riff; a breakbeat's kick and snare; a heavy
///     band's galloping riff, and a breakdown's kick with the riff always (<see cref="Breakdowns" />); a clave's three and two,
///     and the bossa nova's on the snare's stroke, its cross-stick; a trance's chords in 16ths, its arpeggio's steady run, and its bass
///     rolling in the 16ths after the kick or on the offbeats. A section draws each player's, its own rhythm or one of its
///     figures, from a sequence of its own, by the feel facet and the genre's pool, leaned by its energy, a figure two
///     players play, a blast's or a breakbeat's, drawn by the first and played by both; the songs play their own rhythms
///     always. The player plays the figure in every bar it plays in, its own notes
///     there given up, each struck as its own was at or before it. A stage of the song before its fills, which play over
///     it (<see cref="SongStages" />), in a meter of four for a figure of 16ths.
/// </summary>
internal static class Figures
{
    /// <summary>The players in the turns they play their figures in, each turn after what it follows.</summary>
    private static readonly FigurePlayer[][] Turns = [[FigurePlayer.Riff, FigurePlayer.Backbeat, FigurePlayer.Time, FigurePlayer.Claves, FigurePlayer.Chords], [FigurePlayer.Ground], [FigurePlayer.Bass]];

    /// <summary>The part a player is, the drums' for the drums' players.</summary>
    public static TrackRole RoleOf(FigurePlayer player) => player switch
    {
        FigurePlayer.Riff => TrackRole.Riff,
        FigurePlayer.Chords => TrackRole.Chords,
        FigurePlayer.Bass => TrackRole.Bass,
        FigurePlayer.Ground or FigurePlayer.Backbeat or FigurePlayer.Time or FigurePlayer.Claves => TrackRole.Drum,
        _ => throw new ArgumentOutOfRangeException(nameof(player), player, null)
    };

    /// <summary>A player's own rhythm, as its pool names it.</summary>
    public const string Own = "own";

    /// <summary>Every figure, by its player.</summary>
    public static ImmutableArray<Figure> All { get; } =
    [
        new("withRiff", "With the riff, wherever it plays", FigurePlayer.Ground, [], 0, FigurePlayer.Riff, 1, false),
        new("doubleKick", "A double kick, every 16th", FigurePlayer.Ground, [..Enumerable.Range(0, 16)], 16, null, 1, false),
        new("blast", "A blast beat, with the snare on every 8th", FigurePlayer.Ground, [0, 2, 4, 6, 8, 10, 12, 14], 16, null, 1, false),
        new("blast", "A blast beat, with the kick on every 8th", FigurePlayer.Backbeat, [0, 2, 4, 6, 8, 10, 12, 14], 16, null, 1, false),
        new("blast", "A blast beat, a cymbal riding it on every 8th", FigurePlayer.Time, [0, 2, 4, 6, 8, 10, 12, 14], 16, null, 1, false),
        new("breakbeat", "A breakbeat, with the snare broken around 2 and 4", FigurePlayer.Ground, [0, 2, 10, 11], 16, null, 0, false),
        new("breakbeat", "A breakbeat, with the kick broken around 1 and 3", FigurePlayer.Backbeat, [4, 7, 9, 12, 15], 16, null, 0, false),
        new("gallop", "A gallop, an 8th and two 16ths a beat", FigurePlayer.Riff, [0, 2, 3, 4, 6, 7, 8, 10, 11, 12, 14, 15], 16, null, 0, false),
        new("tremolo", "Tremolo-picked, every 16th", FigurePlayer.Riff, [..Enumerable.Range(0, 16)], 16, null, 0, false),
        new("sixteenths", "Every 16th, an arpeggio's steady run or stabs", FigurePlayer.Chords, [..Enumerable.Range(0, 16)], 16, null, 0, false),
        new("rolling", "Rolling, the three 16ths after every beat's kick", FigurePlayer.Bass, [1, 2, 3, 5, 6, 7, 9, 10, 11, 13, 14, 15], 16, null, 0, true),
        new("offbeat", "On the offbeats, the 8th between the kicks", FigurePlayer.Bass, [2, 6, 10, 14], 16, null, 0, true),
        new("withKick", "With the kick, wherever it plays", FigurePlayer.Bass, [], 0, FigurePlayer.Ground, 0, false),
        new("clave32", "The clave's 3-2", FigurePlayer.Claves, [0, 6, 12, 20, 24], 32, null, 0, false),
        new("clave23", "The clave's 2-3", FigurePlayer.Claves, [4, 8, 16, 22, 28], 32, null, 0, false),
        new("bossaClave", "The bossa nova's clave, the 3-2 with its last stroke an 8th later", FigurePlayer.Backbeat, [0, 6, 12, 20, 26], 32, null, 0, false)
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
        FigurePlayer.Time => "Hi-hat's figure",
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
            // a figure of its own shape in a bar of 16 16ths alone, a steady one in any
            var isFour = song.SectionMeters[id].Sixteenths == 16;
            var energy = SectionEnergy.Tilt(section.Energy, section.Rhythm.Coupling);
            var figures = new Dictionary<FigurePlayer, Figure?>();
            foreach (var player in players)
            {
                // the player's own rhythm where a genre's pool keeps no figure a bar not in four plays
                Weighted<string>[] options = [..ByConvention.Weigh(OptionsOf(player).Select(x => (x.Name, x.Weight)), section.Facets[Facet.Feel],
                    song.Genre.Pool<string>(GenreChoices.Figures[player], x => x)).Where(x => isFour || Of(player, x.Value) is not { Follows: null, Step: 0 })];
                if (options.Length == 0)
                    options = [new Weighted<string>(1, Own)];
                var name = context.Pick(energy.Weigh(options, x => Of(player, x)?.EnergyLean ?? 0));
                // a figure an earlier player drew that this one plays too, a blast's snare with its kick, drawn all the same
                // so that the draws are as many
                var shared = figures.Values.FirstOrDefault(x => x is not null && Of(player, x.Name) is not null);
                figures[player] = shared is not null && (isFour || shared.Step > 0) ? Of(player, shared.Name) : Of(player, name);
                // a breakdown's kick with its riff, drawn all the same
                if (player == FigurePlayer.Ground && section.Breakdown)
                    figures[player] = Of(player, "withRiff");
                // and none in a calm section, neither a riff's tremolo nor the drums' blast, drawn all the same
                if (section.Form.Kind == SectionFormKind.Calm)
                    figures[player] = null;
            }
            return figures;
        });
        // the claves keeping the song's clave, the first section's that plays one, as a clave's direction runs through a
        // song, every section drawn all the same
        if (drawn.Values.Select(x => x[FigurePlayer.Claves]).FirstOrDefault(x => x is not null) is { } clave)
            foreach (var figures in drawn.Values.Where(x => x[FigurePlayer.Claves] is not null))
                figures[FigurePlayer.Claves] = clave;
        // but a section running a figure across its bar lines, its cycle kept, as a mutating groove keeps it, its figures
        // drawn all the same: one of fixed onsets, as one that follows, the kick with the riff, follows it as the cycle runs it
        foreach (var id in drawn.Keys.Where(draft.Polymeters.Contains))
            foreach (var player in players)
                drawn[id][player] = drawn[id][player] is { Follows: not null } follows ? follows : null;

        // the figures laid, by their sections and players, which the stages after read: one drawn and never played, its
        // player resting or its time halved, leaves the player its own rhythm
        var laid = new HashSet<(int Section, FigurePlayer Player)>();
        if (drawn.Values.All(x => x.Values.All(f => f is null)))
            return Laid(song, draft, drawn, laid);

        // a two-bar figure's side counted from the song's first bar, so that a clave runs on across the sections
        var barsBefore = new Dictionary<SectionSpan, int>();
        foreach (var span in song.Form.Map.PlayedSections)
            barsBefore[span] = barsBefore.Count == 0 ? 0 : barsBefore.Last().Value + barsBefore.Last().Key.BarCount;

        int? TrackOf(TrackRole role) => song.Playing.Where(x => x.Value == role).Select(x => (int?)x.Key).FirstOrDefault(x => draft.Timeline.TrackTimelineMap.ContainsKey(x!.Value));
        var all = draft.Timeline.TrackTimelineMap;
        var tracks = new Dictionary<FigurePlayer, int[]>
        {
            [FigurePlayer.Ground] = [..DrumGroups.Kick.Tracks.Where(all.ContainsKey)],
            [FigurePlayer.Backbeat] = [..DrumGroups.Snare.Tracks.Where(all.ContainsKey)],
            [FigurePlayer.Time] = [..DrumGroups.Timekeepers.Tracks.Where(all.ContainsKey)],
            [FigurePlayer.Riff] = TrackOf(TrackRole.Riff) is { } own ? [own] : [],
            [FigurePlayer.Claves] = [..new[] { DrumGroups.GetTrackNumber(DrumDefinitions.Claves) }.Where(all.ContainsKey)],
            [FigurePlayer.Chords] = TrackOf(TrackRole.Chords) is { } chords ? [chords] : [],
            [FigurePlayer.Bass] = TrackOf(TrackRole.Bass) is { } bass ? [bass] : []
        };

        // what a figure that follows a player strikes with: its tracks, the riff's the bass's where the song has no riff
        int[] Followed(FigurePlayer player) => player == FigurePlayer.Riff && tracks[player].Length == 0 && TrackOf(TrackRole.Bass) is { } bass ? [bass] : tracks[player];

        // the players in turn by what they follow: the kick with the riff as its figure plays it, the bass around the kick
        // as its figure plays it
        foreach (var turn in Turns)
        {
            var timelines = draft.Timeline.TrackTimelineMap;
            var edits = new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>>();
            foreach (var player in turn)
            foreach (var track in tracks[player])
                edits[track] = notes =>
                {
                    var played = notes.ToList();
                    // the sections, and an intro's window before the first, which plays its first bars
                    foreach (var span in song.Form.Map.PlayedSections)
                    {
                        if (drawn[span.SectionId][player] is not { } figure)
                            continue;
                        // one that follows where the song plays what it follows, the riff's where it has a riff or a bass
                        if (figure.Follows is { } followed && Followed(followed).Length == 0)
                            continue;
                        // every bar, a short last one's figure cut at the section's end
                        for (var index = 0; index < span.BarCount; index++)
                        {
                            var (bar, end) = span.Bar(index);
                            bool In(double position) => position >= bar - 1e-9 && position < end - 1e-9;
                            var own = played.Where(x => In(x.Position)).ToArray();
                            double[] onsets = figure.Follows is { } follows
                                ? [..Followed(follows).SelectMany(t => timelines[t].EventTimeline).Where(x => In(x.Position)).Select(x => x.Position).Distinct().Order()]
                                : figure.Step > 0
                                    ? [..Enumerable.Range(0, span.Meter.Sixteenths).Where(x => x % figure.Step == 0).Select(x => bar + x * 0.25)]
                                    : [..figure.Onsets.Select(x => x - (barsBefore[span] + index) % (figure.Sixteenths / 16) * 16).Where(x => x is >= 0 and < 16).Select(x => bar + x * 0.25)];
                            // in a bar both play, each struck as the player's own note at or before it was; one around the kick
                            // where the kick plays, the player's own rhythm where it rests; and no drum's of fixed onsets in half
                            // or double time, whose figure is the drum's own rhythm stretched or pressed, as the time feel
                            // stretches the drums alone, but one that follows, whose notes are as what it follows plays them
                            // there, a breakdown's kick on its chugs
                            if (own.Length == 0 || onsets.Length == 0 || song.SectionOf(span.SectionId).TimeFeel != 0 && figure.Follows is null && Drums.Contains(player)
                                || figure.AnswersKick && !tracks[FigurePlayer.Ground].Any(t => timelines[t].EventTimeline.Any(x => In(x.Position))))
                                continue;
                            laid.Add((span.SectionId, player));
                            played.RemoveAll(x => In(x.Position));
                            played.AddRange(onsets.Where(In).Select(at => own.Where(x => x.Position <= at + 1e-9).DefaultIfEmpty(own[0]).Last().Value.ToTimelineItem(at)));
                        }
                    }

                    return EventTimeline.Create(notes.Duration, played.OrderBy(x => x.Position));
                };
            draft = draft with { Timeline = draft.Timeline.MapTrackEvents(edits) };
        }

        return Laid(song, draft, drawn, laid);
    }

    /// <summary>The drums' players, whose rhythm a half or double time stretches or presses.</summary>
    private static readonly ImmutableHashSet<FigurePlayer> Drums = [FigurePlayer.Ground, FigurePlayer.Backbeat, FigurePlayer.Time, FigurePlayer.Claves];

    /// <summary>The song with the figures laid traced and handed on, and a heavy figure's backbeat under them.</summary>
    private static SongDraft Laid(SongDecisions song, SongDraft draft, Dictionary<int, Dictionary<FigurePlayer, Figure?>> drawn, HashSet<(int Section, FigurePlayer Player)> laid)
    {
        var figures = drawn.ToImmutableDictionary(x => x.Key, x => x.Value.Where(f => f.Value is not null && laid.Contains((x.Key, f.Key))).ToImmutableDictionary(f => f.Key, f => f.Value!.Name));
        foreach (var (id, played) in figures)
            StateTrace.RecordSection(TracePoints.Figures, SectionGenerator.SectionTrace, id, string.Join(", ", played.Select(x => $"{x.Key} {x.Value}")), played);
        return HeavyBackbeat(song, draft with { Figures = figures });
    }

    /// <summary>The ground's figures heavy enough that the snare under them strikes its head: the double kick, the blast and a breakdown's kick with its riff.</summary>
    private static readonly ImmutableHashSet<string> Heavy = ["doubleKick", "blast", "withRiff"];

    /// <summary>
    ///     A heavy figure's backbeat: the snare under it struck on its head, never its cross-stick, as a thrash or a
    ///     breakdown's drummer hits it; and from a section's second phrase on, a bar the snare would leave empty under it
    ///     given its backbeat, the meter's (2 and 4 in four), so that a double kick runs a phrase at most without one,
    ///     as a drummer brings the snare in, where the section's kit plays none too.
    /// </summary>
    private static SongDraft HeavyBackbeat(SongDecisions song, SongDraft draft)
    {
        var map = song.Form.Map;
        var spans = map.PlayedSections.Where(x => draft.Figures.GetValueOrDefault(x.SectionId)?.GetValueOrDefault(FigurePlayer.Ground) is { } ground && Heavy.Contains(ground)).ToArray();
        var snares = DrumGroups.Snare.Tracks.Where(draft.Timeline.TrackTimelineMap.ContainsKey).ToArray();
        if (spans.Length == 0 || snares.Length == 0)
            return draft;

        return draft with
        {
            Timeline = draft.Timeline.MapTrackEvents(snares.ToDictionary(x => x, track => (Func<EventTimeline<StateMap>, EventTimeline<StateMap>>)(notes =>
            {
                var head = DrumStrokes.At(StateDepths.Note, 0);
                var played = notes.Select(x => spans.Any(s => x.IsWithin(s.Start, s.End)) ? x.Value.MergeWith(head).ToTimelineItem(x.Position) : x).ToList();
                // a snare the song never plays, which has no note to add
                if (played.Count == 0)
                    return notes;
                foreach (var span in spans)
                {
                    // the section's own snare note, its state, which a backbeat added plays, or the nearest the snare plays in the
                    // song where its kit there has none
                    var own = played.FirstOrDefault(x => x.IsWithin(span.Start, span.End)).Value ?? played.MinBy(x => Math.Abs(x.Position - span.Start)).Value;
                    for (var bar = span.Meter.PhraseBars; bar < span.BarCount; bar++)
                    {
                        var (start, end) = span.Bar(bar);
                        if (played.Any(x => x.IsWithin(start, end)))
                            continue;
                        played.AddRange(span.Meter.Backbeats.Select(x => start + x).Where(x => x < end - 1e-9).Select(x => own.MergeWith(head).ToTimelineItem(x)));
                    }
                }

                return EventTimeline.Create(notes.Duration, played.OrderBy(x => x.Position));
            })))
        };
    }
}
