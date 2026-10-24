using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>Who plays a figure: the kick, the snare, the riff, the chords, the claves, the bass, the drum keeping time or the snare comping, in the order a section draws them.</summary>
public enum FigurePlayer
{
    Ground,
    Backbeat,
    Riff,
    Claves,
    Chords,
    Bass,
    Time,

    /// <summary>The snare comping where it colours the groove, a jazz drummer's, the backbeat its hi-hat's foot.</summary>
    Comp
}

/// <summary>
///     A figure a player plays in place of its own rhythm: where its notes start, in 16ths, over as many bars as it lasts,
///     or another player's, wherever that player's notes start, the kick's with the riff, the bass's with the kick; and
///     how a section's energy leans it, a loud section the likelier above 0.
/// </summary>
/// <param name="Sixteenths">How many 16ths it lasts, a bar of its meter's or two; none for one that follows, which lasts as what it follows does.</param>
/// <param name="Follows">The player whose notes' starts it strikes, played before it, none for one of its own onsets.</param>
/// <param name="AnswersKick">Whether it plays around the kick, between its beats, and so only in a bar the kick plays in, as a rolling bass does.</param>
/// <param name="In">The meter of its bars, four's where none is given.</param>
/// <param name="Exact">Whether it is played exactly at every end, as a clave, the swing ride or the waltz is, rather than as a player varies it (<see cref="Loose" />).</param>
/// <param name="Heavy">Whether it is a ground heavy enough that the snare under it strikes its head, as the double kick, the blast and the kick with its riff are.</param>
public sealed record Figure(string Name, string Label, FigurePlayer Player, ImmutableArray<int> Onsets, int Sixteenths, FigurePlayer? Follows, double EnergyLean, bool AnswersKick, Meter? In = null, bool Exact = false, bool Heavy = false)
{
    /// <summary>
    ///     Whether it is played as a player varies it, its onsets and the player's own notes each by a chance, by how
    ///     strictly the section plays it (<see cref="Figures.Strictness" />): every figure but one kept exact, one that
    ///     follows another player, and a pulse, the same every beat, a double kick's, a gallop's or a skank's, whose point
    ///     is that it never varies.
    /// </summary>
    public bool Loose => !Exact && Follows is null && !Pulse;

    /// <summary>Whether its onsets are the same in every beat.</summary>
    private bool Pulse => Onsets.Length > 0 && Sixteenths % 4 == 0 && Onsets.All(x => Onsets.Contains((x + 4) % Sixteenths));

    /// <summary>
    ///     Whether it plays in a bar of the meter: one that follows or strikes at a steady step in any, one of its own shape
    ///     in a meter whose bars' groups its own bars' are, a waltz's in three, a figure of four's in four.
    /// </summary>
    public bool Fits(Meter meter)
    {
        if (Follows is not null || Step > 0)
            return true;
        var bar = In ?? Meter.FourFour;
        return bar.Groups.SequenceEqual(meter.Groups);
    }

    /// <summary>The 16ths between its notes where it strikes at a steady step from the bar's start, every 16th or 8th, so that it plays in any meter; 0 for one of its own shape.</summary>
    public int Step { get; } = Sixteenths == 16 && Onsets.Length > 0 && 16 % Onsets.Length == 0 && Onsets.Select((x, i) => x == i * (16 / Onsets.Length)).All(x => x)
        ? 16 / Onsets.Length
        : 0;

    /// <summary>
    ///     Where it strikes in a bar of the meter at its steady step, in beats: a step of a quarter or more on the bar's
    ///     pulses, as many of them as it strikes of 4/4's, so that 6/8's boom and kick fall on its dotted quarters, 1 and 4;
    ///     a half's or more in a bar of uneven groups on the groups' starts, 5/4's 2+3 on 1 and 3; a finer one every 8th or
    ///     16th, the meter's own.
    /// </summary>
    public IEnumerable<double> StepsIn(Meter meter)
    {
        if (Step < 4)
            return Enumerable.Range(0, meter.Sixteenths).Where(x => x % Step == 0).Select(x => x * 0.25);
        if (Step >= 8 && meter.Groups.Distinct().Count() > 1)
        {
            var every = Math.Max(1, (int)Math.Round(Step / meter.Groups.Average(), MidpointRounding.AwayFromZero));
            return meter.Groups.Select((_, i) => meter.Groups.Take(i).Sum() * 0.25).Where((_, i) => i % every == 0);
        }
        var stride = Math.Max(1, (int)Math.Round(meter.Pulses.Length * Step / 16.0, MidpointRounding.AwayFromZero));
        return meter.Pulses.Where((_, i) => i % stride == 0);
    }
}

