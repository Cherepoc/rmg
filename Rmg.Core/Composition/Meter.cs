using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     The meter a song is in: a bar as its groups of 16ths, from which its hierarchy follows, the bar splitting into
///     its groups and every group into its beats and on down to the 16ths; 4/4 as two halves of two beats. A section is
///     made of 4-bar patterns in every meter, one bar for every chord of a progression of a chord a bar.
/// </summary>
/// <param name="Groups">The bar's groups, each in 16ths, in order.</param>
public sealed record Meter(ImmutableArray<int> Groups)
{
    /// <summary>Four beats, as two halves of two.</summary>
    public static Meter FourFour { get; } = new([8, 8]);

    /// <summary>Three beats.</summary>
    public static Meter ThreeFour { get; } = new([4, 4, 4]);

    /// <summary>Two dotted quarters, each of three 8ths.</summary>
    public static Meter SixEight { get; } = new([6, 6]);

    /// <summary>
    ///     The meters a song is in, each in its usual grouping, and how often, by the feel facet of its unconventionality:
    ///     the plainest in four; at the middle mostly four, 3/4 and 6/8 now and then and odd
    ///     meters, 5/4, 7/8, 15/16 and the like, rarely; the wildest in any odd meter as likely, 3/4 and 6/8 less often,
    ///     never in four.
    /// </summary>
    public static ImmutableArray<(Meter Meter, ByConvention Weight)> Options { get; } =
    [
        (FourFour, new ByConvention(1, 0.92, 0)),
        (ThreeFour, new ByConvention(0, 0.04, 0.3)),
        (SixEight, new ByConvention(0, 0.04, 0.3)),
        (new Meter([8, 12]), new ByConvention(0, 0.006, 1)),
        (new Meter([4, 4, 6]), new ByConvention(0, 0.006, 1)),
        (new Meter([4, 6]), new ByConvention(0, 0.003, 1)),
        (new Meter([4, 4, 4, 6]), new ByConvention(0, 0.003, 1)),
        (new Meter([8, 8, 12]), new ByConvention(0, 0.002, 1)),
        (new Meter([4, 6, 6, 6]), new ByConvention(0, 0.002, 1)),
        (new Meter([4, 3, 3, 3]), new ByConvention(0, 0.002, 1)),
        (new Meter([4, 4, 4, 3]), new ByConvention(0, 0.002, 1))
    ];

    /// <summary>
    ///     A song's meter, by the feel facet of its unconventionality, its groups in an order drawn among theirs, as 7/8
    ///     plays 2+2+3, 3+2+2 or 2+3+2.
    /// </summary>
    /// <param name="given">A meter given in place of the one drawn, by its place in <see cref="Options" />, drawn all the same, its groups' order still drawn.</param>
    public static Meter Draw(IGenerationContext context, double unconventionality, int? given)
    {
        var drawn = context.Pick(ByConvention.Weigh(Options, unconventionality));
        var meter = given is { } option ? Options[option].Meter : drawn;
        var orders = Orders(meter.Groups).ToArray();
        return new Meter(orders[(int)(context.GenerateDouble() * orders.Length)]);
    }

    /// <summary>The place in <see cref="Options" /> of a meter, whatever the order of its groups.</summary>
    public static int OptionOf(Meter meter)
    {
        var groups = meter.Groups.Order().ToArray();
        return Options.Select((x, i) => (x.Meter, i)).Single(x => x.Meter.Groups.Order().SequenceEqual(groups)).i;
    }

    /// <summary>Every distinct order of the groups.</summary>
    private static IEnumerable<ImmutableArray<int>> Orders(ImmutableArray<int> groups)
    {
        if (groups.Length <= 1)
        {
            yield return groups;
            yield break;
        }

        foreach (var first in groups.Distinct())
        foreach (var rest in Orders(groups.Remove(first)))
            yield return [first, ..rest];
    }

    /// <summary>How many bars a section's pattern has.</summary>
    public const int PatternBarCount = Progressions.BarCount;

    /// <summary>The tempo, in beats a minute, that a song's tempo state is a multiple of; a beat is a quarter note.</summary>
    public const double BaseTempo = 120;

