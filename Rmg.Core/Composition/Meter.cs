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
    ///     A bar's cycles of a rhythm of the period and phase given, both in beats counted in the reference bar: a straight
    ///     period, a power of two of 16ths, plays on the nodes of the level whose nodes are most often nearest its length,
    ///     the coarser where two are as near, each node a cycle split as the node splits, so that the same settings play
    ///     as busy in every meter; one of two bars or more over two bars, of which the bar plays the first; a tuplet's or
    ///     a grouped period's cycles run on from the bar's start, starting again where two of them fit, as they always did.
    ///     In four the levels are the halvings of the bar, so a straight period's cycles are its nodes, as they were.
    /// </summary>
    public ImmutableArray<RhythmCycle> GetCycles(double period, double phase)
    {
        var steps = period * 4;
        var isStraight = Math.Abs(steps - Math.Round(steps)) < 1e-9 && Math.Round(steps) >= 1
                         && Math.Abs(Math.Log2(Math.Round(steps)) - Math.Round(Math.Log2(Math.Round(steps)))) < 1e-9;
        if (!isStraight)
            return DyadicRankTimeline.GenerateCycles(BarDuration, phase, period, ResolvedRhythm.RestartOf(period, BarDuration), ResolvedRhythm.SplitOf(period));

        var fraction = phase / period;
        if (Math.Abs(Math.Log2(steps / (2.0 * Sixteenths))) < Math.Abs(Math.Log2(steps / Sixteenths)))
        {
            // two bars' cycle, the bar its first half
            var twoBars = 2 * BarDuration;
            return [new RhythmCycle(0, twoBars, BarDuration, ResolvedRhythm.SplitOf(twoBars), fraction, RankLimitOf(2 * Sixteenths))];
        }

        var level = Levels
            .Select((nodes, index) => (Nodes: nodes, Index: index, Length: nodes.GroupBy(x => x.Length).OrderByDescending(x => x.Count()).ThenByDescending(x => x.Key).First().Key))
            .MinBy(x => (Math.Round(Math.Abs(Math.Log2(x.Length / steps)), 9), x.Index))
            .Nodes;
        return
        [
            ..level.Select(node => new RhythmCycle(node.Start / 4.0, node.Length / 4.0, (node.Start + node.Length) / 4.0, ResolvedRhythm.SplitOf(node.Length / 4.0), fraction, RankLimitOf(node.Length)))
        ];
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