/// <summary>
///     The figures a genre gives its players in its sections: a groove or thrash band's kick with the riff, or in 16ths, a
///     double kick; a black metal band's blast beat, its cymbal riding it, and its tremolo-picked riff; a breakbeat's kick and snare; a heavy
///     band's galloping riff and its reverse gallop, and a breakdown's kick with the riff always (<see cref="Breakdowns" />); a jazz drummer's ride,
///     on the beats and the skip after 2 and 4, and a jazz drummer's comping on the snare; a clave's three and two, and the 6/8 bell over two bars, and the chords comping
///     in clave,
///     and the bossa nova's on the snare's stroke, its cross-stick; a trance's chords in 16ths, its arpeggio's steady run, and its bass
///     rolling in the 16ths after the kick or on the offbeats, a salsa's tumbao, the bossa nova's bass and the tresillo; four on the floor,
///     drum and bass's two-step, the surdo's kick, the snare on every beat, the train beat, the skank, the Charleston, a
///     bass's pulse in 8ths or 16ths, the boom-chicka, the bass on 1 and 3 and the chords on 2 and 4, and 6/8's chick, the
///     chords on the 8ths between its 1 and 4. A section draws each player's, its own rhythm or one of its
///     figures, from a sequence of its own, by the feel facet and the genre's pool, leaned by its energy, a figure two
///     players play, a blast's or a breakbeat's, drawn by the first and played by both; the songs play their own rhythms
///     always. The player plays the figure in every bar it plays in, its own notes
///     there given up but where a loose figure keeps them, as a player varies it, each struck as its own was at or before it. A stage of the song before its fills, which play over
///     it (<see cref="SongStages" />), a figure of its own shape in its own meter, the waltz in three, the rest in four.
/// </summary>
internal static class Figures
{
    /// <summary>What a drum's figure keeps out of: a drop's build roll, which the form wrote into its bars, and a part the form stops.</summary>
    internal const Hold KeepsOutOf = Hold.Build | Hold.Cut;

    /// <summary>The players in the turns they play their figures in, each turn after what it follows.</summary>
    // the snare's comping a turn after its backbeat's, the two its tracks, so that neither's edit takes the other's place
    private static readonly FigurePlayer[][] Turns = [[FigurePlayer.Riff, FigurePlayer.Backbeat, FigurePlayer.Time, FigurePlayer.Claves], [FigurePlayer.Ground, FigurePlayer.Chords, FigurePlayer.Comp], [FigurePlayer.Bass]];

    /// <summary>The part a player is, the drums' for the drums' players.</summary>
    public static TrackRole RoleOf(FigurePlayer player) => player switch
    {
        FigurePlayer.Riff => TrackRole.Riff,
        FigurePlayer.Chords => TrackRole.Chords,
        FigurePlayer.Bass => TrackRole.Bass,
        FigurePlayer.Ground or FigurePlayer.Backbeat or FigurePlayer.Time or FigurePlayer.Claves or FigurePlayer.Comp => TrackRole.Drum,
        _ => throw new ArgumentOutOfRangeException(nameof(player), player, null)
    };

    /// <summary>
    ///     How strictly a section plays a loose figure, by its feel facet: exactly at the plain end, its onsets kept and
    ///     the player's own notes left out; at the wild end the player's own rhythm nearly as much as the figure's. A note on both
    ///     plays always; one on the figure alone by this chance, one of the player's own alone by its rest.
    /// </summary>
    public static ByConvention Strictness { get; } = new(1, 0.9, 0.6);

    /// <summary>A player's own rhythm, as its pool names it.</summary>
    public const string Own = "own";

