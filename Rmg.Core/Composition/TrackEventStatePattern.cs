using Rmg.Core.Events;

namespace Rmg.Core.Composition;

public sealed class TrackEventStatePattern<T>
    where T : notnull
{
    public static TrackEventStatePattern<T> Create(
        double duration,
        IEnumerable<KeyValuePair<int, int>> trackIndexMap,
        TrackEventStateTimelineMap<WithStateMap<EventStatePattern<T>>> trackPatternTimelineMap,
        EventTimeline<WithStateMap<TrackEventStatePattern<T>>> trackPatternMapTimeline
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration);

        var trackIndexDictionary = trackIndexMap.ToDictionary();

        var unwrappedTrackPatternTimelineMap = trackPatternTimelineMap
            .MapTrackTimelines(eventStateTimelineMap => eventStateTimelineMap
                .MapEventTimeline(eventTimeline => eventTimeline.MapValues(x => x.Value.MergeStateMap(x.StateMap)))
                .Unwrap()
            );
        var unwrappedTrackPatternMapTimeline = trackPatternMapTimeline
            .MapValues(x => x.Value.MergeStateMap(x.StateMap))
            .Unwrap();
        var mergedStateTimelineMap =
            TrackEventStateTimelineMap.Merge([unwrappedTrackPatternTimelineMap, unwrappedTrackPatternMapTimeline])
                .MapTrackIndexes(trackIndexDictionary);

        return new TrackEventStatePattern<T>(
            duration,
            trackIndexDictionary,
            trackPatternTimelineMap,
            trackPatternMapTimeline,
            mergedStateTimelineMap
        );
    }

    public double Duration { get; }

    public IReadOnlyDictionary<int, int> TrackIndexMap { get; }

    public TrackEventStateTimelineMap<WithStateMap<EventStatePattern<T>>> TrackPatternTimelineMap { get; }

    public EventTimeline<WithStateMap<TrackEventStatePattern<T>>> TrackPatternMapTimeline { get; }

    public TrackEventStateTimelineMap<T> MergedTimelineMap { get; }

    private TrackEventStatePattern(
        double duration,
        IReadOnlyDictionary<int, int> trackIndexMap,
        TrackEventStateTimelineMap<WithStateMap<EventStatePattern<T>>> trackPatternTimelineMap,
        EventTimeline<WithStateMap<TrackEventStatePattern<T>>> trackPatternMapTimeline,
        TrackEventStateTimelineMap<T> mergedTimelineMap
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration);

        Duration = duration;
        TrackIndexMap = trackIndexMap;
        TrackPatternTimelineMap = trackPatternTimelineMap;
        TrackPatternMapTimeline = trackPatternMapTimeline;
        MergedTimelineMap = mergedTimelineMap;
    }

    public TrackEventStateTimelineMap<T> MergeStateMap(StateMap stateMap)
    {
        return MergedTimelineMap.MergeStateMap(stateMap);
    }
}

public static class TrackEventStatePattern
{
    public static TrackEventStatePattern<T> Create<T>(
        double duration,
        IEnumerable<KeyValuePair<int, int>> trackIndexMap,
        TrackEventStateTimelineMap<WithStateMap<EventStatePattern<T>>> trackPatternTimelineMap,
        EventTimeline<WithStateMap<TrackEventStatePattern<T>>> trackPatternMapTimeline
    ) where T : notnull
    {
        return TrackEventStatePattern<T>.Create(
            duration,
            trackIndexMap,
            trackPatternTimelineMap,
            trackPatternMapTimeline
        );
    }
}