    /// <summary>
    ///     The bar a rhythm's period and phase are counted in, in beats: 4/4's, whatever the song's meter, so that a
    ///     setting means a length, which the song's meter plays at the level of its own nearest it.
    /// </summary>
    public const double ReferenceBar = 4;

    /// <summary>How many 16ths a bar has.</summary>
    public int Sixteenths => Groups.Sum();

    /// <summary>How long a bar is, in beats.</summary>
    public double BarDuration => Sixteenths / 4.0;

    /// <summary>How long a section's pattern is, in beats.</summary>
    public double PatternDuration => PatternBarCount * BarDuration;

    /// <summary>
    ///     The time signature a score writes the bar in: in quarters where every group is whole beats, as 4/4 and 3/4, in
    ///     8ths where every group is whole 8ths, as 6/8 and 7/8, and in 16ths otherwise, as 15/16 of a group of three.
    /// </summary>
    public (int Numerator, int Denominator) TimeSignature =>
        Groups.All(x => x % 4 == 0) ? (Sixteenths / 4, 4) : Groups.All(x => x % 2 == 0) ? (Sixteenths / 2, 8) : (Sixteenths, 16);

    /// <summary>
    ///     The meter's hierarchy, level by level, each level its nodes in 16ths, from the bar down to single 16ths: the
    ///     bar, its groups, and every node split into the odd number of its steps first and in two after, as a grouped
    ///     cycle splits (<see cref="ResolvedRhythm.SplitOf" />); a node of one 16th stays one on the levels below.
    /// </summary>
    public ImmutableArray<ImmutableArray<(int Start, int Length)>> Levels => _levels ??= BuildLevels();

    private ImmutableArray<ImmutableArray<(int Start, int Length)>>? _levels;

    private ImmutableArray<ImmutableArray<(int Start, int Length)>> BuildLevels()
    {
        var levels = new List<ImmutableArray<(int Start, int Length)>> { ImmutableArray.Create((0, Sixteenths)) };
        var groups = Groups.Length > 1 ? Groups.Select((x, i) => (Groups.Take(i).Sum(), x)).ToImmutableArray() : Split(levels[0][0]);
        levels.Add(groups);
        while (levels[^1].Any(x => x.Length > 1))
            levels.Add([..levels[^1].SelectMany(x => x.Length > 1 ? Split(x) : ImmutableArray.Create(x))]);
        return [..levels];

        static ImmutableArray<(int Start, int Length)> Split((int Start, int Length) node)
        {
            var parts = ResolvedRhythm.SplitOf(node.Length / 4.0);
            var length = node.Length / parts;
            return [..Enumerable.Range(0, parts).Select(x => (node.Start + x * length, length))];
        }
    }

    /// <summary>
    ///     The start of the bar's or one of its groups' nearest a place, in beats, the later where two are as near.
    /// </summary>
    public double NearestGroupStart(double position)
    {
        var bar = Math.Floor(position / BarDuration + 1e-9) * BarDuration;
        return Levels[1].Select(x => bar + x.Start / 4.0).Append(bar + BarDuration).MinBy(x => (Math.Round(Math.Abs(x - position), 9), -x));
    }

    /// <summary>
    ///     Whether every group of the bar holds whole pairs of notes, a pair as long as given in beats, as 6/8's pairs of
    ///     16ths fit and its 8ths' do not.
    /// </summary>
    public bool Pairs(double pair)
    {
        var steps = (int)Math.Round(pair * 4);
        return Groups.All(x => x % steps == 0);
    }

    /// <summary>
    ///     The level of the meter's pulse, which a rhythm's period is counted in: the level whose nodes are most often
    ///     nearest a beat long, the coarser where two are as near, 4/4's and 3/4's quarters, 6/8's dotted quarters and
    ///     15/16's groups.
    /// </summary>
    public int Tactus => _tactus ??= Enumerable.Range(0, Levels.Length).MinBy(x => (Math.Round(Math.Abs(Math.Log2(ModeLength(x) / 4.0)), 9), x));

    private int? _tactus;

    /// <summary>How long the meter's pulse most often is, in 16ths: 4/4's quarter, 6/8's dotted quarter.</summary>
    public int PulseSixteenths => ModeLength(Tactus);

