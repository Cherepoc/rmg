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

    public static DyadicRankThresholdPattern Create(
        IGenerationContext generationContext,
        int generationSeed,
        Func<int, double> rankWeightFunc,
        DyadicTimelineDescriptor descriptor
    )
    {
        var seededGenerationContext = generationContext.CreateContext(generationSeed);
        var outcomeRankTimeline =
            DyadicRankTimeline.Generate(descriptor.Duration, descriptor.Phase, descriptor.Period, descriptor.MaxRank)
                .FilterValues(x => seededGenerationContext.TestProbability(rankWeightFunc(x)));

        return new DyadicRankThresholdPattern(descriptor.MaxRank, outcomeRankTimeline);
    }
}
