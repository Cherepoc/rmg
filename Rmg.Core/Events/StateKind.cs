using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Rmg.Core.Events;

[DebuggerDisplay("State Kind {Name} ({typeof(T)})")]
public sealed class StateKind<T> : IStateKind
    where T : notnull
{
    private readonly Func<IEnumerable<T>, T> _aggregateFunc;
    private readonly IState _defaultStateObject;

    private readonly object _defaultValueObject;
    private readonly Func<T, T, bool> _equalityFunc;
    private readonly Func<T, int> _hashFunc;
    private readonly StateTimeline<T> _zeroDurationTimeline;

    public StateKind(
        string name,
        T defaultValue,
        Func<T, T, bool> equalityFunc,
        Func<IEnumerable<T>, T> aggregateFunc,
        Func<T, int>? hashFunc = null
    )
    {
        Name = name;
        DefaultValue = defaultValue;
        DefaultState = new State<T>(this, defaultValue);
        _equalityFunc = equalityFunc;
        _aggregateFunc = aggregateFunc;
        _hashFunc = hashFunc ?? (value => EqualityComparer<T>.Default.GetHashCode(value));

        _defaultValueObject = DefaultValue;
        _defaultStateObject = DefaultState;

        // each kind gets its own zero-duration timeline so that it reports the kind's default value, not default(T)
        _zeroDurationTimeline = new StateTimeline<T>(0, this, []);
    }

    /// <summary>
    ///     A placeholder kind for a state timeline that has no kind of its own, such as the result of merging no
    ///     timelines.
    /// </summary>
    public static StateKind<T> None { get; } = CreateNone();

    public T DefaultValue { get; }

    public State<T> DefaultState { get; }

    public string Name { get; }

    object IStateKind.DefaultValue => _defaultValueObject;

    IState IStateKind.DefaultState => _defaultStateObject;

    IStateTimeline IStateKind.CreateDefaultTimeline(double duration)
    {
        return CreateDefaultTimeline(duration);
    }

    public object AggregateValues(IEnumerable values)
    {
        return AggregateValues(values.Cast<T>());
    }

    public bool CheckValuesEqual(object value1, object value2)
    {
        return _equalityFunc((T)value1, (T)value2);
    }

    public bool CheckValueIsDefault(object value)
    {
        return CheckValueIsDefault((T)value);
    }

    public IState AggregateValuesToState(IEnumerable values)
    {
        return AggregateValuesToState(values.Cast<T>());
    }

    public IState AggregateStates(IEnumerable<IState> states)
    {
        return AggregateValuesToState(states.Select(x => (T)x.Value));
    }

    IState IStateKind.CreateState(object value)
    {
        return CreateState((T)value);
    }

    public IStateTimeline MergeTimelines(IEnumerable<IStateTimeline> timelines)
    {
        return MergeTimelines(timelines.Cast<StateTimeline<T>>());
    }

    IStateTimeline IStateKind.CreateTimelineFromValue(double duration, object value)
    {
        return CreateTimelineFromValue(duration, (T)value);
    }

    IStateTimeline IStateKind.ExtractStateTimeline(EventTimeline<StateMap> eventTimeline)
    {
        return ExtractStateTimeline(eventTimeline);
    }

    // The field is looked up by name, which a trimmed or native build cannot follow on its own, so it is kept
    // by name here. Every ImmutableArray<> this reaches is one the program itself constructs, so its
    // instantiation is always compiled in; only the field's metadata could otherwise go missing.
    [DynamicDependency(nameof(ImmutableArray<int>.Empty), typeof(ImmutableArray<>))]
    [UnconditionalSuppressMessage("Trimming", "IL2090", Justification = "ImmutableArray<>.Empty is kept by the DynamicDependency above.")]
    private static StateKind<T> CreateNone()
    {
        var type = typeof(T);
        T defaultValue;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>))
        {
            // default(ImmutableArray<>) is an uninitialized array that throws on access
            var emptyField = type.GetField(nameof(ImmutableArray<int>.Empty))!;
            defaultValue = (T)emptyField.GetValue(null)!;
        }
        else
        {
            defaultValue = default!;
        }

        return new StateKind<T>(
            "None",
            defaultValue,
            (_, _) => throw new NotImplementedException(),
            _ => throw new NotImplementedException()
        );
    }

    public T AggregateValues(IEnumerable<T> values)
    {
        return _aggregateFunc(values);
    }

    public bool CheckValuesEqual(T value1, T value2)
    {
        return _equalityFunc(value1, value2);
    }

    public int GetValueHashCode(T value)
    {
        return _hashFunc(value);
    }

    public bool CheckValueIsDefault(T value)
    {
        return CheckValuesEqual(value, DefaultValue);
    }

    public State<T> AggregateValuesToState(IEnumerable<T> values)
    {
        return new State<T>(this, AggregateValues(values));
    }

    public State<T> AggregateStates(IEnumerable<State<T>> states)
    {
        return AggregateValuesToState(states.Select(x => x.Value));
    }

    public State<T> CreateState(T value)
    {
        return new State<T>(this, value);
    }

    public StateTimeline<T> MergeTimelines(IEnumerable<StateTimeline<T>> timelines)
    {
        var timelinesArray = timelines.AsImmutableArray();
        if (timelinesArray.All(x => x.Duration == 0))
            return _zeroDurationTimeline;

        if (timelinesArray.Any(x => x.StateKind != this))
            throw new ArgumentException($"Timelines must have the state kind {Name}.", nameof(timelines));

        return StateTimeline<T>.Merge(timelinesArray);
    }

    public StateTimeline<T> CreateTimelineFromValue(double duration, T value)
    {
        if (duration <= 0 || CheckValueIsDefault(value))
            return CreateDefaultTimeline(duration);

        return new StateTimeline<T>(duration, this, [new TimelineItem<T>(0, value)]);
    }

    /// <summary>A timeline of the given duration that holds the kind's default value all along.</summary>
    public StateTimeline<T> CreateDefaultTimeline(double duration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration);

        return duration == 0 ? _zeroDurationTimeline : new StateTimeline<T>(duration, this, []);
    }

    public StateTimeline<T> ExtractStateTimeline(EventTimeline<StateMap> eventTimeline)
    {
        var items = eventTimeline.MapValues(x => x.GetStateValue(this));
        return StateTimeline.Create(eventTimeline.Duration, this, items.ToImmutableArray());
    }
}

public static class StateKind
{
    public static StateKind<T> None<T>()
        where T : notnull
    {
        return StateKind<T>.None;
    }
}

public interface IStateKind
{
    string Name { get; }

    object DefaultValue { get; }

    IState DefaultState { get; }

    IStateTimeline CreateDefaultTimeline(double duration);

    object AggregateValues(IEnumerable values);

    bool CheckValuesEqual(object value1, object value2);

    bool CheckValueIsDefault(object value);

    IState AggregateValuesToState(IEnumerable values);

    IState AggregateStates(IEnumerable<IState> values);

    IState CreateState(object value);

    IStateTimeline MergeTimelines(IEnumerable<IStateTimeline> timelines);

    IStateTimeline CreateTimelineFromValue(double duration, object value);

    IStateTimeline ExtractStateTimeline(EventTimeline<StateMap> eventTimeline);
}
