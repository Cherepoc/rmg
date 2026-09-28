using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.DyadicRankThresholdPatterns;

public sealed class DyadicRankThresholdPatternTest
{
    private const double BarDuration = 4;

    private static Func<int, double> Weights(int rankOffset, double fullness) =>
        WeightUtil.CreateGeometricRankWeightFunc(rankOffset, 0, 1.0, fullness);

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
            new DyadicTimelineDescriptor(BarDuration, 1, 0, 2, BarDuration)
        );

        await Assert.That(Positions(pattern)).IsEquivalentTo(Enumerable.Range(0, 16).Select(x => x * 0.25).ToArray());
    }

    [Test]
    public async Task NoVariation_RepeatsTheFirstCycle_AsARiff()
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var pattern = DyadicRankThresholdPattern.Create(
                new GenerationContext(0),
                seed,
                Weights(1, 0.5),
                new DyadicTimelineDescriptor(BarDuration, 1, 0, 2, BarDuration),
                variation: 0
            );

            // every beat keeps what the first kept
            var cycles = Enumerable.Range(0, 4)
                .Select(beat => Positions(pattern).Where(x => x >= beat && x < beat + 1).Select(x => x - beat).ToArray())
                .ToArray();
            foreach (var cycle in cycles.Skip(1))
                await Assert.That(cycle).IsEquivalentTo(cycles[0]).Because($"seed {seed}");
        }
    }

    [Test]
    public async Task OneRankCycle_WithNoVariation_IsAPulse()
    {
        // a half-beat cycle of one rank: a note on every 8th, or none at all
        var pulses = Enumerable.Range(0, 50)
            .Select(seed => Positions(
                DyadicRankThresholdPattern.Create(
                    new GenerationContext(0),
                    seed,
                    Weights(0, 0.5),
                    new DyadicTimelineDescriptor(BarDuration, 0.5, 0, 0, BarDuration),
                    variation: 0
                )
            ))
            .ToArray();

        await Assert.That(pulses.All(x => x.Length is 0 or 8)).IsTrue();
        await Assert.That(pulses.Count(x => x.Length == 8)).IsEqualTo(50);
    }

    [Test]
    public async Task FullVariation_IsTheSameAsEveryPositionDrawnOnItsOwn()
    {
        // what the pattern did before cycles could repeat: every position kept or not by its own draw, in order
        for (var seed = 0; seed < 30; seed++)
        {
            var descriptor = new DyadicTimelineDescriptor(BarDuration, 4 / 3.0, 0.25, 2, BarDuration);
            var weights = Weights(1, 0.5);
            var context = new GenerationContext(0).CreateContext(seed);
            var expected = DyadicRankTimeline.Generate(BarDuration, descriptor.Phase, descriptor.Period, descriptor.MaxRank, descriptor.Restart)
                .FilterValues(x => context.TestProbability(weights(x)))
                .Select(x => x.Position)
                .ToArray();

            var pattern = DyadicRankThresholdPattern.Create(new GenerationContext(0), seed, weights, descriptor);

            await Assert.That(Positions(pattern)).IsEquivalentTo(expected).Because($"seed {seed}");
        }
    }

    [Test]
    public async Task SomeVariation_RedrawsSomeCycles()
    {
        var redrawn = 0;
        for (var seed = 0; seed < 200; seed++)
        {
            var pattern = DyadicRankThresholdPattern.Create(
                new GenerationContext(0),
                seed,
                Weights(1, 0.5),
                new DyadicTimelineDescriptor(BarDuration, 1, 0, 2, BarDuration),
                variation: 0.5
            );
            var first = Positions(pattern).Where(x => x < 1).ToArray();
            var last = Positions(pattern).Where(x => x >= 3).Select(x => x - 3).ToArray();
            if (!first.SequenceEqual(last))
                redrawn++;
        }

        // a redrawn cycle can come out the same by chance, so fewer than half differ, but many do
        await Assert.That(redrawn).IsGreaterThan(40);
        await Assert.That(redrawn).IsLessThan(160);
    }

    [Test]
    public async Task Slots_NameTheSamePlaceInEveryCycle()
    {
        var slots = DyadicRankTimeline.GenerateSlots(BarDuration, 0, 2, 1, BarDuration);

        await Assert.That(slots.Select(x => x.Position).ToArray()).IsEquivalentTo([0.0, 1, 2, 3]);
        await Assert.That(slots.Select(x => x.Cycle).ToArray()).IsEquivalentTo([0, 0, 1, 1]);
        await Assert.That(slots.Select(x => x.Slot).ToArray()).IsEquivalentTo([0, 1, 0, 1]);
    }
}
