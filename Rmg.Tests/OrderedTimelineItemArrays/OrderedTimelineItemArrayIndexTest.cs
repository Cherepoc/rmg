using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Tests.OrderedTimelineItemArrays;

public sealed class OrderedTimelineItemArrayIndexTest
{
    // Positions 1, 2, 4
    private static readonly ImmutableArray<TimelineItem<int>> Items =
    [
        new(1, 10),
        new(2, 20),
        new(4, 40),
    ];

    [Test]
    public async Task GetIndexAtFloor_Empty_ResultsIn_MinusOne()
    {
        ImmutableArray<TimelineItem<int>> input = [];

        await Assert.That(input.GetIndexAtFloor(1)).IsEqualTo(-1);
    }

    [Test]
    [Arguments(-1, -1)]
    [Arguments(0, -1)]
    [Arguments(0.5, -1)]
    [Arguments(1, 0)]
    [Arguments(1.5, 0)]
    [Arguments(2, 1)]
    [Arguments(3, 1)]
    [Arguments(4, 2)]
    [Arguments(100, 2)]
    public async Task GetIndexAtFloor_ResultsIn_IndexOfLastItemAtOrBefore(double position, int expected)
    {
        await Assert.That(Items.GetIndexAtFloor(position)).IsEqualTo(expected);
    }

    [Test]
    public async Task GetIndexAtFloor_SingleItem()
    {
        ImmutableArray<TimelineItem<int>> input = [new(1, 10)];

        await Assert.That(input.GetIndexAtFloor(0.5)).IsEqualTo(-1);
        await Assert.That(input.GetIndexAtFloor(1)).IsEqualTo(0);
        await Assert.That(input.GetIndexAtFloor(9)).IsEqualTo(0);
    }

    [Test]
    public async Task GetIndexAtCeiling_Empty_ResultsIn_MinusOne()
    {
        ImmutableArray<TimelineItem<int>> input = [];

        await Assert.That(input.GetIndexAtCeiling(1)).IsEqualTo(-1);
    }

    [Test]
    [Arguments(-1, 0)]
    [Arguments(0, 0)]
    [Arguments(0.5, 0)]
    [Arguments(1, 0)]
    [Arguments(1.5, 1)]
    [Arguments(2, 1)]
    [Arguments(3, 2)]
    [Arguments(4, 2)]
    [Arguments(4.5, -1)]
    [Arguments(100, -1)]
    public async Task GetIndexAtCeiling_ResultsIn_IndexOfFirstItemAtOrAfter(double position, int expected)
    {
        await Assert.That(Items.GetIndexAtCeiling(position)).IsEqualTo(expected);
    }

    [Test]
    public async Task GetIndexAtCeiling_SingleItem()
    {
        ImmutableArray<TimelineItem<int>> input = [new(1, 10)];

        await Assert.That(input.GetIndexAtCeiling(0.5)).IsEqualTo(0);
        await Assert.That(input.GetIndexAtCeiling(1)).IsEqualTo(0);
        await Assert.That(input.GetIndexAtCeiling(1.5)).IsEqualTo(-1);
    }
}
