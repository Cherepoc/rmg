using Rmg.Core.Probabilities;

namespace Rmg.Tests.DyadicRankThresholdPatterns;

public sealed class DyadicRankItemPatternTest
{
    private static DyadicRankThresholdPattern Riff(double variation) => DyadicRankThresholdPattern.Create(
        new GenerationContext(3),
        5,
        _ => 0.6,
        // a cycle of a beat, four times in the bar, down to 16ths
        new DyadicTimelineDescriptor(4, 1, 0, 2),
        variation
    );

    [Test]
    public async Task ARepeatedCycle_PlaysTheValuesOfTheCycleItRepeats_AtItsOwnPositions()
    {
        var rhythm = Riff(0);
        var draws = 0;
        var items = DyadicRankItemPattern<(double Position, int Value)>.Create(
                new GenerationContext(1),
                rhythm,
                _ => (_, _) => (0, ++draws),
                1,
                (position, _, drawn) => (position, drawn.Value)
            )
            .GeneratedTimeline;

        var firstCycle = items.Where(x => x.Position < 1).Select(x => x.Value.Value).ToArray();
        await Assert.That(firstCycle.Length).IsGreaterThan(0);
        await Assert.That(draws).IsEqualTo(firstCycle.Length);
        foreach (var cycle in Enumerable.Range(1, 3))
        {
            var values = items.Where(x => x.Position >= cycle && x.Position < cycle + 1).ToArray();
            await Assert.That(values.Select(x => x.Value.Value).ToArray()).IsEquivalentTo(firstCycle);
            await Assert.That(values.All(x => x.Value.Position.Equals(x.Position))).IsTrue();
        }
    }

    [Test]
    public async Task CyclesDrawnAfresh_DrawTheirOwnValues()
    {
        var draws = 0;
        var items = DyadicRankItemPattern<int>.Create(
                new GenerationContext(1),
                Riff(1),
                _ => (_, _) => ++draws,
                1
            )
            .GeneratedTimeline;

        await Assert.That(items.Select(x => x.Value).Distinct().Count()).IsEqualTo(items.Count);
    }
}
