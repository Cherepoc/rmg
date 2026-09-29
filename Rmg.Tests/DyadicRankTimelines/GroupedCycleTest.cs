using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.DyadicRankTimelines;

public sealed class GroupedCycleTest
{
    [Test]
    [Arguments(0.75, true)]
    [Arguments(1.25, true)]
    [Arguments(1.5, true)]
    [Arguments(1, false)]
    [Arguments(0.5, false)]
    [Arguments(4 / 3.0, false)]
    public async Task ACycleOfANumberOf16thsNoPowerOfTwo_IsGrouped(double period, bool isGrouped)
    {
        await Assert.That(ResolvedRhythm.IsGrouped(period)).IsEqualTo(isGrouped);
    }

    [Test]
    public async Task ADotted8th_RestartsEveryHalfBar_OnTheGrid_As3And3And2()
    {
        await Assert.That(ResolvedRhythm.RestartOf(0.75)).IsEqualTo(2);
        await Assert.That(ResolvedRhythm.GridRankLimit(0.75)).IsEqualTo(0);
        await Assert.That(ResolvedRhythm.RestartOf(1)).IsEqualTo(4);
        await Assert.That(ResolvedRhythm.GridRankLimit(1)).IsEqualTo(ResolvedRhythm.MaxRankLimit);
        // a 1.5-beat cycle's halves fall on dotted 8ths, but not their halves
        await Assert.That(ResolvedRhythm.GridRankLimit(1.5)).IsEqualTo(1);

        var positions = DyadicRankTimeline.Generate(4, 0, 0.75, 0, ResolvedRhythm.RestartOf(0.75), ResolvedRhythm.SplitOf(0.75)).Select(x => x.Position).ToArray();
        await Assert.That(positions).IsEquivalentTo([0, 0.75, 1.5, 2, 2.75, 3.5]);
    }
}
