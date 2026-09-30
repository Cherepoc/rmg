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
