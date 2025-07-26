using System.Collections.Immutable;

namespace Rmg.Core.Events;

public sealed class StateTimelineMap : ITimelineLike<StateTimelineMap>
{
    private readonly ImmutableDictionary<IStateKind, IStateTimeline> _stateTimelineDictionary;

    private StateTimelineMap(
        double duration,
        ImmutableDictionary<IStateKind, IStateTimeline> stateTimelineDictionary,
        EventTimeline<StateMap> stateMapEventTimeline
    )
    {
        Duration = duration;
        _stateTimelineDictionary = stateTimelineDictionary;
        StateMapEventTimeline = stateMapEventTimeline;

        StateTimelines = [..stateTimelineDictionary.Values.OrderBy(x => x.StateKind.Name)];
    }

    public ImmutableArray<IStateTimeline> StateTimelines { get; }

    public EventTimeline<StateMap> StateMapEventTimeline { get; }

    public static StateTimelineMap Empty { get; } =
        new(0, ImmutableDictionary<IStateKind, IStateTimeline>.Empty, EventTimeline<StateMap>.Empty);

    public static StateTimelineMap Merge(IEnumerable<StateTimelineMap> timelines)
    {
        var timelineArray = timelines.AsImmutableArray();
        if (timelineArray.IsEmpty)
            return Empty;

        var duration = timelineArray.Max(x => x.Duration);
        return Create(duration, timelineArray.SelectMany(x => x.StateTimelines));
    }

    public double Duration { get; }

    public bool IsEmpty => StateTimelines.IsEmpty;

    public StateTimelineMap Trim(double duration)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (Duration == duration)
            return this;

        if (duration == 0)
            return Empty;

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

    public static StateTimelineMap Create(double duration, IEnumerable<IStateTimeline> stateTimelines)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration, nameof(duration));

        if (duration == 0)
            return Empty;

        var mergedStateTimelines = StateTimeline.Merge(stateTimelines.Select(x => x.Trim(duration)));

        if (mergedStateTimelines.IsEmpty)
            return Empty;

        var stateTimelineDictionary = mergedStateTimelines.ToImmutableDictionary(x => x.StateKind);
        var stateMapEventTimeline = StateTimelinesToStateMapEventTimeline(mergedStateTimelines, duration);

        return new StateTimelineMap(duration, stateTimelineDictionary, stateMapEventTimeline);
    }

    public StateTimeline<T> GetStateTimeline<T>(StateKind<T> stateKind)
        where T : notnull
    {
        return _stateTimelineDictionary.TryGetValue(stateKind, out var stateTimeline)
            ? (StateTimeline<T>)stateTimeline
            : stateKind.EmptyTimeline;
    }

    public StateMap GetEffectiveStateMapAt(double position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);

        if (IsEmpty || position >= Duration || position < StateMapEventTimeline[0].Position)
            return StateMap.Default;

        var index = StateMapEventTimeline.ToImmutableArray().GetIndexAtFloor(position);
        return StateMapEventTimeline[index].Value;
    }

    public StateTimelineMap MergeStateMap(StateMap stateMap)
    {
        if (IsEmpty)
            return Empty;

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
            return EventTimeline.Empty<StateMap>();

        var positions = mergedStateTimelines
            .SelectMany(x => x.Positions)
            .Distinct()
            .Order();

        var items =
            ImmutableArray.CreateBuilder<TimelineItem<StateMap>>(mergedStateTimelines.Sum(x => x.Positions.Length));
        foreach (var position in positions)
        {
            var stateMap = mergedStateTimelines
                .Select(x => x.GetEffectiveStateAt(position))
                .ToStateMap();
            items.Add(stateMap.ToTimelineItem(position));
        }

        return EventTimeline<StateMap>.Create(duration, items.ToImmutable());
    }
}
