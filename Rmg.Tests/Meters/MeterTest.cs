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
            // a grouped cycle started again every smallest power of two holding two of it, any other at the bar
            var restart = ResolvedRhythm.IsGrouped(period) ? Math.Min(4, Math.Pow(2, Math.Ceiling(Math.Log2(2 * period) - 1e-9))) : 4;
            var before = DyadicRankTimeline.GenerateSlots(4, phase, period, maxRank, restart, ResolvedRhythm.SplitOf(period));
            var now = DyadicRankTimeline.GenerateSlots(4, Meter.FourFour.GetCycles(period, phase), maxRank);
            await Assert.That(now.SequenceEqual(before)).IsTrue().Because($"period {period}, phase {phase}, rank {maxRank}");
        }
    }

    [Test]
    [Arguments(new[] { 8, 8 }, new[] { 1.0, 3.0 })]
    [Arguments(new[] { 4, 4, 4 }, new[] { 1.0, 2.0 })]
    [Arguments(new[] { 6, 6 }, new[] { 1.5 })]
    [Arguments(new[] { 4, 4, 4, 3 }, new[] { 1.0, 2.0, 3.0 })]
    [Arguments(new[] { 4, 3, 3, 3 }, new[] { 1.0, 1.75, 2.5 })]
    [Arguments(new[] { 4, 4, 6 }, new[] { 1.0, 2.0 })]
    [Arguments(new[] { 6, 4 }, new[] { 1.5 })]
    [Arguments(new[] { 12, 8 }, new[] { 1.0, 2.0, 4.0 })]
    [Arguments(new[] { 4, 6, 6, 6 }, new[] { 1.0, 2.5, 4.0 })]
    public async Task TheBackbeat_StrikesTheBarsOtherGroups(int[] groups, double[] beats)
    {
        // the backbeat's part: half a bar in four, half its cycle late
        var meter = new Meter([..groups]);
        var slots = DyadicRankTimeline.GenerateSlots(meter.BarDuration, meter.GetCycles(2, 1), 0);

        await Assert.That(slots.Select(x => x.Position).ToArray()).IsEquivalentTo(beats);
    }

    [Test]
    [Arguments(new[] { 8, 8 }, 2)]
    [Arguments(new[] { 4, 4, 4 }, 1)]
    [Arguments(new[] { 6, 6 }, 1)]
    [Arguments(new[] { 4, 4, 4, 3 }, 1)]
    public async Task ThePulse_IsTheLevelNearestABeat(int[] groups, int tactus)
    {
        await Assert.That(new Meter([..groups]).Tactus).IsEqualTo(tactus);
    }

    [Test]
    [Arguments(new[] { 6, 6 }, new[] { 0, 0.75, 1.5, 2.25 })]
    [Arguments(new[] { 4, 4, 4 }, new[] { 0, 0.75, 1.5, 2.25 })]
    [Arguments(new[] { 4, 4, 4, 3 }, new[] { 0, 0.75, 1.5, 2.25, 3 })]
    [Arguments(new[] { 8, 8 }, new[] { 0, 0.75, 1.5, 2, 2.75, 3.5 })]
    public async Task DottedEighths_RunOnFromEveryNodeThatHoldsTwo(int[] groups, double[] starts)
    {
        var cycles = new Meter([..groups]).GetCycles(0.75, 0);

        await Assert.That(cycles.Select(x => x.Start).ToArray()).IsEquivalentTo(starts);
    }

    [Test]
    [Arguments(new[] { 8, 8 }, 1 / 3.0, 12)]
    [Arguments(new[] { 6, 6 }, 0.5, 6)]
    [Arguments(new[] { 4, 4, 4 }, 1 / 3.0, 9)]
    public async Task EighthTriplets_PlayThreeOverEveryPulse(int[] groups, double length, int count)
    {
        var cycles = new Meter([..groups]).GetCycles(1 / 3.0, 0);

        await Assert.That(cycles.Length).IsEqualTo(count);
        await Assert.That(cycles.All(x => Math.Abs(x.Length - length) < 1e-9)).IsTrue();
    }

    [Test]
    public async Task TheMeters_DrawTheirGroupsInEveryOrder()
    {
        var context = new Rmg.Core.Probabilities.GenerationContext(1);
        var sevens = Enumerable.Range(0, 3000).Select(_ => Meter.Draw(context, 1)).Where(x => x.Sixteenths == 14).Select(x => string.Join("+", x.Groups)).Distinct().Order().ToArray();

        await Assert.That(sevens).IsEquivalentTo(["4+4+6", "4+6+4", "6+4+4"]);
    }

    [Test]
    public async Task ThePlainestSongs_AreInFour_AndTheWildest_NeverInFour()
    {
        var context = new Rmg.Core.Probabilities.GenerationContext(1);
        var plain = Enumerable.Range(0, 3000).Select(_ => Meter.Draw(context, 0)).ToArray();
        var wild = Enumerable.Range(0, 3000).Select(_ => Meter.Draw(context, 1)).ToArray();

        await Assert.That(plain.All(x => x == Meter.FourFour)).IsTrue();
        await Assert.That(wild.All(x => x != Meter.FourFour)).IsTrue();
    }
}
