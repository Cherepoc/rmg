using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.DyadicRankThresholdPatterns;

public sealed class DyadicRankThresholdPatternTest
{
    private const double BarDuration = 4;

    private static Func<int, double> Weights(int rankOffset, double fullness) =>
        WeightUtil.CreateGeometricRankWeightFunc(rankOffset, fullness);

    private static double[] Positions(DyadicRankThresholdPattern pattern) =>
        [..pattern.OutcomeRankTimeline.Select(x => x.Position)];

    [Test]
    public async Task FullPattern_OnAFastCycle_IsARoll()
    {
        // a beat-long cycle halved down to 16ths, every position kept
        var pattern = DyadicRankThresholdPattern.Create(
            new GenerationContext(0),
            1,
            Weights(0, 1),
            new DyadicTimelineDescriptor(BarDuration, 1, 0, 2, BarDuration, 2),
            variation: 1
        );

        await Assert.That(Positions(pattern)).IsEquivalentTo(Enumerable.Range(0, 16).Select(x => x * 0.25).ToArray(), CollectionOrdering.Matching);
    }

    [Test]
    public async Task NoVariation_RepeatsTheFirstCycle_AsARiff()
    {
        for (var seed = 0UL; seed < 50; seed++)
        {
            var pattern = DyadicRankThresholdPattern.Create(
                new GenerationContext(0),
                seed,
                Weights(1, 0.5),
                new DyadicTimelineDescriptor(BarDuration, 1, 0, 2, BarDuration, 2),
                variation: 0
            );

            // every beat keeps what the first kept
            var cycles = Enumerable.Range(0, 4)
                .Select(beat => Positions(pattern).Where(x => x >= beat && x < beat + 1).Select(x => x - beat).ToArray())
                .ToArray();
            foreach (var cycle in cycles.Skip(1))
                await Assert.That(cycle).IsEquivalentTo(cycles[0], CollectionOrdering.Matching).Because($"seed {seed}");
        }
    }

    [Test]
    public async Task OneRankCycle_WithNoVariation_IsAPulse()
    {
        // a half-beat cycle of one rank, kept half the time: with no variation every cycle as the first, a note on every
        // 8th or none
        var pulses = Enumerable.Range(0, 50)
            .Select(seed => Positions(
                DyadicRankThresholdPattern.Create(
                    new GenerationContext(0),
                    (ulong)seed,
                    Weights(1, 0.5),
                    new DyadicTimelineDescriptor(BarDuration, 0.5, 0, 0, BarDuration, 2),
                    variation: 0
                )
            ))
            .ToArray();

        await Assert.That(pulses.All(x => x.Length is 8 or 0)).IsTrue();
        await Assert.That(pulses.Any(x => x.Length == 8) && pulses.Any(x => x.Length == 0)).IsTrue();
    }

    [Test]
    public async Task FullVariation_IsTheSameAsEveryPositionDrawnOnItsOwn()
    {
        // what the pattern did before cycles could repeat: every position kept or not by its own draw, in order
        for (var seed = 0UL; seed < 30; seed++)
        {
            var descriptor = new DyadicTimelineDescriptor(BarDuration, 4 / 3.0, 0.25, 2, BarDuration, 2);
            var weights = Weights(1, 0.5);
            var context = new GenerationContext(0).CreateContext((ulong)seed);
            var expected = DyadicRankTimeline.GenerateSlots(BarDuration, descriptor.Cycles, descriptor.MaxRank)
                .Where(x => context.TestProbability(weights(x.Rank)))
                .Select(x => x.Position)
                .ToArray();

            var pattern = DyadicRankThresholdPattern.Create(new GenerationContext(0), seed, weights, descriptor, variation: 1);

            await Assert.That(Positions(pattern)).IsEquivalentTo(expected, CollectionOrdering.Matching).Because($"seed {seed}");
        }
    }

    [Test]
    public async Task SomeVariation_RedrawsSomeCycles()
    {
        var redrawn = 0;
        for (var seed = 0UL; seed < 200; seed++)
        {
            var pattern = DyadicRankThresholdPattern.Create(
                new GenerationContext(0),
                seed,
                Weights(1, 0.5),
                new DyadicTimelineDescriptor(BarDuration, 1, 0, 2, BarDuration, 2),
                variation: 0.5
            );
            var first = Positions(pattern).Where(x => x < 1).ToArray();
            var last = Positions(pattern).Where(x => x >= 3).Select(x => x - 3).ToArray();
            if (!first.SequenceEqual(last))
                redrawn++;
        }

        // a redrawn cycle can come out the same by chance, so not every one differs, but many do
        await Assert.That(redrawn).IsGreaterThan(40);
        await Assert.That(redrawn).IsLessThan(160);
    }

    [Test]
    public async Task Slots_NameTheSamePlaceInEveryCycle()
    {
        var slots = DyadicRankTimeline.GenerateSlots(BarDuration, 0, 2, 1, BarDuration, 2);

        await Assert.That(slots.Select(x => x.Position).ToArray()).IsEquivalentTo([0.0, 1, 2, 3], CollectionOrdering.Matching);
        await Assert.That(slots.Select(x => x.Cycle).ToArray()).IsEquivalentTo([0, 0, 1, 1], CollectionOrdering.Matching);
        await Assert.That(slots.Select(x => x.Slot).ToArray()).IsEquivalentTo([0, 1, 0, 1], CollectionOrdering.Matching);
    }
}
