using Rmg.Core.Events;

namespace Rmg.Core.Probabilities;

public sealed class DyadicRankThresholdPattern
{
    private DyadicRankThresholdPattern(int maxRank, EventTimeline<KeptBeat> outcomeTimeline)
    {
        MaxRank = maxRank;
        OutcomeTimeline = outcomeTimeline;
        OutcomeRankTimeline = outcomeTimeline.MapValues(x => x.Rank);
    }

    /// <summary>The weakest rank a beat of the pattern can have; the strongest is 0.</summary>
    public int MaxRank { get; }

    /// <summary>The beats kept, each with where it is in its cycle and the cycle it plays the draw of.</summary>
    public EventTimeline<KeptBeat> OutcomeTimeline { get; }

    /// <summary>The ranks of the beats kept.</summary>
    public EventTimeline<int> OutcomeRankTimeline { get; }

    /// <param name="rankWeightFunc">The chance of keeping a position of a rank.</param>
    /// <param name="variation">
    ///     The chance of a cycle drawing afresh what it keeps, rather than keeping what the cycle before kept: 1 draws
    ///     every cycle afresh, and 0 repeats the first cycle all along, as a riff; a cycle of one rank then is a pulse.
    /// </param>
    public static DyadicRankThresholdPattern Create(
        IGenerationContext generationContext,
        int generationSeed,
        Func<int, double> rankWeightFunc,
        DyadicTimelineDescriptor descriptor,
        double variation = 1
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(variation);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(variation, 1);

        var seededGenerationContext = generationContext.CreateContext((uint)generationSeed);
        // every slot's last draw, and the cycle it was drawn in
        var decisions = new Dictionary<int, (bool IsKept, int Cycle)>();
        var kept = new List<TimelineItem<KeptBeat>>();
        var cycle = -1;
        var isRedrawn = true;
        foreach (var slot in DyadicRankTimeline.GenerateSlots(descriptor.Duration, descriptor.Cycles, descriptor.MaxRank))
        {
            if (slot.Cycle != cycle)
            {
                cycle = slot.Cycle;
                // the first cycle is always drawn; a later one by the variation, which at 1 draws without a draw
                isRedrawn = cycle == 0 || seededGenerationContext.TestProbability(variation);
            }

            if (isRedrawn || !decisions.TryGetValue(slot.Slot, out var decision))
            {
                decision = (seededGenerationContext.TestProbability(rankWeightFunc(slot.Rank)), cycle);
                decisions[slot.Slot] = decision;
            }

            if (decision.IsKept)
                kept.Add(new TimelineItem<KeptBeat>(slot.Position, new KeptBeat(slot.Rank, slot.Slot, decision.Cycle, cycle)));
        }

        return new DyadicRankThresholdPattern(descriptor.MaxRank, EventTimeline.Create(descriptor.Duration, kept));
    }
}

/// <summary>A beat a pattern keeps.</summary>
/// <param name="Rank">How strong the beat is, 0 the strongest.</param>
/// <param name="Slot">Where in its cycle the beat is, the same in every cycle.</param>
/// <param name="Source">
///     The cycle whose draw the beat plays: its own where the cycle was drawn afresh, or the last one drawn where it
///     repeats it, so that the beats of a repeated cycle are those of the cycle it repeats.
/// </param>
/// <param name="Cycle">The cycle the beat is in.</param>
public readonly record struct KeptBeat(int Rank, int Slot, int Source, int Cycle)
{
    /// <summary>Whether the beat repeats one of an earlier cycle, which it plays as that one played.</summary>
    public bool IsRepeated => Source < Cycle;
}
