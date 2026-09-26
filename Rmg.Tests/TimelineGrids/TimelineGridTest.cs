using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.TimelineGrids;

public sealed class TimelineGridTest
{
    [Test]
    [Arguments(0.0)]
    [Arguments(1.0)]
    [Arguments(0.25)]
    [Arguments(1.0 / 1024)]
    [Arguments(4.0 / 3)]
    [Arguments(2.0 / 5)]
    [Arguments(8.0 / 7)]
    public async Task Snap_KeepsAGridPosition(double position)
    {
        var snapped = TimelineGrid.Snap(position);

        await Assert.That(snapped).IsEqualTo(position).Within(1e-12);
        await Assert.That(TimelineGrid.Snap(snapped)).IsEqualTo(snapped);
    }

    [Test]
    public async Task Snap_MakesTwoWaysToTheSameMomentOne()
    {
        // 0.1 + 0.2 is a hair off 0.3 in binary
        await Assert.That(0.1 + 0.2).IsNotEqualTo(0.3);
        await Assert.That(TimelineGrid.Snap(0.1 + 0.2)).IsEqualTo(TimelineGrid.Snap(0.3));
    }

    [Test]
    public async Task RhythmPositions_OfAllPeriods_AreEitherEqualOrApart()
    {
        // every period and phase the generator can give a 4-beat pattern: powers of 2 times the tuplet ratios
        var positions = new List<double>();
        for (var power = -2; power <= 1; power++)
        for (var primeIndex = -RhythmPeriod.MaxPrimeIndex; primeIndex <= RhythmPeriod.MaxPrimeIndex; primeIndex++)
        for (var phaseRank = 0; phaseRank <= 2; phaseRank++)
        {
            var period = Math.Pow(2, power) * primeIndex.ToRhythmPeriodValue();
            var phase = DyadicRankDistribution.GetHalfOffset(phaseRank, 0.5) * period;
            positions.AddRange(DyadicRankTimeline.Generate(4, phase * 4, period * 4, 2).Select(x => x.Position));
        }

        var sorted = positions.Distinct().Order().ToArray();
        var nearPairs = sorted.Zip(sorted.Skip(1)).Count(x => x.Second - x.First < 1e-6);

        await Assert.That(sorted.Length).IsGreaterThan(100);
        await Assert.That(nearPairs).IsEqualTo(0);
        await Assert.That(sorted.All(x => TimelineGrid.Snap(x) == x)).IsTrue();
    }
}