    /// <summary>Where the bar's pulses start, in beats: 4/4's four beats, 6/8's two dotted quarters.</summary>
    public ImmutableArray<double> Pulses => [..Levels[Tactus].Select(x => x.Start / 4.0)];

    /// <summary>How long a level's nodes most often are, in 16ths, the longer where two lengths are as common.</summary>
    private int ModeLength(int level) =>
        Levels[level].GroupBy(x => x.Length).OrderByDescending(x => x.Count()).ThenByDescending(x => x.Key).First().Key;

    /// <summary>
    ///     A bar's cycles of a rhythm of the period and phase given, both in beats counted in the reference bar. A
    ///     straight period, a power of two of 16ths, is counted in the pulses it has in four: the reference bar plays on
    ///     the bar, two bars over two bars, of which the bar plays the first, and a shorter one on the nodes of the level
    ///     whose nodes are most often nearest as many of the meter's pulses, a finer level counted half as far again, so
    ///     that a half bar is 6/8's bar and 3/4's too; each node a cycle of its own descendants, its ranks the depths they first
    ///     start at. A phase of half a cycle strikes the node's other parts first, its first after, as 4/4's backbeat does
    ///     on 2 and 4, 3/4's on 2 and 3 and 6/8's on its fourth 8th; another shifts it. A tuplet plays its notes over the
    ///     nodes its span of them would play on as a straight period, three over a dotted quarter its 8ths, and a grouped
    ///     period runs on from the start of every node of the finest level whose nodes hold two of it, 6/8's dotted 8ths
    ///     3+3 in each of its groups; a tuplet whose span is no straight period of a bar or less runs on from the bar's
    ///     start. In four every cycle is as it was.
    /// </summary>
    public ImmutableArray<RhythmCycle> GetCycles(double period, double phase)
    {
        var steps = period * 4;
        var isStraight = IsPowerOfTwo(steps);
        var fraction = phase / period;
        if (ResolvedRhythm.IsGrouped(period))
        {
            // from every node that holds two of its cycles, the bar where none does
            var restart = Enumerable.Range(0, Levels.Length).LastOrDefault(x => Levels[x].Min(n => n.Length) >= 2 * steps - 1e-9);
            return
            [
                ..Levels[restart].SelectMany(node => DyadicRankTimeline.GenerateCycles(node.Length / 4.0, phase, period, node.Length / 4.0, ResolvedRhythm.SplitOf(period))
                    .Select(x => x with { Start = x.Start + node.Start / 4.0, End = x.End + node.Start / 4.0 }))
            ];
        }

        if (!isStraight)
        {
            // a tuplet over the nodes of its span, where its span is a straight period of a bar or less
            var tuplet = Enumerable.Range(1, 7).Select(x => 2 * x + 1).FirstOrDefault(x => IsPowerOfTwo(x * steps) && x * steps <= ReferenceBar * 4 + 1e-9);
            if (tuplet == 0)
                return DyadicRankTimeline.GenerateCycles(BarDuration, phase, period, BarDuration, ResolvedRhythm.SplitOf(period));

            return
            [
                ..Levels[LevelOf(tuplet * period)].SelectMany(node => Enumerable.Range(0, tuplet).Select(x =>
                    new RhythmCycle(node.Start / 4.0 + x * node.Length / 4.0 / tuplet, node.Length / 4.0 / tuplet, (node.Start + node.Length) / 4.0, 2, fraction)
                ))
            ];
        }

        if (period > ReferenceBar + 1e-9)
        {
            // two bars' cycle, the bar its first half
            var twoBars = 2 * BarDuration;
            return [new RhythmCycle(0, twoBars, BarDuration, ResolvedRhythm.SplitOf(twoBars), fraction, RankLimitOf(2 * Sixteenths))];
        }

        var level = LevelOf(period);
        var weakFirst = Math.Abs(fraction - 0.5) < 1e-9;
        return
        [
            ..Levels[level].Select(node => new RhythmCycle(
                node.Start / 4.0,
                node.Length / 4.0,
                (node.Start + node.Length) / 4.0,
                ResolvedRhythm.SplitOf(node.Length / 4.0),
                weakFirst ? 0 : fraction,
                RankLimitOf(node.Length),
                Template(level, node, weakFirst)
            ))
        ];
    }

