using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Rmg.Core.Events;

/// <summary>Which part of the program a state kind is for.</summary>
public enum StateScope
{
    /// <summary>State that only the song's generation reads, such as the rhythm settings.</summary>
    Composition,

    /// <summary>State that <c>Render</c> turns into notes, such as the velocity and the pitch offsets.</summary>
    Render
}

[DebuggerDisplay("State Kind {Name} ({typeof(T)})")]
public sealed class StateKind<T> : IStateKind
    where T : notnull
{
    private readonly Func<IEnumerable<T>, T> _aggregateFunc;
    private readonly Func<T, bool> _isAggregatedFunc;
    private readonly Func<T, T, bool> _equalityFunc;
    private readonly Func<T, int> _hashFunc;
    private readonly StateTimeline<T> _zeroDurationTimeline;

    public StateKind(
        string name,
        T defaultValue,
        Func<T, T, bool> equalityFunc,
        Func<IEnumerable<T>, T> aggregateFunc,
        Func<T, int>? hashFunc = null,
        Func<T, bool>? isAggregatedFunc = null,
        StateScope scope = StateScope.Composition,
        bool isShared = false
    )
    {
        Key = StateKindNames.Register(name);
        Name = name;
        Scope = scope;
        IsShared = isShared;
        DefaultValue = defaultValue;
        DefaultState = new State<T>(this, defaultValue);
        _equalityFunc = equalityFunc;
        _aggregateFunc = aggregateFunc;
        _hashFunc = hashFunc ?? (value => EqualityComparer<T>.Default.GetHashCode(value));
        _isAggregatedFunc = isAggregatedFunc ?? (_ => true);

        // each kind gets its own zero-duration timeline so that it reports the kind's default value, not default(T)
        _zeroDurationTimeline = new StateTimeline<T>(0, this, []);
    }

    public T DefaultValue { get; }

    public State<T> DefaultState { get; }

    public string Name { get; }

    public long Key { get; }

    public StateScope Scope { get; }

    public bool IsShared { get; }

    IState IStateKind.DefaultState => DefaultState;

    IState IStateKind.AggregateStates(ReadOnlySpan<IState> states)
    {
        return AggregateStates(states);
    }

    bool IStateKind.CheckStateIsAggregated(IState state)
    {
        return _isAggregatedFunc(((State<T>)state).Value);
    }

    public IStateTimeline MergeTimelines(IEnumerable<IStateTimeline> timelines)
    {
        return MergeTimelines(timelines.Cast<StateTimeline<T>>());
    }

    IStateTimeline IStateKind.CreateTimelineFromState(double duration, IState state)
    {
        // a timeline holds values, not states, so a state's parts go with it: the layer of a value that is one part,
        // or a timeline of every part, which keeps a layer that took part twice visible as twice
        var timeline = CreateTimelineFromValue(duration, ((State<T>)state).Value);
        var contributions = state.Contributions;
        return contributions.Length switch
        {
            0 => timeline,
            1 => timeline.WithLayer(contributions[0].Layer),
            _ => timeline.WithSources([..contributions.Select(x => CreateTimelineFromValue(duration, (T)x.Value).WithLayer(x.Layer))])
        };
    }

    IStateTimeline IStateKind.ExtractStateTimeline(EventTimeline<StateMap> eventTimeline)
    {
        return ExtractStateTimeline(eventTimeline);
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

    /// <summary>
    ///     The states aggregated in their order. While a <see cref="StateTrace" /> runs, the result also keeps what
    ///     every one of them was made of.
    /// </summary>
    private State<T> AggregateStates(ReadOnlySpan<IState> states)
    {
        var values = new T[states.Length];
        for (var i = 0; i < states.Length; i++)
            values[i] = ((State<T>)states[i]).Value;
        var value = AggregateValues(values);

        if (!StateTrace.IsExplaining)
            return new State<T>(this, value);

        var contributions = ImmutableArray.CreateBuilder<StateContribution>();
        foreach (var state in states)
            contributions.AddRange(state.Contributions.IsEmpty ? [StateContribution.Unlabeled(state.Value)] : state.Contributions);
        return new State<T>(this, value, contributions.ToImmutable());
    }
}

/// <summary>
///     The names of all state kinds, of whatever value type, and their keys. A kind's name is its identity, so no two
///     kinds share one, nor a key.
/// </summary>
internal static class StateKindNames
{
    private static readonly ConcurrentDictionary<long, string> Keys = new();

    /// <summary>The kind's key, a hash of its name the same in every run (FNV-1a), which a state map orders its states by.</summary>
    public static long Register(string name)
    {
        var hash = 14695981039346656037UL;
        foreach (var c in name)
            hash = (hash ^ c) * 1099511628211UL;
        var key = unchecked((long)hash);
        if (!Keys.TryAdd(key, name))
            throw new ArgumentException(Keys[key] == name
                ? $"A state kind named '{name}' already exists."
                : $"The state kind '{name}' has the key of '{Keys[key]}'.", nameof(name));
        return key;
    }
}

public interface IStateKind
{
    string Name { get; }

    /// <summary>A number of the kind's own, from its name, by which a state map orders and finds its states (<see cref="StateKindNames.Register" />).</summary>
    long Key { get; }

    StateScope Scope { get; }

    /// <summary>
    ///     Whether every track must see the same value of the kind at the same time, such as the scale, so that only
    ///     the layers the tracks share may set it.
    /// </summary>
    bool IsShared { get; }

    IState DefaultState { get; }

    /// <summary>The states aggregated in their order; every one must be of this kind.</summary>
    IState AggregateStates(ReadOnlySpan<IState> states);

    /// <summary>
    ///     Whether the state is as aggregating it alone would leave it, such as a collection that is already in order,
    ///     so that it can be kept as it is.
    /// </summary>
    bool CheckStateIsAggregated(IState state);

    IStateTimeline MergeTimelines(IEnumerable<IStateTimeline> timelines);

    IStateTimeline CreateTimelineFromState(double duration, IState state);

    IStateTimeline ExtractStateTimeline(EventTimeline<StateMap> eventTimeline);
}
