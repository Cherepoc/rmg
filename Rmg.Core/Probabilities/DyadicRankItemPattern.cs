using Rmg.Core.Events;

namespace Rmg.Core.Probabilities;

public sealed class DyadicRankItemPattern<T>
    where T : notnull
{
    private readonly Func<IGenerationContext, Func<double, int, T>> _rankedItemGeneratorFunc;

    private DyadicRankItemPattern(
        DyadicRankThresholdPattern rhythmPattern,
        Func<IGenerationContext, Func<double, int, T>> rankedItemGeneratorFunc,
        int generationSeed,
        EventTimeline<T> generatedTimeline
    )
    {
        _rankedItemGeneratorFunc = rankedItemGeneratorFunc;
        GenerationSeed = generationSeed;
        RhythmPattern = rhythmPattern;
        GeneratedTimeline = generatedTimeline;
    }

    public int GenerationSeed { get; }

    public DyadicRankThresholdPattern RhythmPattern { get; }

    public EventTimeline<T> GeneratedTimeline { get; }

    public DyadicRankItemPattern<T> WithRhythmIntensity(IGenerationContext context, double intensity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(intensity);

        var newRhythmPattern = RhythmPattern.WithIntensity(context, intensity);
        return Create(context, newRhythmPattern, _rankedItemGeneratorFunc, GenerationSeed);
    }

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
        return new DyadicRankItemPattern<T>(
            rhythmPattern,
            rankedItemGeneratorFunc,
            generationSeed,
            itemTimeline
        );
    }
}