    /// <summary>
    ///     How long a span before a bar line is in the meter, given in beats counted in the reference bar: the bar's last
    ///     node of the level the span plays on as a straight period, such as a fill of half a bar 6/8's whole bar and a
    ///     beat its last dotted quarter.
    /// </summary>
    public double SpanOf(double beats) => Levels[LevelOf(beats)][^1].Length / 4.0;

    /// <summary>
    ///     The level a straight period of the reference bar or less plays on: the bar for the reference bar, and for a
    ///     shorter one the level whose nodes are most often nearest as many of the meter's pulses as it has in four, a
    ///     finer level counted half as far again (<see cref="FinerLevelWeight" />), the coarser where two are as near.
    /// </summary>
    private int LevelOf(double period)
    {
        var pulses = period / (ReferenceBar / 4);
        var tactus = ModeLength(Tactus);
        return period > ReferenceBar - 1e-9
            ? 0
            : Enumerable.Range(0, Levels.Length).MinBy(x =>
            {
                var distance = Math.Log2(ModeLength(x) / (double)tactus / pulses);
                return (Math.Round(distance < 0 ? -distance * FinerLevelWeight : distance, 9), x);
            });
    }

    /// <summary>
    ///     How much farther a level of nodes shorter than a period counts than one of longer nodes, so that a period
    ///     between two plays on the coarser unless the finer is much nearer: 13/16's two pulses on its bar, not its groups
    ///     of three 16ths, where the backbeat would strike inside every group.
    /// </summary>
    private const double FinerLevelWeight = 1.5;

    private static bool IsPowerOfTwo(double value) =>
        Math.Abs(value - Math.Round(value)) < 1e-9 && Math.Round(value) >= 1 && Math.Abs(Math.Log2(Math.Round(value)) - Math.Round(Math.Log2(Math.Round(value)))) < 1e-9;

    /// <summary>
    ///     A node's cycle, as its positions in parts of its length, each with its rank: its start, then the starts of its
    ///     descendants level by level, each rank the depth they first start at, and past the 16ths every part halved; with
    ///     its other parts first, its first child's start a rank weaker than its others'.
    /// </summary>
    private ImmutableArray<(double Position, int Rank)> Template(int level, (int Start, int Length) node, bool weakFirst)
    {
        var ranks = new SortedDictionary<double, int> { [0] = 0 };
        for (var depth = 1; depth <= DyadicRankDistribution.MaxRank; depth++)
        {
            double[] starts = level + depth < Levels.Length
                ? [..Levels[level + depth].Where(x => x.Start >= node.Start && x.Start < node.Start + node.Length).Select(x => (x.Start - node.Start) / (double)node.Length)]
                : [];
            var fresh = starts.Where(x => !ranks.ContainsKey(x)).ToArray();
            // past the tree, every part halved
            if (fresh.Length == 0)
            {
                var bounds = ranks.Keys.Append(1.0).ToArray();
                fresh = [..bounds.Zip(bounds.Skip(1), (a, b) => (a + b) / 2)];
            }

            foreach (var position in fresh)
                ranks.TryAdd(position, depth);
        }

        return [..ranks.Select(x => (x.Key, weakFirst && x.Value <= 1 ? 1 - x.Value : x.Value))];
    }

    /// <summary>
    ///     The finest rank a node plays on the 16ths' grid: none for a node of a power of two of 16ths, which halves as
    ///     finely as a rhythm asks, and as far as the grid for any other (<see cref="ResolvedRhythm.GridRankLimit" />).
    /// </summary>
    private static int RankLimitOf(int sixteenths)
    {
        var isPowerOfTwo = (sixteenths & (sixteenths - 1)) == 0;
        return isPowerOfTwo ? int.MaxValue : ResolvedRhythm.GridRankLimit(sixteenths / 4.0);
    }

    public bool Equals(Meter? other) => other is not null && Groups.SequenceEqual(other.Groups);

    public override int GetHashCode() => Groups.Aggregate(17, (hash, group) => hash * 31 + group);

    public override string ToString() => $"{Sixteenths}/16 ({string.Join("+", Groups)})";
}
