using System.Collections.Concurrent;
using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Core.Probabilities;

public sealed class DyadicRankTimeline
{
    // the templates of a cycle's positions in [0, 1), by how it splits first and its weakest rank
    private static readonly ConcurrentDictionary<(int Split, int MaxRank), EventTimeline<int>> RankTimelines = new();

    /// <summary>
    ///     A cycle's positions, each with its rank: its start the strongest, then the parts it splits into first, then
    ///     every part halved again and again, each halving a rank weaker. Split in two, it is the dyadic cycle: its middle,
    ///     then its quarters, then its 8ths.
    /// </summary>
    private static EventTimeline<int> BuildRankTimeline(int split, int maxRank)
    {
        var items = new List<TimelineItem<int>> { new(0, 0) };
        if (maxRank >= 1)
            items.AddRange(Enumerable.Range(1, split - 1).Select(x => new TimelineItem<int>(x / (double)split, 1)));

        var parts = split;
        for (var rank = 2; rank <= maxRank; rank++, parts *= 2)
            items.AddRange(Enumerable.Range(0, parts).Select(x => new TimelineItem<int>((x + 0.5) / parts, rank)));

        return EventTimeline.Create(1, items.OrderBy(x => x.Position));
    }

    public static EventTimeline<int> Generate(double duration, double phase, double period, int maxRank, double restart, int split)
    {
        return EventTimeline.Create(
            duration,
            GenerateSlots(duration, phase, period, maxRank, restart, split).Select(x => new TimelineItem<int>(x.Position, x.Rank))
        );
    }

    /// <summary>
    ///     The positions of the cycles in the duration, in order, each with its rank, the cycle it is in and its slot
    ///     in the cycle, which is the same slot in every cycle. The cycles start again every restart, where the last of
    ///     them is cut off, as they are at the duration's end.
    /// </summary>
    /// <param name="restart">How often the cycles start again, such as every half bar; the duration for never.</param>
    /// <param name="split">How many parts a cycle splits into first, before every part halves.</param>
    public static ImmutableArray<DyadicRankSlot> GenerateSlots(double duration, double phase, double period, int maxRank, double restart, int split)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(restart);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRank);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxRank, DyadicRankDistribution.MaxRank);
        ArgumentOutOfRangeException.ThrowIfLessThan(split, 2);

        var templateTimeline = RankTimelines.GetOrAdd((split, maxRank), x => BuildRankTimeline(x.Split, x.MaxRank))
            .Stretch(period)
            .PhaseShift(phase);

        var cycleCount = (int)Math.Ceiling(Math.Min(restart, duration) / period);
        var slots = new List<DyadicRankSlot>();
        var cycle = 0;
        for (var start = 0.0; start < duration - 1e-9; start += restart)
        {
            var end = Math.Min(start + restart, duration);
            for (var inSpan = 0; inSpan < cycleCount; inSpan++, cycle++)
            {
                var shifted = templateTimeline.Shift(start + inSpan * period);
                for (var slot = 0; slot < shifted.Count; slot++)
                    if (shifted[slot].Position < end)
                        slots.Add(new DyadicRankSlot(shifted[slot].Position, shifted[slot].Value, cycle, slot));
            }
        }

        // the positions are multiples of a period that can be a tuplet's, so they are snapped to the grid; one that
        // snaps to the end is the next pattern's first beat, which that pattern plays
        return
        [
            ..slots
                .OrderBy(x => x.Position)
                .Select(x => x with { Position = TimelineGrid.Snap(x.Position) })
                .Where(x => x.Position >= 0 && x.Position < duration)
        ];
    }
}

/// <param name="Rank">How strong the position is, 0 the strongest.</param>
/// <param name="Cycle">Which repetition of the cycle the position is in, from 0.</param>
/// <param name="Slot">Where in its cycle the position is, the same in every repetition.</param>
public readonly record struct DyadicRankSlot(double Position, int Rank, int Cycle, int Slot);
