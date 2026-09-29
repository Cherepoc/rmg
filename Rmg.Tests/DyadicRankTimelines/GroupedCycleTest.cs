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
        await Assert.That(ResolvedRhythm.RestartOf(1)).IsEqualTo(4);

        var positions = DyadicRankTimeline.Generate(4, 0, 0.75, 0, ResolvedRhythm.RestartOf(0.75), ResolvedRhythm.SplitOf(0.75)).Select(x => x.Position).ToArray();
        await Assert.That(positions).IsEquivalentTo([0, 0.75, 1.5, 2, 2.75, 3.5]);
    }

    [Test]
    [Arguments(0.75, 3, 1)]
    [Arguments(1.5, 3, 2)]
    [Arguments(3, 3, 2)]
    [Arguments(1.25, 5, 1)]
    [Arguments(1, 2, 2)]
    [Arguments(4 / 3.0, 2, 2)]
    public async Task AGroupedCycle_SplitsIntoItsOddNumberOfParts_ThenHalvesAsFarAsTheGrid(double period, int split, int gridRankLimit)
    {
        await Assert.That(ResolvedRhythm.SplitOf(period)).IsEqualTo(split);
        await Assert.That(ResolvedRhythm.GridRankLimit(period)).IsEqualTo(gridRankLimit);
    }

    [Test]
    public async Task ADottedQuarter_SplitsIntoThree8ths_ItsFirstStrong_AndThenInto16ths()
    {
        var slots = DyadicRankTimeline.Generate(1.5, 0, 1.5, 2, 1.5, ResolvedRhythm.SplitOf(1.5)).ToArray();

        await Assert.That(slots.Select(x => x.Position)).IsEquivalentTo([0, 0.25, 0.5, 0.75, 1, 1.25]);
        await Assert.That(slots.Select(x => x.Value)).IsEquivalentTo([0, 2, 1, 2, 1, 2]);
    }
}
