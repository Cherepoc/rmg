using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Tests.StateMaps;

public sealed class StateMapTest
{
    private static readonly StateKind<int> KeyOffset = StateKinds.KeyOffset;
    private static readonly StateKind<int> OctaveOffset = StateKinds.OctaveOffset;
    private static readonly StateKind<double> Tempo = StateKinds.Tempo;
    private static readonly StateKind<ImmutableArray<int>> ScaleOffsets = StateKinds.ScaleOffsets;

    private static StateMap Map(params IState[] states) => StateMap.FromStates(states);

    [Test]
    public async Task Default_IsDefault_AndHasNoStates()
    {
        var result = StateMap.Default;

        await Assert.That(result.IsDefault).IsTrue();
        await Assert.That(result.States.Length).IsEqualTo(0);
        await Assert.That(result.Kinds.Length).IsEqualTo(0);
    }

    [Test]
    public async Task FromStates_Empty_ResultsIn_Default()
    {
        var result = StateMap.FromStates([]);

        await Assert.That(result).IsSameReferenceAs(StateMap.Default);
    }

    [Test]
    public async Task FromStates_OnlyDefaultValues_ResultsIn_Default()
    {
        var result = Map(KeyOffset.CreateState(0), Tempo.CreateState(1));

        await Assert.That(result.IsDefault).IsTrue();
    }

    [Test]
    public async Task FromStates_DefaultValues_AreDropped()
    {
        var result = Map(KeyOffset.CreateState(2), OctaveOffset.CreateState(0));

        await Assert.That(result.Kinds.Select(x => x.Name)).IsEquivalentTo(new[] { KeyOffset.Name });
    }

    [Test]
    public async Task FromStates_SameKind_IsAggregated()
    {
        var result = Map(KeyOffset.CreateState(1), KeyOffset.CreateState(2));

        await Assert.That(result.GetStateValue(KeyOffset)).IsEqualTo(3);
    }

    [Test]
    public async Task FromStates_SameKindCancellingOut_IsDropped()
    {
        var result = Map(KeyOffset.CreateState(1), KeyOffset.CreateState(-1));

        await Assert.That(result.IsDefault).IsTrue();
    }

    [Test]
    public async Task FromStates_StatesAreOrderedByKindName()
    {
        var result = Map(Tempo.CreateState(2), OctaveOffset.CreateState(1), KeyOffset.CreateState(1));

        var names = result.Kinds.Select(x => x.Name).ToArray();
        await Assert.That(names).IsEquivalentTo(names.Order().ToArray());
        await Assert.That(names.Length).IsEqualTo(3);
    }

    [Test]
    public async Task GetStateValue_Present_ResultsIn_Value()
    {
        var input = Map(KeyOffset.CreateState(5));

        await Assert.That(input.GetStateValue(KeyOffset)).IsEqualTo(5);
    }

    [Test]
    public async Task GetStateValue_Missing_ResultsIn_KindDefault()
    {
        var input = Map(KeyOffset.CreateState(5));

        await Assert.That(input.GetStateValue(Tempo)).IsEqualTo(1);
        await Assert.That(input.GetStateValue(OctaveOffset)).IsEqualTo(0);
    }

    [Test]
    public async Task GetState_Present_ResultsIn_State()
    {
        var input = Map(KeyOffset.CreateState(5));

        await Assert.That(input.GetState(KeyOffset)).IsEqualTo(KeyOffset.CreateState(5));
        await Assert.That(input.GetState((IStateKind)KeyOffset).Value).IsEqualTo(5);
    }

    [Test]
    public async Task GetState_Missing_ResultsIn_DefaultState()
    {
        var input = Map(KeyOffset.CreateState(5));

        await Assert.That(input.GetState(OctaveOffset)).IsEqualTo(OctaveOffset.DefaultState);
        await Assert.That(input.GetState((IStateKind)OctaveOffset).IsDefault).IsTrue();
    }

    [Test]
    public async Task Subset_KeepsOnlyRequestedKinds()
    {
        var input = Map(KeyOffset.CreateState(1), OctaveOffset.CreateState(2), Tempo.CreateState(3));

        var result = input.Subset([KeyOffset, Tempo]);

        await Assert.That(result).IsEqualTo(Map(KeyOffset.CreateState(1), Tempo.CreateState(3)));
    }

    [Test]
    public async Task Subset_NoKinds_ResultsIn_Default()
    {
        var input = Map(KeyOffset.CreateState(1));

        await Assert.That(input.Subset([]).IsDefault).IsTrue();
    }

    [Test]
    public async Task Subset_KindsNotInMap_AreIgnored()
    {
        var input = Map(KeyOffset.CreateState(1));

        await Assert.That(input.Subset([KeyOffset, Tempo])).IsEqualTo(input);
    }

    [Test]
    public async Task Except_RemovesRequestedKinds()
    {
        var input = Map(KeyOffset.CreateState(1), OctaveOffset.CreateState(2), Tempo.CreateState(3));

        var result = input.Except([OctaveOffset]);

        await Assert.That(result).IsEqualTo(Map(KeyOffset.CreateState(1), Tempo.CreateState(3)));
    }

    [Test]
    public async Task Except_NoKinds_ResultsIn_SameContent()
    {
        var input = Map(KeyOffset.CreateState(1));

        await Assert.That(input.Except([])).IsEqualTo(input);
    }

