using Rmg.Core.Probabilities;

namespace Rmg.Tests.DyadicRankThresholdPatterns;

public sealed class DyadicRankItemPatternTest
{
    private static DyadicRankThresholdPattern Riff(double variation) => DyadicRankThresholdPattern.Create(
        new GenerationContext(3),
        5,
        _ => 0.6,
        // a cycle of a beat, four times in the bar, down to 16ths
        new DyadicTimelineDescriptor(4, 1, 0, 2, 4, 2),
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
            await Assert.That(values.Select(x => x.Value.Value).ToArray()).IsEquivalentTo(firstCycle, CollectionOrdering.Matching);
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
                1,
                (_, _, kept) => kept
            )
            .GeneratedTimeline;

        // beats in more than one cycle, so that drawing afresh is tested
        await Assert.That(items.Any(x => x.Position >= 1)).IsTrue();
        await Assert.That(items.Select(x => x.Value).Distinct().Count()).IsEqualTo(items.Count);
    }

    /// <summary>A cycle that repeats one before it, a later one redrawn among them, plays that cycle's values at its own places.</summary>
    [Test]
    public async Task ARepeatedCycle_PlaysTheValuesOfItsSource_ALaterOneAmongThem()
    {
        var sourced = 0;
        foreach (var seed in Enumerable.Range(0, 32))
        {
            var rhythm = DyadicRankThresholdPattern.Create(new GenerationContext(3), (ulong)seed, _ => 0.6, new DyadicTimelineDescriptor(4, 1, 0, 2, 4, 2), 0.5);
            var draws = 0;
            // every beat's value by its place, a cycle a beat long, and the rhythm's beats, which say where each repeats from
            var values = DyadicRankItemPattern<int>.Create(new GenerationContext(1), rhythm, _ => (_, _) => ++draws, 1, (_, _, kept) => kept).GeneratedTimeline
                .ToDictionary(x => Math.Round(x.Position, 6), x => x.Value);
            foreach (var beat in rhythm.OutcomeTimeline.Where(x => x.Value.IsRepeated))
            {
                await Assert.That(values[Math.Round(beat.Position, 6)]).IsEqualTo(values[Math.Round(beat.Position - (beat.Value.Cycle - beat.Value.Source), 6)]).Because($"seed {seed} at {beat.Position}");
                sourced += beat.Value.Source > 0 ? 1 : 0;
            }
        }

        await Assert.That(sourced).IsGreaterThan(0);
    }
}
