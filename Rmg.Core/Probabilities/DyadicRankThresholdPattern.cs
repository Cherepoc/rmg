using Rmg.Core.Events;

namespace Rmg.Core.Probabilities;

public sealed class DyadicRankThresholdPattern
{
    public int GenerationSeed { get; }

    public double Intensity { get; }

    public DyadicTimelineDescriptor Descriptor { get; }

    public EventTimeline<ProbabilityThreshold<int>> ProbabilityThresholdTimeline { get; }

    public EventTimeline<int> OutcomeRankTimeline { get; }

    private DyadicRankThresholdPattern(
        int generationSeed,
        double intensity,
        DyadicTimelineDescriptor descriptor,
        EventTimeline<ProbabilityThreshold<int>> probabilityThresholdTimeline,
        EventTimeline<int> outcomeRankTimeline
    )
    {
        GenerationSeed = generationSeed;
        Intensity = intensity;
        Descriptor = descriptor;
        ProbabilityThresholdTimeline = probabilityThresholdTimeline;
        OutcomeRankTimeline = outcomeRankTimeline;
    }

    public DyadicRankThresholdPattern WithIntensity(IGenerationContext generationContext, double intensity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(intensity);
        
        var outcomeRankTimeline = GenerateOutcomeRankTimeline(
            generationContext,
            GenerationSeed,
            intensity,
            ProbabilityThresholdTimeline
        );

        return new DyadicRankThresholdPattern(
            GenerationSeed,
            intensity,
            Descriptor,
            ProbabilityThresholdTimeline,
            outcomeRankTimeline
        );
    }

    public static DyadicRankThresholdPattern Create(
        IGenerationContext generationContext,
        int generationSeed,
        double intensity,
        Func<int, double> rankWeightFunc,
        DyadicTimelineDescriptor descriptor
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(intensity);

        var probabilityThresholdTimeline =
            DyadicRankTimeline.Generate(descriptor.Duration, descriptor.Phase, descriptor.Period, descriptor.MaxRank)
                .MapValues(x => new ProbabilityThreshold<int>(rankWeightFunc(x), x));

        var outcomeRankTimeline = GenerateOutcomeRankTimeline(
            generationContext,
            generationSeed,
            intensity,
            probabilityThresholdTimeline
        );

        return new DyadicRankThresholdPattern(
            generationSeed,
            intensity,
            descriptor,
            probabilityThresholdTimeline,
            outcomeRankTimeline
        );
    }

    private static EventTimeline<int> GenerateOutcomeRankTimeline(
        IGenerationContext generationContext,
        int generationSeed,
        double intensity,
        EventTimeline<ProbabilityThreshold<int>> probabilityThresholdTimeline
    )
    {
        var seededGenerationContext = generationContext.CreateContext(generationSeed);
        return probabilityThresholdTimeline
            .FilterValues(x => seededGenerationContext.TestProbability(Math.Min(x.Threshold * intensity, 1)))
            .MapValues(x => x.Value);
    }
}