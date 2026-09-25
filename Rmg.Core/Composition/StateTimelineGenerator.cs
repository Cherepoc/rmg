using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>Generates the timeline of one state kind, such as the chord root of a progression.</summary>
public interface IStateTimelineGenerator
{
    IStateTimeline Generate(IGenerationContext context, double duration);
}

/// <summary>
///     Generates a state timeline that takes a new value at the start of every step. The values are drawn fresh for
///     every step, or picked from a pool drawn beforehand, so that some of them come back.
/// </summary>
public sealed class StateTimelineGenerator<T> : IStateTimelineGenerator
    where T : notnull
{
    private readonly Func<IGenerationContext, T> _valueGenerator;

    internal StateTimelineGenerator(
        StateKind<T> stateKind,
        double stepDuration,
        Func<IGenerationContext, T> valueGenerator,
        int poolSize
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stepDuration);
        ArgumentOutOfRangeException.ThrowIfNegative(poolSize);

        StateKind = stateKind;
        StepDuration = stepDuration;
        PoolSize = poolSize;
        _valueGenerator = valueGenerator;
    }

    public StateKind<T> StateKind { get; }

    public double StepDuration { get; }

    /// <summary>How many values the steps pick from; 0 draws a fresh value for every step.</summary>
    public int PoolSize { get; }

    IStateTimeline IStateTimelineGenerator.Generate(IGenerationContext context, double duration)
    {
        return Generate(context, duration);
    }

    public StateTimeline<T> Generate(IGenerationContext context, double duration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration);

        var valueGenerator = PoolSize > 0
            ? Generators.ItemSelector(Generators.Sequence(_valueGenerator, PoolSize)(context))
            : _valueGenerator;

        var stepCount = (int)Math.Ceiling(duration / StepDuration);
        var items = new TimelineItem<T>[stepCount];
        for (var i = 0; i < stepCount; i++)
            items[i] = valueGenerator(context).ToTimelineItem(i * StepDuration);

        return StateTimeline.Create(duration, StateKind, items);
    }
}

public static class StateTimelineGenerator
{
    public static StateTimelineGenerator<T> Create<T>(
        StateKind<T> stateKind,
        double stepDuration,
        Func<IGenerationContext, T> valueGenerator,
        int poolSize = 0
    )
        where T : notnull
    {
        return new StateTimelineGenerator<T>(stateKind, stepDuration, valueGenerator, poolSize);
    }
}
