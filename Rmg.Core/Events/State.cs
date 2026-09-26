using System.Collections.Immutable;
using System.Diagnostics;

namespace Rmg.Core.Events;

/// <summary>
///     A value of a state kind. It is a class, since a state is nearly always kept as an <see cref="IState" />, which
///     a struct would be boxed into.
/// </summary>
[DebuggerDisplay("[State {Kind.Name}: {Value}]")]
public sealed class State<T> : IState, IEquatable<State<T>>
    where T : notnull
{
    public State(StateKind<T> kind, T value)
    {
        Kind = kind;
        Value = value;
    }

    internal State(StateKind<T> kind, T value, ImmutableArray<StateContribution> contributions)
        : this(kind, value)
    {
        Contributions = contributions;
    }

    public StateKind<T> Kind { get; }

    public T Value { get; }

    public bool IsDefault => Kind.CheckValueIsDefault(Value);

    /// <summary>What the value was made of, layer by layer, while a <see cref="StateTrace" /> runs; empty otherwise.</summary>
    public ImmutableArray<StateContribution> Contributions { get; } = [];

    IStateKind IState.Kind => Kind;

    object IState.Value => Value;

    // a state made from another keeps what that one was made of
    public State<T> Map(Func<T, T> mapFunc)
    {
        return new State<T>(Kind, mapFunc(Value), Contributions);
    }

    public State<T> ToKind(StateKind<T> kind)
    {
        return new State<T>(kind, Value, Contributions);
    }

    IState IState.WithLayer(string layer)
    {
        return new State<T>(Kind, Value, [new StateContribution(layer, Value)]);
    }

    IState IState.ToKind(IStateKind kind)
    {
        if (kind is StateKind<T> stateKind)
            return ToKind(stateKind);

        throw new ArgumentException(
            $"Cannot convert state with kind {Kind} to kind {kind} - only kinds with the same value type are allowed."
        );
    }

    public bool Equals(State<T>? other)
    {
        if (other is null)
            return false;

        return ReferenceEquals(this, other) || Kind.Equals(other.Kind) && Kind.CheckValuesEqual(Value, other.Value);
    }

    public override bool Equals(object? obj)
    {
        return obj is State<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Kind, Kind.GetValueHashCode(Value));
    }

    public static bool operator ==(State<T>? left, State<T>? right)
    {
        return left?.Equals(right) ?? right is null;
    }

    public static bool operator !=(State<T>? left, State<T>? right)
    {
        return !(left == right);
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

    public ImmutableArray<StateContribution> Contributions { get; }

    /// <summary>The state, recorded as the given layer's contribution.</summary>
    public IState WithLayer(string layer);

    public IState ToKind(IStateKind kind);
}
