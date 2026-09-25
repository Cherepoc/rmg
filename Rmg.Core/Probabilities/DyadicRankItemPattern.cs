using Rmg.Core.Events;

namespace Rmg.Core.Probabilities;

public sealed class DyadicRankItemPattern<T>
    where T : notnull
{
    private DyadicRankItemPattern(EventTimeline<T> generatedTimeline)
    {
        GeneratedTimeline = generatedTimeline;
    }

    public EventTimeline<T> GeneratedTimeline { get; }

    public static DyadicRankItemPattern<T> Create(
        IGenerationContext context,
        DyadicRankThresholdPattern rhythmPattern,
        Func<IGenerationContext, Func<double, int, T>> rankedItemGeneratorFunc,
        int generationSeed
    )
    {
        var seededContext = context.CreateContext(generationSeed);
        var rankedItemGenerator = rankedItemGeneratorFunc(seededContext);
        var itemTimeline = rhythmPattern.OutcomeRankTimeline
            .MapValues(rankedItemGenerator);
        return new DyadicRankItemPattern<T>(itemTimeline);
    }
}
