using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Core.Probabilities;

public sealed class DyadicRankTimeline
{
    private static readonly ImmutableArray<EventTimeline<int>> RankTimelines = BuildRankTimelines();

    private static ImmutableArray<EventTimeline<int>> BuildRankTimelines()
    {
        var result = new EventTimeline<int>[DyadicRankDistribution.MaxRank + 1];

        for (var i = 0; i < result.Length; i++)
        {
            var dyadicDistributionItems = DyadicRankDistribution.GetCombinedHalfDistributionByRank(i);
            var timelineItems = dyadicDistributionItems.Select(x => new TimelineItem<int>(x.Position, x.Rank));
            result[i] = EventTimeline.Create(1, timelineItems);
        }

        return [..result];
    }

    public static EventTimeline<int> Generate(double duration, double phase, double period, int maxRank)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRank);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxRank, RankTimelines.Length - 1);

        var templateTimeline = RankTimelines[maxRank]
            .Stretch(period)
            .PhaseShift(phase);

        var itemCount = (int)Math.Ceiling(duration / period);
        var timelinesToMerge = new EventTimeline<int>[itemCount];
        for (var i = 0; i < itemCount; i++)
            timelinesToMerge[i] = templateTimeline.Shift(i * period);

        // the positions are multiples of a period that can be a tuplet's, so they are snapped to the grid; one that
        // snaps to the end is the next pattern's first beat, which that pattern plays
        var merged = EventTimeline.Merge(timelinesToMerge).Trim(duration);
        return EventTimeline.Create(
            duration,
            merged.Select(x => new TimelineItem<int>(TimelineGrid.Snap(x.Position), x.Value))
        );
    }
}
