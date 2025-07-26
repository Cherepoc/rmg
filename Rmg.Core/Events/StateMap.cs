using System.Collections.Immutable;

namespace Rmg.Core.Events;

public sealed class StateMap
{
    private readonly int _hashCode;
    private readonly ImmutableDictionary<IStateKind, IState> _stateByKinds;

    private readonly ImmutableArray<IState> _states;

    private StateMap(ImmutableArray<IState> states)
    {
        _states = states;
        _hashCode = CalculateHashCode(_states);
        Kinds = [.._states.Select(x => x.Kind)];
        _stateByKinds = _states.ToImmutableDictionary(x => x.Kind);
    }

    public static StateMap Default { get; } = new(ImmutableArray<IState>.Empty);

    public bool IsDefault => _states.IsEmpty;

    public ImmutableArray<IStateKind> Kinds { get; }

    public ImmutableArray<IState> States => _states;

    public static StateMap FromStates(IEnumerable<IState> states)
    {
        var processedStates = states
            .GroupBy(x => x.Kind)
            .Select(x => x.Key.AggregateStates(x))
            .Where(x => !x.IsDefault)
            .OrderBy(x => x.Kind.Name)
            .ToImmutableArray();
        if (processedStates.IsEmpty)
            return Default;

        return new StateMap(processedStates);
    }

    public T GetStateValue<T>(StateKind<T> kind)
        where T : notnull
    {
        if (_stateByKinds.TryGetValue(kind, out var state))
            return (T)state.Value;

        return kind.DefaultValue;
    }

    public State<T> GetState<T>(StateKind<T> kind)
        where T : notnull
    {
        return _stateByKinds.TryGetValue(kind, out var state) ? (State<T>)state : kind.DefaultState;
    }

    public IState GetState(IStateKind kind)
    {
        return _stateByKinds.TryGetValue(kind, out var state) ? state : kind.DefaultState;
    }

    public ImmutableArray<IStateTimeline> CreateTimelines(double duration)
    {
        return _states
            .Select(x => x.Kind.CreateTimelineFromValue(duration, x.Value))
            .ToImmutableArray();
    }

    public StateTimelineMap ToStateTimelineMap(double duration)
    {
        var stateTimelines = _states.Select(x => x.Kind.CreateTimelineFromValue(duration, x.Value));
        return StateTimelineMap.Create(duration, stateTimelines);
    }

    public StateMap Subset(IEnumerable<IStateKind> stateKinds)
    {
        return FromStates(_stateByKinds.RemoveRange(_stateByKinds.Keys.Except(stateKinds)).Values);
    }

    public StateMap Except(IEnumerable<IStateKind> stateKinds)
    {
        return FromStates(_stateByKinds.RemoveRange(stateKinds).Values);
    }

    public StateMap SetValue<T>(StateKind<T> stateKind, T value)
        where T : notnull
    {
        return FromStates(_stateByKinds.SetItem(stateKind, stateKind.CreateState(value)).Values);
    }

    public StateMap SetValue<T>(StateKind<T> stateKind, Func<T, T> valueMapFunc)
        where T : notnull
    {
        var stateValue = GetStateValue(stateKind);
        var mappedValue = valueMapFunc(stateValue);
        return FromStates(_stateByKinds.SetItem(stateKind, stateKind.CreateState(mappedValue)).Values);
    }

    public StateMap SwapStateKinds(IEnumerable<KeyValuePair<IStateKind, IStateKind>> stateKindPairs)
    {
        var stateKindDictionary = stateKindPairs.AsReadOnlyDictionary();
        if (stateKindDictionary.Count == 0)
            return this;

        var newStates = new IState[_states.Length];
        for (var i = 0; i < _states.Length; i++)
        {
            var state = _states[i];
            newStates[i] = stateKindDictionary.TryGetValue(state.Kind, out var newStateKind)
                ? newStateKind.CreateState(state.Value)
                : state;
        }

        return FromStates(newStates);
    }

    public StateMap MergeWith(StateMap other)
    {
        if (other.IsDefault)
            return this;
        if (IsDefault)
            return other;

        return FromStates(_states.Concat(other._states));
    }

    private bool Equals(StateMap other)
    {
        return _states.SequenceEqual(other._states);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is StateMap other && Equals(other);
    }

    public override int GetHashCode()
    {
        return _hashCode;
    }

    public static bool operator ==(StateMap? left, StateMap? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(StateMap? left, StateMap? right)
    {
        return !Equals(left, right);
    }

    public static StateMap Aggregate(IEnumerable<StateMap> stateMaps)
    {
        return FromStates(stateMaps.SelectMany(x => x.States));
    }

    private static int CalculateHashCode(ImmutableArray<IState> states)
    {
        var hashCode = new HashCode();
        foreach (var state in states)
            hashCode.Add(state.GetHashCode());
        return hashCode.ToHashCode();
    }
}
