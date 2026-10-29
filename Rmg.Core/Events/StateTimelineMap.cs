using System.Collections.Immutable;

namespace Rmg.Core.Events;

public sealed class StateTimelineMap : ITimelineLike<StateTimelineMap>
{
    private readonly ImmutableDictionary<IStateKind, IStateTimeline> _stateTimelineDictionary;

    // the state at every change, built when a state is first looked up: most maps are only merged, shifted or trimmed
    // into others and never looked up
    private EventTimeline<StateMap>? _stateMapEventTimeline;

    private StateTimelineMap(double duration, ImmutableDictionary<IStateKind, IStateTimeline> stateTimelineDictionary)
    {
        Duration = duration;
        _stateTimelineDictionary = stateTimelineDictionary;

        // in the order a state map keeps its kinds (IStateKind.Key), so that the states read at a place are a map as they come
        StateTimelines = [..stateTimelineDictionary.Values.OrderBy(x => x.StateKind.Key)];
    }

    public ImmutableArray<IStateTimeline> StateTimelines { get; }

    // a single reference write, so another thread sees either nothing or a whole timeline, both equal
    public EventTimeline<StateMap> StateMapEventTimeline =>
        _stateMapEventTimeline ??= StateTimelinesToStateMapEventTimeline(StateTimelines, Duration);

    private static readonly StateTimelineMap Zero = new(0, ImmutableDictionary<IStateKind, IStateTimeline>.Empty);

    public static StateTimelineMap Merge(IEnumerable<StateTimelineMap> timelines)
    {
        var timelineArray = timelines.AsImmutableArray();
        if (timelineArray.IsEmpty)
            return Zero;

        var duration = timelineArray.Max(x => x.Duration);
        return Create(duration, timelineArray.SelectMany(x => x.StateTimelines));
    }

    public double Duration { get; }

    /// <summary>Whether the map holds only default state, that is no state timelines at all.</summary>
    public bool IsDefault => StateTimelines.IsEmpty;

    public StateTimelineMap Trim(double duration)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (Duration == duration)
            return this;

        if (duration == 0)
            return Zero;

        return Create(duration, StateTimelines);
    }

    public StateTimelineMap Shift(double offset)
    {
        if (offset == 0)
            return this;

        var newDuration = Duration + offset;
        if (newDuration < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be greater then the duration.");

        return Create(newDuration, StateTimelines.Select(x => x.Shift(offset)));
    }

    /// <summary>A map of the given duration with only default state.</summary>
    public static StateTimelineMap Create(double duration)
    {
        return Create(duration, []);
    }

    public static StateTimelineMap Create(double duration, IEnumerable<IStateTimeline> stateTimelines)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration, nameof(duration));

        if (duration == 0)
            return Zero;

        var mergedStateTimelines = StateTimeline.Merge(stateTimelines.Select(x => x.Trim(duration)));

        if (mergedStateTimelines.IsEmpty)
            return new StateTimelineMap(duration, ImmutableDictionary<IStateKind, IStateTimeline>.Empty);

        return new StateTimelineMap(duration, mergedStateTimelines.ToImmutableDictionary(x => x.StateKind));
    }

    /// <summary>The timelines of the scope, such as those that <c>Render</c> reads.</summary>
    public StateTimelineMap OfScope(StateScope scope)
    {
        if (StateTimelines.All(x => x.StateKind.Scope == scope))
            return this;

        return Create(Duration, StateTimelines.Where(x => x.StateKind.Scope == scope));
    }

    /// <summary>The map without the timelines of the given kinds.</summary>
    public StateTimelineMap Except(IEnumerable<IStateKind> stateKinds)
    {
        var excluded = stateKinds as IStateKind[] ?? [..stateKinds];
        return Create(Duration, StateTimelines.Where(x => Array.IndexOf(excluded, x.StateKind) < 0));
    }

    public StateTimeline<T> GetStateTimeline<T>(StateKind<T> stateKind)
        where T : notnull
    {
        return _stateTimelineDictionary.TryGetValue(stateKind, out var stateTimeline)
            ? (StateTimeline<T>)stateTimeline
            : stateKind.CreateDefaultTimeline(Duration);
    }

    /// <summary>
    ///     The state at a position, as <see cref="GetEffectiveStateMapAt" /> has it, read from the timelines there rather
    ///     than from the state at every change, which it builds only where that is built already: for a map read at a few
    ///     places, such as a track's notes, once each.
    /// </summary>
    public StateMap GetStateMapAt(double position)
    {
        if (_stateMapEventTimeline is not null || IsDefault || position >= Duration)
            return GetEffectiveStateMapAt(position);
        return ReadAt(position);
    }

    /// <summary>
    ///     The state at a position, as <see cref="GetStateMapAt(double)" /> has it, and where it next changes: the first of
    ///     its timelines' positions after it, or the map's end, until which a later place reads the same.
    /// </summary>
    internal StateMap GetStateMapAt(double position, out double until)
    {
        until = position >= Duration ? double.PositiveInfinity : Duration;
        foreach (var timeline in StateTimelines)
        {
            var positions = timeline.Positions;
            var next = positions.BinarySearch(position);
            next = next >= 0 ? next + 1 : ~next;
            if (next < positions.Length && positions[next] < until)
                until = positions[next];
        }

        return GetStateMapAt(position);
    }

    /// <summary>The state at a position, read from every timeline there.</summary>
    private StateMap ReadAt(double position)
    {
        var states = new IState[StateTimelines.Length];
        for (var i = 0; i < states.Length; i++)
            states[i] = StateTimelines[i].GetEffectiveStateAt(position);
        return StateMap.FromOrderedStates(states);
    }

    public StateMap GetEffectiveStateMapAt(double position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);

        if (IsDefault || position >= Duration)
            return StateMap.Default;
        // while a trace explains, every layer's contribution at the place itself, which the state at the last change of a
        // value misses where a layer comes in or goes without changing it
        if (StateTrace.IsExplaining)
            return ReadAt(position);

        var index = StateMapEventTimeline.ToImmutableArray().GetIndexAtFloor(position);
        return StateMapEventTimeline[index].Value;
    }

    public StateTimelineMap MergeStateMap(StateMap stateMap)
    {
        if (Duration == 0)
            return Zero;

        if (stateMap.IsDefault)
            return this;

        var otherStateTimelineMap = stateMap.ToStateTimelineMap(Duration);
        return Merge([this, otherStateTimelineMap]);
    }

    private static EventTimeline<StateMap> StateTimelinesToStateMapEventTimeline(
        ImmutableArray<IStateTimeline> mergedStateTimelines,
        double duration
    )
    {
        if (mergedStateTimelines.IsEmpty)
            return EventTimeline.Create<StateMap>(duration);

        var positions = mergedStateTimelines
            .SelectMany(x => x.Positions)
            .Distinct()
            .Order();

        var items =
            ImmutableArray.CreateBuilder<TimelineItem<StateMap>>(mergedStateTimelines.Sum(x => x.Positions.Length));
        // one state of every timeline, which come in their kinds' order, a kind each, as a map keeps its states
        var states = new IState[mergedStateTimelines.Length];
        foreach (var position in positions)
        {
            for (var i = 0; i < states.Length; i++)
                states[i] = mergedStateTimelines[i].GetEffectiveStateAt(position);
            items.Add(StateMap.FromOrderedStates(states).ToTimelineItem(position));
        }

        return EventTimeline<StateMap>.Create(duration, items.ToImmutable());
    }
}
