using Rmg.Core.Probabilities;
using Rmg.Core.Composition;

namespace Rmg.Tests.Meters;

public sealed class MeterTest
{
    [Test]
    [Arguments(new[] { 8, 8 }, 4, 4, 4.0)]
    [Arguments(new[] { 4, 4, 4 }, 3, 4, 3.0)]
    [Arguments(new[] { 6, 6 }, 6, 8, 3.0)]
    [Arguments(new[] { 4, 4, 6 }, 7, 8, 3.5)]
    [Arguments(new[] { 4, 4, 4, 3 }, 15, 16, 3.75)]
    [Arguments(new[] { 3, 3, 3, 3, 3 }, 15, 16, 3.75)]
    public async Task AMeter_WritesItsTimeSignature_AndLastsItsSixteenths(int[] groups, int numerator, int denominator, double bar)
    {
        var meter = new Meter([..groups]);

        await Assert.That(meter.TimeSignature).IsEqualTo((numerator, denominator));
        await Assert.That(meter.BarDuration).IsEqualTo(bar);
        await Assert.That(meter.PatternDuration).IsEqualTo(4 * bar);
    }

    [Test]
    public async Task Meters_OfTheSameGroups_AreEqual()
    {
        await Assert.That(new Meter([8, 8])).IsEqualTo(Meter.FourFour);
        await Assert.That(new Meter([4, 4, 4, 3])).IsNotEqualTo(new Meter([3, 3, 3, 3, 3]));
    }
}

public sealed class MeterTreeTest
{
    private static int[] LengthsOf(Meter meter, int level) => [..meter.Levels[level].Select(x => x.Length)];

    [Test]
    public async Task TheTree_SplitsTheBarIntoItsGroups_AndEveryNodeByItsOddStepsFirst()
    {
        var threeFour = new Meter([4, 4, 4]);
        var sixEight = new Meter([6, 6]);
        var fifteen = new Meter([4, 4, 4, 3]);

        await Assert.That(LengthsOf(Meter.FourFour, 1)).IsEquivalentTo(new[] { 8, 8 });
        await Assert.That(LengthsOf(Meter.FourFour, 2)).IsEquivalentTo(new[] { 4, 4, 4, 4 });
        await Assert.That(LengthsOf(threeFour, 1)).IsEquivalentTo(new[] { 4, 4, 4 });
        await Assert.That(LengthsOf(sixEight, 2)).IsEquivalentTo(new[] { 2, 2, 2, 2, 2, 2 });
        await Assert.That(LengthsOf(fifteen, 2)).IsEquivalentTo(new[] { 2, 2, 2, 2, 2, 2, 1, 1, 1 });
        await Assert.That(fifteen.Levels[^1].All(x => x.Length == 1)).IsTrue();
    }

    [Test]
    public async Task AStraightPeriod_PlaysOnTheNodesNearestItsLength()
    {
        double[] Starts(Meter meter, double period) => [..meter.GetCycles(period, 0).Select(x => x.Start)];

        // a beat's cycle: 3/4's beats, 6/8's 8ths, the nearer of its dotted quarters and 8ths, and 15/16's groups
        await Assert.That(Starts(new Meter([4, 4, 4]), 1)).IsEquivalentTo(new[] { 0.0, 1, 2 });
        await Assert.That(Starts(new Meter([6, 6]), 0.5)).IsEquivalentTo(new[] { 0.0, 0.5, 1, 1.5, 2, 2.5 });
        await Assert.That(Starts(new Meter([4, 4, 4, 3]), 1)).IsEquivalentTo(new[] { 0.0, 1, 2, 3 });
        // a node of three 16ths plays no finer than its 16ths
        await Assert.That(new Meter([4, 4, 4, 3]).GetCycles(1, 0)[^1].RankLimit).IsEqualTo(1);
    }

    [Test]
    public async Task InFour_EveryPeriodAndPhase_PlaysTheSlotsItAlwaysDid()
    {
        foreach (var power in new[] { -3, -2, -1, 0, 1 })
        foreach (var primeIndex in Enumerable.Range(-5, 11))
        foreach (var phaseFraction in new[] { 0, 0.25, 0.5, 0.75 })
        foreach (var maxRank in new[] { 0, 1, 2, 3 })
        {
            var period = Math.Pow(2, power) * primeIndex.ToRhythmPeriodValue() * Meter.ReferenceBar;
            var phase = phaseFraction * period;
            var before = DyadicRankTimeline.GenerateSlots(4, phase, period, maxRank, ResolvedRhythm.RestartOf(period, 4), ResolvedRhythm.SplitOf(period));
            var now = DyadicRankTimeline.GenerateSlots(4, Meter.FourFour.GetCycles(period, phase), maxRank);
            await Assert.That(now.SequenceEqual(before)).IsTrue().Because($"period {period}, phase {phase}, rank {maxRank}");
        }
    }
}