    [Test]
    public async Task Except_AllKinds_ResultsIn_Default()
    {
        var input = Map(KeyOffset.CreateState(1), Tempo.CreateState(3));

        await Assert.That(input.Except([KeyOffset, Tempo]).IsDefault).IsTrue();
    }

    [Test]
    public async Task MergeWith_OtherDefault_ResultsIn_This()
    {
        var input = Map(KeyOffset.CreateState(1));

        await Assert.That(input.MergeWith(StateMap.Default)).IsSameReferenceAs(input);
    }

    [Test]
    public async Task MergeWith_ThisDefault_ResultsIn_Other()
    {
        var other = Map(KeyOffset.CreateState(1));

        await Assert.That(StateMap.Default.MergeWith(other)).IsSameReferenceAs(other);
    }

    [Test]
    public async Task MergeWith_DisjointKinds_ResultsIn_Union()
    {
        var input = Map(KeyOffset.CreateState(1));
        var other = Map(Tempo.CreateState(2));

        await Assert.That(input.MergeWith(other)).IsEqualTo(Map(KeyOffset.CreateState(1), Tempo.CreateState(2)));
    }

    [Test]
    public async Task MergeWith_SharedKinds_AreAggregated()
    {
        var input = Map(KeyOffset.CreateState(1), Tempo.CreateState(2));
        var other = Map(KeyOffset.CreateState(4), Tempo.CreateState(3));

        var result = input.MergeWith(other);

        await Assert.That(result.GetStateValue(KeyOffset)).IsEqualTo(5);
        await Assert.That(result.GetStateValue(Tempo)).IsEqualTo(6);
    }

    [Test]
    public async Task MergeWith_CollectionKinds_ConcatenatesSortedKeepingDuplicates()
    {
        var input = Map(ScaleOffsets.CreateState([3, 1]));
        var other = Map(ScaleOffsets.CreateState([2, 3]));

        var result = input.MergeWith(other);

        await Assert.That(result.GetStateValue(ScaleOffsets).ToArray()).IsEquivalentTo([1, 2, 3, 3]);
    }

    [Test]
    public async Task MergeWith_SameOffsetFromTwoLayers_KeepsBoth()
    {
        // each layer adds its own offset, and the offsets are summed when rendered
        var input = Map(StateKinds.ChordRootNoteOffset.CreateState([0.25]));
        var other = Map(StateKinds.ChordRootNoteOffset.CreateState([0.25]));

        var result = input.MergeWith(other);

        await Assert.That(result.GetStateValue(StateKinds.ChordRootNoteOffset).ToArray()).IsEquivalentTo([0.25, 0.25]);
    }

    [Test]
    public async Task Aggregate_Empty_ResultsIn_Default()
    {
        await Assert.That(StateMap.Aggregate([]).IsDefault).IsTrue();
    }

    [Test]
    public async Task Aggregate_MultipleMaps_AggregatesPerKind()
    {
        var maps = new[]
        {
            Map(KeyOffset.CreateState(1)),
            Map(KeyOffset.CreateState(2), Tempo.CreateState(2)),
            Map(Tempo.CreateState(4)),
        };

        var result = StateMap.Aggregate(maps);

        await Assert.That(result.GetStateValue(KeyOffset)).IsEqualTo(3);
        await Assert.That(result.GetStateValue(Tempo)).IsEqualTo(8);
    }

    [Test]
    public async Task Equality_SameStates_AreEqual_WithSameHashCode()
    {
        var a = Map(KeyOffset.CreateState(1), Tempo.CreateState(2));
        var b = Map(Tempo.CreateState(2), KeyOffset.CreateState(1));

        await Assert.That(a.Equals(b)).IsTrue();
        await Assert.That(a == b).IsTrue();
        await Assert.That(a != b).IsFalse();
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    [Test]
    public async Task Equality_DifferentValues_AreNotEqual()
    {
        var a = Map(KeyOffset.CreateState(1));
        var b = Map(KeyOffset.CreateState(2));

        await Assert.That(a == b).IsFalse();
        await Assert.That(a != b).IsTrue();
    }

    [Test]
    public async Task Equality_DifferentKinds_AreNotEqual()
    {
        var a = Map(KeyOffset.CreateState(1));
        var b = Map(OctaveOffset.CreateState(1));

        await Assert.That(a.Equals(b)).IsFalse();
    }

    [Test]
    public async Task Equality_Null_IsNotEqual()
    {
        var a = Map(KeyOffset.CreateState(1));

        await Assert.That(a == null).IsFalse();
        await Assert.That(a.Equals(null)).IsFalse();
    }

    [Test]
    public async Task ToStateTimelineMap_HasStateMapAtEveryPosition()
    {
        var input = Map(KeyOffset.CreateState(1), Tempo.CreateState(2));

        var result = input.ToStateTimelineMap(4);

        await Assert.That(result.Duration).IsEqualTo(4);
        await Assert.That(result.GetEffectiveStateMapAt(0)).IsEqualTo(input);
        await Assert.That(result.GetEffectiveStateMapAt(3.5)).IsEqualTo(input);
    }

    [Test]
    public async Task ToStateTimelineMap_Default_ResultsIn_Default()
    {
        await Assert.That(StateMap.Default.ToStateTimelineMap(4).IsDefault).IsTrue();
    }
}
