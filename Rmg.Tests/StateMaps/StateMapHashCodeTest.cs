using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Tests.StateMaps;

public sealed class StateMapHashCodeTest
{
    private static readonly StateKind<ImmutableArray<int>> ScaleOffsets = StateKinds.ScaleOffsets;

    private static StateMap ScaleMap(params int[] values) =>
        StateMap.FromStates([ScaleOffsets.CreateState([..values])]);

    [Test]
    public async Task CollectionStates_WithEqualContent_AreEqual()
    {
        await Assert.That(ScaleMap(1, 2).Equals(ScaleMap(1, 2))).IsTrue();
    }

    [Test]
    public async Task CollectionStates_WithEqualContent_HaveSameHashCode()
    {
        await Assert.That(ScaleMap(1, 2).GetHashCode()).IsEqualTo(ScaleMap(1, 2).GetHashCode());
    }

    [Test]
    public async Task CollectionStates_WithEqualContent_AreFoundInHashSet()
    {
        var set = new HashSet<StateMap> { ScaleMap(1, 2) };

        await Assert.That(set.Contains(ScaleMap(1, 2))).IsTrue();
    }

    [Test]
    public async Task CollectionStates_WithEqualContent_AreUsableAsDictionaryKeys()
    {
        var dictionary = new Dictionary<StateMap, string> { [ScaleMap(3, 4)] = "value" };

        await Assert.That(dictionary.ContainsKey(ScaleMap(3, 4))).IsTrue();
    }

    [Test]
    public async Task State_WithEqualCollectionContent_HasSameHashCode()
    {
        var a = ScaleOffsets.CreateState([1, 2]);
        var b = ScaleOffsets.CreateState([1, 2]);

        await Assert.That(a.Equals(b)).IsTrue();
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }
}
