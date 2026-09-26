using Rmg.Core.Events;

namespace Rmg.Core.Probabilities;

public sealed class DyadicRankThresholdPattern
{
    private DyadicRankThresholdPattern(int maxRank, EventTimeline<int> outcomeRankTimeline)
    {
        MaxRank = maxRank;
        OutcomeRankTimeline = outcomeRankTimeline;
    }

    /// <summary>The weakest rank a beat of the pattern can have; the strongest is 0.</summary>
    public int MaxRank { get; }

    public EventTimeline<int> OutcomeRankTimeline { get; }

    /// <param name="rankWeightFunc">The chance of keeping a position of a rank.</param>
    /// <param name="variation">
    ///     The chance of a cycle drawing afresh what it keeps, rather than keeping what the cycle before kept: 1 draws
    ///     every cycle afresh, and 0 repeats the first cycle all along, as a riff; a cycle of one rank then is a pulse.
    /// </param>
    public static DyadicRankThresholdPattern Create(
        IGenerationContext generationContext,
        int generationSeed,
        Func<int, double> rankWeightFunc,
        DyadicTimelineDescriptor descriptor,
        double variation = 1
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(variation);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(variation, 1);

        var seededGenerationContext = generationContext.CreateContext(generationSeed);
        var decisions = new Dictionary<int, bool>();
        var kept = new List<TimelineItem<int>>();
        var cycle = -1;
        var isRedrawn = true;
        foreach (var slot in DyadicRankTimeline.GenerateSlots(descriptor.Duration, descriptor.Phase, descriptor.Period, descriptor.MaxRank))
        {
            if (slot.Cycle != cycle)
            {
                cycle = slot.Cycle;
                // the first cycle is always drawn; a later one by the variation, which at 1 draws without a draw
                isRedrawn = cycle == 0 || seededGenerationContext.TestProbability(variation);
            }

            if (isRedrawn || !decisions.TryGetValue(slot.Slot, out var isKept))
            {
                isKept = seededGenerationContext.TestProbability(rankWeightFunc(slot.Rank));
                decisions[slot.Slot] = isKept;
            }

            if (isKept)
                kept.Add(new TimelineItem<int>(slot.Position, slot.Rank));
        }

        return new DyadicRankThresholdPattern(descriptor.MaxRank, EventTimeline.Create(descriptor.Duration, kept));
    }
}
