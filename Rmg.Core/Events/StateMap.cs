using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Rmg.Core.Events;

/// <summary>
///     States of different kinds, at most one of each and none with its kind's default value, ordered by kind name. The
///     order lets two maps be merged in one pass and a kind be found by binary search.
/// </summary>
public sealed class StateMap
{
    // aggregating states sorts them by kind, and a sort this small is quicker as an insertion sort, which is stable
    private const int InsertionSortMaxLength = 64;

    private readonly ImmutableArray<IState> _states;

    // computed when first asked for; 0 means not yet
    private int _hashCode;
    private ImmutableArray<IStateKind> _kinds;

    private StateMap(ImmutableArray<IState> states)
    {
        _states = states;
    }

    public static StateMap Default { get; } = new([]);

    public bool IsDefault => _states.IsEmpty;

    public ImmutableArray<IStateKind> Kinds
    {
        get
        {
            if (_kinds.IsDefault)
                _kinds = [.._states.Select(x => x.Kind)];
            return _kinds;
        }
    }

    public ImmutableArray<IState> States => _states;

    /// <summary>
    ///     The states, those of the same kind aggregated in the order they come in and those left at their kind's
    ///     default value dropped.
    /// </summary>
    public static StateMap FromStates(IEnumerable<IState> states)
    {
        var items = states.ToArray();
        if (items.Length == 0)
            return Default;

        SortByKind(items);

        var count = 0;
        var isUnchanged = true;
        for (var start = 0; start < items.Length;)
        {
            var kind = items[start].Kind;
            var end = start + 1;
            while (end < items.Length && ReferenceEquals(items[end].Kind, kind))
                end++;

            var state = items[start];
            if (end - start > 1 || !kind.CheckStateIsAggregated(state))
            {
                state = kind.AggregateStates(new ReadOnlySpan<IState>(items, start, end - start));
                isUnchanged = false;
            }

            if (state.IsDefault)
                isUnchanged = false;
            else
                items[count++] = state;

            start = end;
        }

        if (count == 0)
            return Default;

        // the array is this method's own, so it can become the map's without a copy
        return isUnchanged
            ? new StateMap(ImmutableCollectionsMarshal.AsImmutableArray(items))
            : new StateMap(ImmutableCollectionsMarshal.AsImmutableArray(items[..count]));
    }

    public T GetStateValue<T>(StateKind<T> kind)
        where T : notnull
    {
        var index = IndexOf(kind);
        return index >= 0 ? ((State<T>)_states[index]).Value : kind.DefaultValue;
    }

    public State<T> GetState<T>(StateKind<T> kind)
        where T : notnull
    {
        var index = IndexOf(kind);
        return index >= 0 ? (State<T>)_states[index] : kind.DefaultState;
    }

    public IState GetState(IStateKind kind)
    {
        var index = IndexOf(kind);
        return index >= 0 ? _states[index] : kind.DefaultState;
    }

    public StateTimelineMap ToStateTimelineMap(double duration)
    {
        var stateTimelines = _states.Select(x => x.Kind.CreateTimelineFromState(duration, x));
        return StateTimelineMap.Create(duration, stateTimelines);
    }

    /// <summary>The states of the scope, such as those that <c>Render</c> reads.</summary>
    public StateMap OfScope(StateScope scope)
    {
        return Filter(x => x.Kind.Scope == scope);
    }

    /// <summary>
    ///     This map, if none of its kinds is shared, and an exception naming them otherwise: a layer of a single track
    ///     cannot set what every track must see the same, such as the scale.
    /// </summary>
    public StateMap ThrowIfShared(string layer)
    {
        foreach (var state in _states)
            if (state.Kind.IsShared)
                throw new InvalidOperationException(
                    $"The {layer} layer sets '{state.Kind.Name}', which only the layers shared by all tracks may set."
                );

        return this;
    }

    /// <summary>
    ///     What every layer contributed to the kind's value, recorded while a <see cref="StateTrace" /> ran; empty
    ///     when none did or the kind has its default value.
    /// </summary>
    public ImmutableArray<StateContribution> Explain(IStateKind kind)
    {
        return GetState(kind).Contributions;
    }