    /// <summary>Every figure, by its player.</summary>
    public static ImmutableArray<Figure> All { get; } =
    [
        new("withRiff", "With the riff, wherever it plays", FigurePlayer.Ground, [], 0, FigurePlayer.Riff, 1, false, Heavy: true),
        new("doubleKick", "A double kick, every 16th", FigurePlayer.Ground, [..Enumerable.Range(0, 16)], 16, null, 1, false, Heavy: true),
        new("blast", "A blast beat, with the snare on every 8th", FigurePlayer.Ground, [0, 2, 4, 6, 8, 10, 12, 14], 16, null, 1, false, Heavy: true),
        new("blast", "A blast beat, with the kick on every 8th", FigurePlayer.Backbeat, [0, 2, 4, 6, 8, 10, 12, 14], 16, null, 1, false),
        new("blast", "A blast beat, a cymbal riding it on every 8th", FigurePlayer.Time, [0, 2, 4, 6, 8, 10, 12, 14], 16, null, 1, false),
        // the swing ride, the skip played late by the song's swing
        new("swingRide", "The swing ride, on the beats and the skip after 2 and 4", FigurePlayer.Time, [0, 4, 6, 8, 12, 14], 16, null, 0, false, Exact: true),
        // the kick on every beat, a dance track's, a reggae's steppers, a pop chorus's, the likelier the louder the section
        new("fourOnTheFloor", "Four on the floor, the kick on every beat", FigurePlayer.Ground, [0, 4, 8, 12], 16, null, 1, false),
        // drum and bass's, the kick on 1 and the 3-and around the snare's 2 and 4
        new("twoStep", "The two-step, the kick on 1 and the 3-and", FigurePlayer.Ground, [0, 10], 16, null, 0, false),
        // the bossa nova's surdo, on 1, the 2-and, 3 and the 4-and
        new("surdo", "The surdo, on 1, the 2-and, 3 and the 4-and", FigurePlayer.Ground, [0, 6, 8, 14], 16, null, 0, false),
        // the snare on every beat, Motown's, and a power metal band's over its double kick
        new("everyBeat", "The snare on every beat", FigurePlayer.Backbeat, [0, 4, 8, 12], 16, null, 1, false),
        // the country train beat, the snare in even 8ths
        new("trainBeat", "The train beat, the snare in even 8ths", FigurePlayer.Backbeat, [0, 2, 4, 6, 8, 10, 12, 14], 16, null, 0, false),
        new("breakbeat", "A breakbeat, with the snare broken around 2 and 4", FigurePlayer.Ground, [0, 2, 10, 11], 16, null, 0, false),
        new("breakbeat", "A breakbeat, with the kick broken around 1 and 3", FigurePlayer.Backbeat, [4, 7, 9, 12, 15], 16, null, 0, false),
        new("gallop", "A gallop, an 8th and two 16ths a beat", FigurePlayer.Riff, [0, 2, 3, 4, 6, 7, 8, 10, 11, 12, 14, 15], 16, null, 0, false),
        new("reverseGallop", "A reverse gallop, two 16ths and an 8th a beat", FigurePlayer.Riff, [0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14], 16, null, 0, false),
        new("tremolo", "Tremolo-picked, every 16th", FigurePlayer.Riff, [..Enumerable.Range(0, 16)], 16, null, 0, false),
        new("arpeggio16", "Every 16th, an arpeggio's steady run or stabs", FigurePlayer.Chords, [..Enumerable.Range(0, 16)], 16, null, 1, false),
        // a ska's or a reggae's chop on the 8ths' offbeats, and a swing band's Charleston comp, on 1 and the 2-and
        new("skank", "The skank, on the 8ths' offbeats", FigurePlayer.Chords, [2, 6, 10, 14], 16, null, 0, false),
        new("charleston", "The Charleston, on 1 and the 2-and", FigurePlayer.Chords, [0, 6], 16, null, 0, false),
        // the comp in clave, a Latin pianist's or guitarist's on the son clave and the bossa nova guitar's batida on its clave
        new("withClave", "With the clave, wherever it strikes", FigurePlayer.Chords, [], 0, FigurePlayer.Claves, 0, false),
        // the boom-chicka's chick, the chords on 2 and 4 over the bass's boom on 1 and 3
        new("backbeat", "The chick, on 2 and 4", FigurePlayer.Chords, [4, 12], 16, null, 0, false),
        // the waltz, the bass's oom on 1 and the chords' pah-pah on 2 and 3, drawn by the chords and played by both
        new("waltz", "The waltz, the pah-pah on 2 and 3", FigurePlayer.Chords, [4, 8], 12, null, 0, false, Meter.ThreeFour, Exact: true),
        // a jig's or a slip's chords, the 8ths between the bass's 1 and 4, a folk accompanist's, loose as the chick is
        new("sixEight", "6/8's chick, the 8ths between 1 and 4", FigurePlayer.Chords, [2, 4, 8, 10], 12, null, 0, false, Meter.SixEight),
        new("rolling", "Rolling, the three 16ths after every beat's kick", FigurePlayer.Bass, [1, 2, 3, 5, 6, 7, 9, 10, 11, 13, 14, 15], 16, null, 1, true),
        new("offbeat", "On the offbeats, the 8th between the kicks", FigurePlayer.Bass, [2, 6, 10, 14], 16, null, 0, true),
        // a pulse, the root hammered in even 8ths or 16ths, a disco's, a synthwave's, a darksynth's
        new("halves", "The boom, on 1 and 3", FigurePlayer.Bass, [0, 8], 16, null, 0, false),
        new("waltz", "The waltz, the oom on 1", FigurePlayer.Bass, [0], 12, null, 0, false, Meter.ThreeFour, Exact: true),
        new("eighths", "A pulse in even 8ths", FigurePlayer.Bass, [0, 2, 4, 6, 8, 10, 12, 14], 16, null, 0, false),
        new("sixteenths", "A pulse in even 16ths", FigurePlayer.Bass, [..Enumerable.Range(0, 16)], 16, null, 0, false),
        new("withKick", "With the kick, wherever it plays", FigurePlayer.Bass, [], 0, FigurePlayer.Ground, 0, false),
        // a salsa's bass, counted in half notes as its clave is: each of its bars' 2-and and 4, anticipating the chord
        new("tumbao", "The tumbao, a salsa bar's 2-and and 4, ahead of the chord", FigurePlayer.Bass, [3, 6, 11, 14], 16, null, 0, false),
        new("bossaBass", "The bossa nova's bass, on 1, the 2-and, 3 and the 4-and, as its surdo", FigurePlayer.Bass, [0, 6, 8, 14], 16, null, 0, false),
        // the tresillo, 3+3+2 in each half of the bar, a jazz-funk vamp's bass arpeggio
        new("tresillo", "The tresillo, 3+3+2 in each half bar", FigurePlayer.Bass, [0, 3, 6, 8, 11, 14], 16, null, 0, false),
        // a son clave counted in half notes, a salsa's beat, its two sides in a bar
        // a jazz drummer's comping on the snare, soft strokes on the 2-and, the 3-and and the 4-and, as loose as the drummer
        // talks with the soloist
        new("jazzComp", "Comping on the 2-and, the 3-and and the 4-and", FigurePlayer.Comp, [6, 10, 14], 16, null, 0, false),
        new("clave32", "The clave's 3-2", FigurePlayer.Claves, [0, 3, 6, 10, 12], 16, null, 0, false, Exact: true),
        new("clave23", "The clave's 2-3", FigurePlayer.Claves, [2, 4, 8, 11, 14], 16, null, 0, false, Exact: true),
        // the 6/8 bell, a 12/8 bell over two bars of 6/8, an Afro-Cuban 6/8's, its 8ths 1, 3, 5, 6, 8, 10 and 12
        new("bell68", "The 6/8 bell, over two bars", FigurePlayer.Claves, [0, 4, 8, 10, 14, 18, 22], 24, null, 0, false, Meter.SixEight, Exact: true),
        new("bossaClave", "The bossa nova's clave, the 3-2 with its last stroke an 8th later", FigurePlayer.Backbeat, [0, 6, 12, 20, 26], 32, null, 0, false, Exact: true)
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
        FigurePlayer.Comp => "Snare's comping",
        _ => "Claves' figure"
    };

