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
        return EventTimeline.Create(
            duration,
            GenerateSlots(duration, phase, period, maxRank).Select(x => new TimelineItem<int>(x.Position, x.Rank))
        );
    }

    /// <summary>
    ///     The positions of the cycles in the duration, in order, each with its rank, the cycle it is in and its slot
    ///     in the cycle, which is the same slot in every cycle.
    /// </summary>
    public static ImmutableArray<DyadicRankSlot> GenerateSlots(double duration, double phase, double period, int maxRank)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRank);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxRank, RankTimelines.Length - 1);

        var templateTimeline = RankTimelines[maxRank]
            .Stretch(period)
            .PhaseShift(phase);

        var cycleCount = (int)Math.Ceiling(duration / period);
        var slots = new List<DyadicRankSlot>();
        for (var cycle = 0; cycle < cycleCount; cycle++)
        {
            var shifted = templateTimeline.Shift(cycle * period);
            for (var slot = 0; slot < shifted.Count; slot++)
                slots.Add(new DyadicRankSlot(shifted[slot].Position, shifted[slot].Value, cycle, slot));
        }

        // the positions are multiples of a period that can be a tuplet's, so they are snapped to the grid; one that
        // snaps to the end is the next pattern's first beat, which that pattern plays
        return
        [
            ..slots
                .OrderBy(x => x.Position)
                .Where(x => x.Position < duration)
                .Select(x => x with { Position = TimelineGrid.Snap(x.Position) })
                .Where(x => x.Position >= 0 && x.Position < duration)
        ];
    }
}

/// <param name="Rank">How strong the position is, 0 the strongest.</param>
/// <param name="Cycle">Which repetition of the cycle the position is in, from 0.</param>
/// <param name="Slot">Where in its cycle the position is, the same in every repetition.</param>
public readonly record struct DyadicRankSlot(double Position, int Rank, int Cycle, int Slot);
