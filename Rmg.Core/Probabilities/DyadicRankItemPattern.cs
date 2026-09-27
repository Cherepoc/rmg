using Rmg.Core.Events;

namespace Rmg.Core.Probabilities;

/// <summary>
///     The items of a rhythm's beats. Every beat draws its values, except a beat of a repeated cycle, which takes the
///     values its beat had in the cycle it repeats, so that a repeated figure repeats how it plays as well as where;
///     then every beat's item is made of its values at its own position, such as with the chord there.
/// </summary>
public sealed class DyadicRankItemPattern<T>
    where T : notnull
{
    private DyadicRankItemPattern(EventTimeline<T> generatedTimeline)
    {
        GeneratedTimeline = generatedTimeline;
    }

    public EventTimeline<T> GeneratedTimeline { get; }

    /// <param name="drawFunc">Draws a beat's values, in the order of the beats, from the pattern's own sequence.</param>
    /// <param name="generationSeed">The seed of the pattern's own sequence.</param>
    /// <param name="complete">Makes a beat's item of its values, at its position; none keeps the values.</param>
    public static DyadicRankItemPattern<T> Create(
        IGenerationContext context,
        DyadicRankThresholdPattern rhythmPattern,
        Func<IGenerationContext, Func<double, KeptBeat, T>> drawFunc,
        int generationSeed,
        Func<double, KeptBeat, T, T>? complete = null
    )
    {
        var seededContext = context.CreateContext(generationSeed);
        var draw = drawFunc(seededContext);
        complete ??= (_, _, values) => values;
        // every beat's values by its cycle and its place in it
        var drawn = new Dictionary<(int Cycle, int Slot), T>();
        var itemTimeline = rhythmPattern.OutcomeTimeline.MapValues((position, beat) =>
            {
                if (!beat.IsRepeated || !drawn.TryGetValue((beat.Source, beat.Slot), out var values))
                    drawn[(beat.Cycle, beat.Slot)] = values = draw(position, beat);
                return complete(position, beat, values);
            }
        );
        return new DyadicRankItemPattern<T>(itemTimeline);
    }
}
