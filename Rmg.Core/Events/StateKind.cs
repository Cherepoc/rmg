using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Rmg.Core.Events;

[DebuggerDisplay("State Kind {Name} ({typeof(T)})")]
public sealed class StateKind<T> : IStateKind
    where T : notnull
{
    public static StateKind<T> Empty { get; } = CreateEmpty();

    private static StateKind<T> CreateEmpty()
    {
        var type = typeof(T);
        T defaultValue;
        if (type.IsAssignableTo(typeof(IEnumerable<>)))
        {
            var elementType = type.GetGenericArguments()[0];
            defaultValue = (T)(object)Array.CreateInstance(elementType, 0);
        }
        else
        {
            defaultValue = default!;
        }

        return new StateKind<T>(
            "Empty",
            defaultValue,
            (_, _) => throw new NotImplementedException(),
            _ => throw new NotImplementedException()
        );
    }

    private readonly object _defaultValueObject;
    private readonly IState _defaultStateObject;
    private readonly Func<T, T, bool> _equalityFunc;
    private readonly Func<IEnumerable<T>, T> _aggregateFunc;

    public StateKind(string name, T defaultValue, Func<T, T, bool> equalityFunc, Func<IEnumerable<T>, T> aggregateFunc)
    {
        Name = name;
        DefaultValue = defaultValue;
        DefaultState = new State<T>(this, defaultValue);
        _equalityFunc = equalityFunc;
        _aggregateFunc = aggregateFunc;

        _defaultValueObject = DefaultValue;
        _defaultStateObject = DefaultState;
    }

    public string Name { get; }

    public T DefaultValue { get; }

    object IStateKind.DefaultValue => _defaultValueObject;

    public State<T> DefaultState { get; }

    IState IStateKind.DefaultState => _defaultStateObject;

    public StateTimeline<T> EmptyTimeline => StateTimeline<T>.Empty;

    IStateTimeline IStateKind.EmptyTimeline => EmptyTimeline;

    public T AggregateValues(IEnumerable<T> values) => _aggregateFunc(values);

    public object AggregateValues(IEnumerable values) => AggregateValues(values.Cast<T>());

    public bool CheckValuesEqual(T value1, T value2) => _equalityFunc(value1, value2);

    public bool CheckValuesEqual(object value1, object value2) => _equalityFunc((T)value1, (T)value2);

    public bool CheckValueIsDefault(T value) => CheckValuesEqual(value, DefaultValue);

    public bool CheckValueIsDefault(object value) => CheckValueIsDefault((T)value);

    public State<T> AggregateValuesToState(IEnumerable<T> values) => new(this, AggregateValues(values));

    public IState AggregateValuesToState(IEnumerable values) => AggregateValuesToState(values.Cast<T>());

    public State<T> AggregateStates(IEnumerable<State<T>> states) => AggregateValuesToState(states.Select(x => x.Value));
    
    public IState AggregateStates(IEnumerable<IState> states) => AggregateValuesToState(states.Select(x => (T)x.Value));

    public State<T> CreateState(T value) => new(this, value);

    IState IStateKind.CreateState(object value) => CreateState((T)value);

    public StateTimeline<T> MergeTimelines(IEnumerable<StateTimeline<T>> timelines)
    {
        var timelinesArray = timelines.AsImmutableArray();
        if (timelinesArray.Length == 0 || timelinesArray.All(x => x.IsEmpty))
            return EmptyTimeline;
        
        if (timelinesArray.Any(x => x.StateKind != this))
            throw new ArgumentException($"Timelines must have the state kind {Name}.", nameof(timelines));
        
        return StateTimeline<T>.Merge(timelinesArray);
    }

    public IStateTimeline MergeTimelines(IEnumerable<IStateTimeline> timelines) =>
        MergeTimelines(timelines.Cast<StateTimeline<T>>());

    public StateTimeline<T> CreateTimelineFromValue(double duration, T value)
    {
        if (duration <= 0 || CheckValueIsDefault(value))
            return EmptyTimeline;

        return new StateTimeline<T>(duration, this, [new TimelineItem<T>(0, value)]);
    }

    IStateTimeline IStateKind.CreateTimelineFromValue(double duration, object value) =>
        CreateTimelineFromValue(duration, (T)value);
    
    public StateTimeline<T> CreateStateTimelineFromEventTimeline(EventTimeline<T> eventTimeline)
    {
        if (eventTimeline.IsEmpty)
            return EmptyTimeline;

        return StateTimeline.Create(eventTimeline.Duration, this, eventTimeline.ToImmutableArray());
    }
    
    public StateTimeline<T> ExtractStateTimeline(EventTimeline<StateMap> eventTimeline)
    {
        if (eventTimeline.IsEmpty)
            return EmptyTimeline;

        var items = eventTimeline.MapValues(x => x.GetStateValue(this));
        return StateTimeline.Create(eventTimeline.Duration, this, items.ToImmutableArray());
    }

    IStateTimeline IStateKind.ExtractStateTimeline(EventTimeline<StateMap> eventTimeline) =>
        ExtractStateTimeline(eventTimeline);
}

public static class StateKind
{
    public static StateKind<T> Empty<T>()
        where T : notnull
    {
        return StateKind<T>.Empty;
    }
}

public interface IStateKind
{
    string Name { get; }

    object DefaultValue { get; }

    IState DefaultState { get; }

    IStateTimeline EmptyTimeline { get; }

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