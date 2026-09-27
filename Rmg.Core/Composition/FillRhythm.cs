using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     The rhythm a fill plays: the groove's, the snare's in the bar before the line, with its cycle and phase, so that
///     the fill falls on the groove's grid and its strongest notes where the groove's accents are, only finer by a rank
///     or two, as the fill draws, and no finer than the fastest note the song's tempo allows.
/// </summary>
/// <param name="Period">The cycle, in beats.</param>
/// <param name="Phase">Where the cycle's strongest note falls in it, in beats.</param>
/// <param name="MaxRank">How many times the cycle is halved, down to the fill's finest notes.</param>
internal sealed record FillRhythm(double Period, double Phase, int MaxRank)
{
    /// <summary>The fill's finest notes, in beats.</summary>
    public double Fine => Period / Math.Pow(2, MaxRank);

    /// <summary>The notes a rank coarser, which a fill that speeds up starts on; the finest where there are none.</summary>
    public double Coarse => MaxRank > 0 ? 2 * Fine : Fine;

    /// <summary>
    ///     The note of the cycle's halvings nearest an 8th, finer or coarser than the fill's, by which a fill is moved off
    ///     the beat or a landing is pushed early.
    /// </summary>
    public double Push => Period / Math.Pow(2, Math.Max(0, Math.Round(Math.Log2(Period / 0.5))));

    /// <summary>The rank of a note of the fill's finest notes, by where it falls in the cycle.</summary>
    public int RankAt(double position, double line, int maxRank)
    {
        var offset = position - (line - Meter.BarDuration) - Phase;
        for (var rank = 0; rank < maxRank; rank++)
        {
            var steps = offset / (Period / Math.Pow(2, rank));
            if (Math.Abs(steps - Math.Round(steps)) < 1e-6)
                return rank;
        }

        return maxRank;
    }

    /// <param name="groove">The groove's rhythm.</param>
    /// <param name="extraRanks">How many ranks finer than the groove the fill plays.</param>
    /// <param name="tuplet">The tuplet the fill plays, 1 for straight; a groove of its own keeps its own.</param>
    /// <param name="minNote">The shortest note the fill may play, in beats.</param>
    public static FillRhythm Of(ResolvedRhythm groove, int extraRanks, int tuplet, double minNote)
    {
        var period = groove.Period;
        // a straight groove plays the tuplet by dividing its cycle by it, to the nearest power of two
        if (groove.PrimeIndex.ToTuplet() == 1 && tuplet != 1)
            period *= Math.Pow(2, Math.Round(Math.Log2(tuplet))) / tuplet;
        var maxRank = Math.Max(0, (int)Math.Floor(Math.Log2(period / minNote) + 1e-9));
        return new FillRhythm(period, groove.Phase, Math.Min(groove.MaxRank + extraRanks, maxRank));
    }

    /// <summary>
    ///     The notes of the fill from one position up to another, each with its rank: the fill's dyadic pattern over the
    ///     bar that ends at the line, whose cycles repeat, and which keeps a note of each rank by its chance.
    ///     Where the fill's first note is its own, it always plays, on the finest notes, so that a run starts on a hit.
    /// </summary>
    /// <param name="maxRank">The rank of the finest notes, at most the fill's.</param>
    public ImmutableArray<(double Position, int Rank, int MaxRank)> Play(
        IGenerationContext context,
        int seed,
        Func<int, double> keepChance,
        double line,
        double from,
        double to,
        int maxRank,
        bool startsOnAHit = false
    )
    {
        var barStart = line - Meter.BarDuration;
        var pattern = DyadicRankThresholdPattern.Create(
            context,
            seed,
            keepChance,
            new DyadicTimelineDescriptor(Meter.BarDuration, Period, Phase, maxRank),
            0
        );
        var notes = pattern.OutcomeRankTimeline
            .Where(x => barStart + x.Position >= from - 1e-9 && barStart + x.Position < to - 1e-9)
            .Select(x => (barStart + x.Position, x.Value, pattern.MaxRank))
            .ToList();
        // the first note on the finest notes, which a span that falls off them has none of
        var note = Period / Math.Pow(2, maxRank);
        var first = barStart + Phase + Math.Ceiling((from - barStart - Phase) / note - 1e-9) * note;
        if (startsOnAHit && first < to - 1e-9 && (notes.Count == 0 || notes[0].Item1 > first + 1e-9))
            notes.Insert(0, (first, RankAt(first, line, maxRank), maxRank));
        return [..notes];
    }
}
