using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>Generates the timeline of one state kind, such as the chord root of a progression.</summary>
public interface IStateTimelineGenerator
{
    IStateTimeline Generate(IGenerationContext context, double duration);
}

/// <summary>
///     Generates a state timeline that takes a new value at the start of every step, picked from a pool drawn
///     beforehand, so that some of them come back.
/// </summary>
public sealed class StateTimelineGenerator<T> : IStateTimelineGenerator
    where T : notnull
{
    private readonly Func<IGenerationContext, T> _valueGenerator;

    internal StateTimelineGenerator(
        StateKind<T> stateKind,
        double stepDuration,
        Func<IGenerationContext, T> valueGenerator,
        int poolSize,
        string layer
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stepDuration);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(poolSize);

        Layer = layer;
        StateKind = stateKind;
        StepDuration = stepDuration;
        PoolSize = poolSize;
        _valueGenerator = valueGenerator;
    }

    public StateKind<T> StateKind { get; }

    public double StepDuration { get; }

    /// <summary>How many values the steps pick from, one at the least.</summary>
    public int PoolSize { get; }

    /// <summary>The layer the values belong to, such as the bar, which a <see cref="StateTrace" /> records.</summary>
    public string Layer { get; }

    IStateTimeline IStateTimelineGenerator.Generate(IGenerationContext context, double duration)
    {
        return Generate(context, duration);
    }

    public StateTimeline<T> Generate(IGenerationContext context, double duration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration);

        var valueGenerator = Generators.ItemSelector(Generators.Sequence(_valueGenerator, PoolSize)(context));

        var stepCount = (int)Math.Ceiling(duration / StepDuration);
        var items = new TimelineItem<T>[stepCount];
        for (var i = 0; i < stepCount; i++)
            items[i] = valueGenerator(context).ToTimelineItem(i * StepDuration);

        var timeline = StateTimeline.Create(duration, StateKind, items);
        return timeline.WithLayer(Layer);
    }
}

internal static class StateTimelineGenerator
{
    public static StateTimelineGenerator<T> Create<T>(
        StateKind<T> stateKind,
        double stepDuration,
        Func<IGenerationContext, T> valueGenerator,
        int poolSize,
        string layer
    )
        where T : notnull
    {
        return new StateTimelineGenerator<T>(stateKind, stepDuration, valueGenerator, poolSize, layer);
    }
}
