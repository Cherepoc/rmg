using System.Diagnostics;

namespace Rmg.Core.Events;

[DebuggerDisplay("[State {Kind.Name}: {Value}]")]
public readonly struct State<T> : IState, IEquatable<State<T>>
    where T : notnull
{
    public StateKind<T> Kind { get; }

    public T Value { get; }

    private readonly object _objectValue;

    public bool IsDefault => Kind.CheckValueIsDefault(Value);

    public State(StateKind<T> kind, T value)
    {
        Kind = kind;
        Value = value;
        _objectValue = Value;
    }

    IStateKind IState.Kind => Kind;

    object IState.Value => _objectValue;

    public State<T> Map(Func<T, T> mapFunc)
    {
        return new State<T>(Kind, mapFunc(Value));
    }

    public State<T> ToKind(StateKind<T> kind)
    {
        return new State<T>(kind, Value);
    }

    IState IState.ToKind(IStateKind kind)
    {
        if (kind is StateKind<T> stateKind)
            return ToKind(stateKind);

        throw new ArgumentException(
            $"Cannot convert state with kind {Kind} to kind {kind} - only kinds with the same value type are allowed."
        );
    }

    public bool Equals(State<T> other)
    {
        return Kind.Equals(other.Kind) && Kind.CheckValuesEqual(Value, other.Value);
    }

    public override bool Equals(object? obj)
    {
        return obj is State<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Kind, Value);
    }

    public static bool operator ==(State<T> left, State<T> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(State<T> left, State<T> right)
    {
        return !left.Equals(right);
    }
}

public static class State
{
    public static StateMap ToStateMap(this IEnumerable<IState> states)
    {
        return StateMap.FromStates(states);
    }
}

public interface IState
{
    public IStateKind Kind { get; }

    public object Value { get; }

    public bool IsDefault { get; }

    public IState ToKind(IStateKind kind);
}
