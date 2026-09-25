using System.Collections.Immutable;
using Rmg.Core;

namespace Rmg.Tests.ArrayExtensions;

public sealed class ArrayExtensionsTest
{
    private static readonly ImmutableArray<int> Sorted = [1, 2, 4];

    [Test]
    [Arguments(0, -1)]
    [Arguments(1, 0)]
    [Arguments(2, 1)]
    [Arguments(3, 1)]
    [Arguments(4, 2)]
    [Arguments(5, 2)]
    public async Task BinarySearchFloor_ResultsIn_IndexOfLargestItemNotAboveValue(int value, int expected)
    {
        await Assert.That(Sorted.BinarySearchFloor(value)).IsEqualTo(expected);
    }

    [Test]
    public async Task BinarySearchFloor_Empty_ResultsIn_MinusOne()
    {
        await Assert.That(ImmutableArray<int>.Empty.BinarySearchFloor(1)).IsEqualTo(-1);
    }

    [Test]
    [Arguments(0, 0)]
    [Arguments(1, 0)]
    [Arguments(2, 1)]
    [Arguments(3, 2)]
    [Arguments(4, 2)]
    [Arguments(5, -1)]
    public async Task BinarySearchCeiling_ResultsIn_IndexOfSmallestItemNotBelowValue(int value, int expected)
    {
        await Assert.That(Sorted.BinarySearchCeiling(value)).IsEqualTo(expected);
    }

    [Test]
    public async Task BinarySearchCeiling_Empty_ResultsIn_MinusOne()
    {
        await Assert.That(ImmutableArray<int>.Empty.BinarySearchCeiling(1)).IsEqualTo(-1);
    }
}
