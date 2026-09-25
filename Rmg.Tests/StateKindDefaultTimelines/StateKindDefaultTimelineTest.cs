using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Tests.StateKindDefaultTimelines;

/// <summary>
/// A missing timeline must behave like a timeline of the kind's default value.
/// </summary>
public sealed class StateKindDefaultTimelineTest
{
    [Test]
    public async Task MultiplicativeKind_DefaultTimeline_ResultsIn_KindDefault()
    {
        var result = StateKinds.Tempo.CreateDefaultTimeline(0).GetEffectiveValueAt(0);

        await Assert.That(result).IsEqualTo(1);
    }

    [Test]
    public async Task GetStateTimeline_MissingMultiplicativeKind_ResultsIn_KindDefault()
    {
        var input = StateTimelineMap.Create(0);

        var result = input.GetStateTimeline(StateKinds.Tempo);

        await Assert.That(result.GetEffectiveValueAt(0)).IsEqualTo(1);
    }

    [Test]
    public async Task GetStateTimeline_MissingKindInMapWithState_ResultsIn_KindDefault()
    {
        var input = StateTimelineMap.Create(
            2,
            [StateTimeline.Create(2, StateKinds.KeyOffset, [new TimelineItem<int>(0, 1)])]
        );

        var result = input.GetStateTimeline(StateKinds.Tempo);

        await Assert.That(result.GetEffectiveValueAt(1)).IsEqualTo(1);
        await Assert.That(result.GetEffectiveStateAt(1).Value).IsEqualTo(1);
    }

    [Test]
    public async Task CollectionKind_DefaultTimeline_ResultsIn_InitializedEmptyCollection()
    {
        var result = StateKinds.ScaleOffsets.CreateDefaultTimeline(0).GetEffectiveValueAt(0);

        await Assert.That(result.IsDefault).IsFalse();
        await Assert.That(result.Length).IsEqualTo(0);
    }

    [Test]
    public async Task GetStateTimeline_MissingCollectionKind_ResultsIn_InitializedEmptyCollection()
    {
        var result = StateTimelineMap.Create(0).GetStateTimeline(StateKinds.ScaleOffsets).GetEffectiveValueAt(0);

        await Assert.That(result.IsDefault).IsFalse();
    }

    [Test]
    public async Task NoneKind_OfCollectionType_HasInitializedDefaultValue()
    {
        var result = StateKind.None<ImmutableArray<int>>().DefaultValue;

        await Assert.That(result.IsDefault).IsFalse();
    }
}