    public StateMap Subset(IEnumerable<IStateKind> stateKinds)
    {
        var kinds = stateKinds.ToHashSet();
        return Filter(x => kinds.Contains(x.Kind));
    }

    public StateMap Except(IEnumerable<IStateKind> stateKinds)
    {
        var kinds = stateKinds.ToHashSet();
        return Filter(x => !kinds.Contains(x.Kind));
    }

    /// <summary>
    ///     The states of both maps, those of a kind in both aggregated, this map's first. The same as
    ///     <see cref="FromStates" /> of both maps' states, in one pass over the two ordered maps.
    /// </summary>
    public StateMap MergeWith(StateMap other)
    {
        if (other.IsDefault)
            return this;
        if (IsDefault)
            return other;

        var first = _states.AsSpan();
        var second = other._states.AsSpan();
        var result = new IState[first.Length + second.Length];
        var count = 0;
        int i = 0, j = 0;
        while (i < first.Length && j < second.Length)
        {
            var comparison = CompareKinds(first[i].Kind, second[j].Kind);
            if (comparison < 0)
            {
                result[count++] = first[i++];
            }
            else if (comparison > 0)
            {
                result[count++] = second[j++];
            }
            else
            {
                var state = first[i].Kind.AggregateStates([first[i], second[j]]);
                if (!state.IsDefault)
                    result[count++] = state;
                i++;
                j++;
            }
        }

        while (i < first.Length)
            result[count++] = first[i++];
        while (j < second.Length)
            result[count++] = second[j++];

        if (count == 0)
            return Default;

        return new StateMap(
            ImmutableCollectionsMarshal.AsImmutableArray(count == result.Length ? result : result[..count])
        );
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
        if (_hashCode == 0)
        {
            var hashCode = new HashCode();
            foreach (var state in _states)
                hashCode.Add(state.GetHashCode());
            // a single write, so that another thread sees either nothing or the whole of it
            _hashCode = hashCode.ToHashCode() is var value and not 0 ? value : 1;
        }

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
        var maps = stateMaps.AsImmutableArray();
        return maps.Length switch
        {
            0 => Default,
            1 => maps[0],
            2 => maps[0].MergeWith(maps[1]),
            _ => FromStates(maps.SelectMany(x => x.States))
        };
    }

    /// <summary>
    ///     Kinds in order of name, which is a kind's identity, compared by ordinal so that the order is the same
    ///     whatever the culture.
    /// </summary>
    private static int CompareKinds(IStateKind first, IStateKind second)
    {
        return ReferenceEquals(first, second) ? 0 : string.CompareOrdinal(first.Name, second.Name);
    }

    /// <summary>Sorts the states by kind, keeping those of the same kind in the order they come in.</summary>
    private static void SortByKind(IState[] items)
    {
        if (items.Length > InsertionSortMaxLength)
        {
            var sorted = items.OrderBy(x => x, Comparer<IState>.Create((x, y) => CompareKinds(x.Kind, y.Kind))).ToArray();
            sorted.CopyTo(items, 0);
            return;
        }

        for (var i = 1; i < items.Length; i++)
        {
            var item = items[i];
            var j = i - 1;
            while (j >= 0 && CompareKinds(items[j].Kind, item.Kind) > 0)
            {
                items[j + 1] = items[j];
                j--;
            }

            items[j + 1] = item;
        }
    }

    private int IndexOf(IStateKind kind)
    {
        var low = 0;
        var high = _states.Length - 1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            var comparison = CompareKinds(_states[middle].Kind, kind);
            if (comparison == 0)
                return middle;
            if (comparison < 0)
                low = middle + 1;
            else
                high = middle - 1;
        }

        return -1;
    }

    /// <summary>The states that pass, which are already ordered, aggregated and none of them default.</summary>
    private StateMap Filter(Func<IState, bool> predicate)
    {
        var result = new IState[_states.Length];
        var count = 0;
        foreach (var state in _states)
            if (predicate(state))
                result[count++] = state;

        if (count == 0)
            return Default;
        if (count == _states.Length)
            return this;

        return new StateMap(ImmutableCollectionsMarshal.AsImmutableArray(result[..count]));
    }
}