    /// <summary>A player's figure of the name, none for its own rhythm or one it does not play.</summary>
    internal static Figure? Of(FigurePlayer player, string name) => All.FirstOrDefault(x => x.Player == player && x.Name == name);

    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var players = Enum.GetValues<FigurePlayer>();
        // every section's figure of each player, none for its own rhythm, each player drawn in turn
        var drawn = song.SectionIds.Distinct().ToDictionary(id => id, id =>
        {
            var section = song.SectionOf(id);
            var context = song.Stream(SongStream.Figures, id);
            // a figure of its own shape in a bar of its meter alone, a steady one in any
            var meter = song.SectionMeters[id];
            var energy = section.EnergyTilt;
            var figures = new Dictionary<FigurePlayer, Figure?>();
            foreach (var player in players)
            {
                // the player's own rhythm where a genre's pool keeps no figure a bar not in four plays
                Weighted<string>[] options = [..ByConvention.Weigh(OptionsOf(player).Select(x => (x.Name, x.Weight)), section.Facets[Facet.Feel],
                    song.Genre.Pool<string>(GenreChoices.Figures[player], x => x)).Where(x => Of(player, x.Value)?.Fits(meter) ?? true)];
                if (options.Length == 0)
                    options = [new Weighted<string>(1, Own)];
                var name = context.Pick(energy.Weigh(options, x => Of(player, x)?.EnergyLean ?? 0));
                // a figure an earlier player drew that this one plays too, a blast's snare with its kick, drawn all the same
                // so that the draws are as many
                var shared = figures.Values.FirstOrDefault(x => x is not null && Of(player, x.Name) is not null);
                figures[player] = shared is not null && shared.Fits(meter) ? Of(player, shared.Name) : Of(player, name);
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
        // and a loop's bass and riff the figures of the section whose lines it plays, drawn all the same
        foreach (var id in drawn.Keys.Where(x => song.SectionOf(x).LinesFrom is not null))
            foreach (var player in new[] { FigurePlayer.Bass, FigurePlayer.Riff })
                drawn[id][player] = drawn[song.SectionOf(id).LinesFrom!.Value][player];
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
        foreach (var span in song.Form.Map.Plays.Where(x => !x.IsEnding).Select(x => x.Span))
            barsBefore[span] = barsBefore.Count == 0 ? 0 : barsBefore.Last().Value + barsBefore.Last().Key.BarCount;

        int? TrackOf(TrackRole role) => song.Playing.Where(x => x.Value == role).Select(x => (int?)x.Key).FirstOrDefault(x => draft.Timeline.TrackTimelineMap.ContainsKey(x!.Value));
        var all = draft.Timeline.TrackTimelineMap;
        var tracks = new Dictionary<FigurePlayer, int[]>
        {
            [FigurePlayer.Ground] = [..DrumGroups.Kick.Tracks.Where(all.ContainsKey)],
            [FigurePlayer.Backbeat] = [..DrumGroups.Snare.Tracks.Where(all.ContainsKey)],
            [FigurePlayer.Comp] = [..DrumGroups.Snare.Tracks.Where(all.ContainsKey)],
            [FigurePlayer.Time] = [..DrumGroups.Timekeepers.Tracks.Where(all.ContainsKey)],
            [FigurePlayer.Riff] = TrackOf(TrackRole.Riff) is { } own ? [own] : [],
            [FigurePlayer.Claves] = [..new[] { DrumGroups.GetTrackNumber(DrumDefinitions.Claves) }.Where(all.ContainsKey)],
            [FigurePlayer.Chords] = TrackOf(TrackRole.Chords) is { } chords ? [chords] : [],
            [FigurePlayer.Bass] = TrackOf(TrackRole.Bass) is { } bass ? [bass] : []
        };

        // what a figure that follows a player strikes with in a section: its tracks, the riff's the bass's where the song has no
        // riff, the claves' the snare's where the claves play no figure there and the snare plays the bossa nova's clave
        int[] Followed(FigurePlayer player, SectionSpan span) => player switch
        {
            FigurePlayer.Riff when tracks[player].Length == 0 && TrackOf(TrackRole.Bass) is { } bass => [bass],
            FigurePlayer.Claves when (tracks[player].Length == 0 || drawn[span.SectionId][player] is null) && drawn[span.SectionId][FigurePlayer.Backbeat]?.Name == "bossaClave"
                => tracks[FigurePlayer.Backbeat],
            FigurePlayer.Claves when drawn[span.SectionId][player] is null => [],
            _ => tracks[player]
        };

        // the parts the form stops, a stop ending's silence, which no figure strikes into
        var holds = draft.Holds(song);
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
                    // the sections as the song plays them, an intro's window and a hook intro as their sources
                    foreach (var span in song.Form.Map.Plays.Where(x => !x.IsEnding).Select(x => x.Span))
                    {
                        if (drawn[span.SectionId][player] is not { } figure)
                            continue;
                        // the snare comping only where it colours the groove, never where it leads the backbeat
                        if (player == FigurePlayer.Comp && song.SectionOf(span.SectionId).DrumRoles.GetValueOrDefault(track) != DrumRole.Colour)
                            continue;
                        // one that follows where the song plays what it follows, the riff's where it has a riff or a bass
                        if (figure.Follows is { } followed && Followed(followed, span).Length == 0)
                            continue;
                        // every bar, a short last one's figure cut at the section's end
                        for (var index = 0; index < span.BarCount; index++)
                        {
                            var (bar, end) = span.Bar(index);
                            bool In(double position) => position >= bar - 1e-9 && position < end - 1e-9;
                            var own = played.Where(x => In(x.Position)).ToArray();
                            double[] onsets = figure.Follows is { } follows
                                ? [..Followed(follows, span).SelectMany(t => timelines[t].EventTimeline).Where(x => In(x.Position)).Select(x => x.Position).Distinct().Order()]
                                : figure.Step > 0
                                    ? [..figure.StepsIn(span.Meter).Select(x => bar + x)]
                                    : [..figure.Onsets.Select(x => x - (barsBefore[span] + index) % (figure.Sixteenths / span.Meter.Sixteenths) * span.Meter.Sixteenths)
                                        .Where(x => x >= 0 && x < span.Meter.Sixteenths).Select(x => bar + x * 0.25)];
                            onsets = [..onsets.Where(x => !holds.At(Hold.Cut, x, track))];
                            var held = Drums.Contains(player) && holds.Meets(KeepsOutOf, bar, end, track);
                            // in a bar both play, each struck as the player's own note at or before it was; one around the kick
                            // where the kick plays, the player's own rhythm where it rests; and no drum's of fixed onsets in half
                            // or double time, whose figure is the drum's own rhythm stretched or pressed, as the time feel
                            // stretches the drums alone, but one that follows, whose notes are as what it follows plays them
                            // there, a breakdown's kick on its chugs, and the kick's steady step, a double kick's 16ths or a
                            // blast's 8ths, no groove the time feel stretches, under the backbeat it halves or doubles
                            // and a drum's never over a drop's build roll, which the form wrote into the bar
                            if (own.Length == 0 || onsets.Length == 0 || held
                                || song.SectionOf(span.SectionId).TimeFeel != 0 && figure.Follows is null && Drums.Contains(player)
                                && !(player == FigurePlayer.Ground && figure.Step > 0)
                                || figure.AnswersKick && !tracks[FigurePlayer.Ground].Any(t => timelines[t].EventTimeline.Any(x => In(x.Position))))
                                continue;
                            laid.Add((span.SectionId, player));
                            played.RemoveAll(x => In(x.Position));
                            // a loose figure's onsets and the player's own notes each kept by a chance, from a sequence of the
                            // bar's, by its place in its section, and the track's own
                            double[] kept = figure.Loose
                                ? Loosen([..own.Select(x => x.Position)], [..onsets.Where(In)], Strictness.At(song.SectionOf(span.SectionId).Facets[Facet.Feel]),
                                    song.Stream(SongStream.LooseFigures, song.Form.Map.PlaceKey(bar), track))
                                : [..onsets.Where(In)];
                            played.AddRange(kept.Select(at => own.Where(x => x.Position <= at + 1e-9).DefaultIfEmpty(own[0]).Last().Value.ToTimelineItem(at)));
                        }
                    }

                    return EventTimeline.Create(notes.Duration, played.OrderBy(x => x.Position));
                };
            draft = draft with { Timeline = draft.Timeline.MapTrackEvents(edits) };
        }

        return Laid(song, draft, drawn, laid);
    }

