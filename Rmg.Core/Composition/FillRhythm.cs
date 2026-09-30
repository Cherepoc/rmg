using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     The rhythm a fill plays: the groove's state, the snare's in the bar before the line, with the fill's layer added
///     and resolved as a bar pattern's is, so that the fill keeps the groove's cycle and phase, falls on its grid, and
///     its strongest notes fall where the groove's accents are. The layer makes it finer and fuller; its
///     steps fold back into range as a bar pattern's do, so a fill over a groove at its finest turns sparser, and one
///     that steps its rank offset puts its weight on weaker notes. The finest rank is as fine as the shortest note the
///     song's tempo allows, where a bar pattern stops at <see cref="ResolvedRhythm.MaxRankLimit" />.
/// </summary>
/// <param name="Rhythm">The resolved settings.</param>
/// <param name="RankLimit">The finest rank the tempo allows, which a change of speed folds into.</param>
/// <param name="Meter">The meter of the bar that ends at the line the fill leads into.</param>
internal sealed record FillRhythm(ResolvedRhythm Rhythm, int RankLimit, Meter Meter)
{
    /// <summary>The layer of a plain fill: finer, fuller by a section change's share, its cycles repeating.</summary>
    public static StateMap PlainLayer { get; } = StateMap.FromStates(
        [
            CompositionStateKinds.Rhythm.MaxRank.CreateState(FillLayers.FinerRanks),
            CompositionStateKinds.Rhythm.Fullness.CreateState(FillLayers.Fullness),
            CompositionStateKinds.Rhythm.Variation.CreateState(-1)
        ]
    );

    /// <summary>The cycle, in beats.</summary>
    public double Period => Rhythm.Period;

    /// <summary>Where the cycle's strongest note falls in it, in beats.</summary>
    public double Phase => Rhythm.Phase;

    /// <summary>How many times the cycle is halved, down to the fill's finest notes.</summary>
    public int MaxRank => Rhythm.MaxRank;

    /// <summary>The tuplet the fill's notes fall on, 1 for straight.</summary>
    public int Tuplet => Rhythm.PrimeIndex.ToTuplet();

    /// <summary>The fill's finest notes, in beats.</summary>
    public double Fine => Period / Math.Pow(2, MaxRank);

    /// <summary>The notes a rank coarser, where the halves of a fill that changes speed meet; the finest where there are none.</summary>
    public double Coarse => MaxRank > 0 ? 2 * Fine : Fine;

    /// <summary>
    ///     The note of the cycle's halvings nearest an 8th, finer or coarser than the fill's, by which a fill is moved off
    ///     the beat or a landing is pushed early.
    /// </summary>
    public double Push => Period / Math.Pow(2, Math.Max(0, Math.Round(Math.Log2(Period / 0.5))));

    /// <param name="groove">The state of the groove's rhythm.</param>
    /// <param name="layer">The fill's layer over it.</param>
    /// <param name="minNote">The shortest note the fill may play, in beats.</param>
    /// <param name="meter">The meter the song's bars are in.</param>
    public static FillRhythm Of(StateMap groove, StateMap layer, double minNote, Meter meter)
    {
        var rhythm = ResolvedRhythm.Of(groove.MergeWith(layer), minNote);
        // no finer than the tempo allows, nor, for a grouped cycle, than the grid
        var noteLimit = Math.Max(0, (int)Math.Floor(Math.Log2(rhythm.Period / minNote) + 1e-9));
        return new FillRhythm(rhythm, ResolvedRhythm.IsGrouped(rhythm.Period) ? Math.Min(noteLimit, ResolvedRhythm.GridRankLimit(rhythm.Period)) : noteLimit, meter);
    }

    /// <summary>Whether the fill's finest notes have one at a position, in the bar that ends at the line.</summary>
    public bool Has(double position, double line)
    {
        var steps = (position - (line - Meter.BarDuration) - Phase) / Fine;
        return Math.Abs(steps - Math.Round(steps)) < 1e-6;
    }

    /// <summary>A rank a step finer or coarser than the fill's, folded into range as the fill's own is.</summary>
    public int Step(int step)
    {
        return (MaxRank + step).BounceInBounds(0, RankLimit);
    }

    /// <summary>
    ///     The notes of the fill from one position up to another, each with its rank: the fill's dyadic pattern over the
    ///     bar that ends at the line, which keeps a note by its rank's distance from the rank offset, as a bar pattern
    ///     does. A sparse fill over a short span may keep none, and leave its drums silent there.
    /// </summary>
    /// <param name="maxRank">The rank of the finest notes.</param>
    public ImmutableArray<(double Position, int Rank, int MaxRank)> Play(
        IGenerationContext context,
        int seed,
        double line,
        double from,
        double to,
        int maxRank
    )
    {
        var barStart = line - Meter.BarDuration;
        var pattern = DyadicRankThresholdPattern.Create(
            context,
            seed,
            WeightUtil.CreateGeometricRankWeightFunc(Math.Min(Rhythm.RankOffset, maxRank), 0, 1, Rhythm.Fullness),
            new DyadicTimelineDescriptor(Meter.BarDuration, Meter.GetCycles(Period, Phase), maxRank),
            Rhythm.Variation
        );
        return
        [
            ..pattern.OutcomeRankTimeline
                .Where(x => barStart + x.Position >= from - 1e-9 && barStart + x.Position < to - 1e-9)
                .Select(x => (barStart + x.Position, x.Value, pattern.MaxRank))
        ];
    }
}