    /// <summary>
    ///     A loose figure's onsets in a bar as the section plays them, by how strictly: a place both the figure and the
    ///     player's own rhythm strike always, one the figure's alone by the strictness, one the player's own alone by its
    ///     rest, so that at 1 the figure is played exactly.
    /// </summary>
    internal static double[] Loosen(double[] own, double[] onsets, double strict, IGenerationContext context)
    {
        var ownAt = own.Select(x => Math.Round(x, 6)).ToHashSet();
        var onAt = onsets.Select(x => Math.Round(x, 6)).ToHashSet();
        return [..ownAt.Union(onAt).Order().Where(at => context.GenerateDouble() < (ownAt.Contains(at) && onAt.Contains(at) ? 1 : onAt.Contains(at) ? strict : 1 - strict))];
    }

    /// <summary>The drums' players, whose rhythm a half or double time stretches or presses.</summary>
    private static readonly ImmutableHashSet<FigurePlayer> Drums = [FigurePlayer.Ground, FigurePlayer.Backbeat, FigurePlayer.Time, FigurePlayer.Claves, FigurePlayer.Comp];

    /// <summary>The song with the figures laid traced and handed on, and a heavy figure's backbeat under them.</summary>
    private static SongDraft Laid(SongDecisions song, SongDraft draft, Dictionary<int, Dictionary<FigurePlayer, Figure?>> drawn, HashSet<(int Section, FigurePlayer Player)> laid)
    {
        var figures = drawn.ToImmutableDictionary(x => x.Key, x => x.Value.Where(f => f.Value is not null && laid.Contains((x.Key, f.Key))).ToImmutableDictionary(f => f.Key, f => f.Value!.Name));
        foreach (var (id, played) in figures)
            StateTrace.RecordSection(TracePoints.Figures, SectionGenerator.SectionTrace, id, string.Join(", ", played.Select(x => $"{x.Key} {x.Value}")), played);
        return HeavyBackbeat(song, draft with { Figures = figures });
    }

    /// <summary>
    ///     A heavy figure's backbeat: the snare under it struck on its head, never its cross-stick, as a thrash or a
    ///     breakdown's drummer hits it; and from a section's second phrase on, a bar the snare would leave empty under it
    ///     given its backbeat, the meter's (2 and 4 in four), so that a double kick runs a phrase at most without one,
    ///     as a drummer brings the snare in, where the section's kit plays none too; only in a bar the other drums play,
    ///     never where the drums rest, as while a part opens the song alone or the drums come in late.
    /// </summary>
    private static SongDraft HeavyBackbeat(SongDecisions song, SongDraft draft)
    {
        var map = song.Form.Map;
        var spans = map.Plays.Where(x => !x.IsEnding).Select(x => x.Span).Where(x => draft.Figures.GetValueOrDefault(x.SectionId)?.GetValueOrDefault(FigurePlayer.Ground) is { } ground && Of(FigurePlayer.Ground, ground)?.Heavy == true).ToArray();
        var snares = DrumGroups.Snare.Tracks.Where(draft.Timeline.TrackTimelineMap.ContainsKey).ToArray();
        if (spans.Length == 0 || snares.Length == 0)
            return draft;
        // the other drums, which play where the drums do
        var others = draft.Timeline.TrackTimelineMap.Keys.Where(x => song.Tracks.Definitions[x].Role == TrackRole.Drum && !snares.Contains(x)).ToArray();
        bool DrumsPlay(double start, double end) => others.Any(t => draft.Timeline.TrackTimelineMap[t].EventTimeline.Any(x => x.IsWithin(start, end)));
        // and never in a part the form stops
        var holds = draft.Holds(song);

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
                        if (played.Any(x => x.IsWithin(start, end)) || !DrumsPlay(start, end))
                            continue;
                        played.AddRange(span.Meter.BackbeatsIn(song.SectionOf(span.SectionId).TimeFeel).Select(x => start + x)
                            .Where(x => x < end - 1e-9 && !holds.At(Hold.Cut, x, track)).Select(x => own.MergeWith(head).ToTimelineItem(x)));
                    }
                }

                return EventTimeline.Create(notes.Duration, played.OrderBy(x => x.Position));
            })))
        };
    }
}
