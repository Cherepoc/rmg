using Rmg.Core.Events;

namespace Rmg.Tests.LowestLayerWins;

public sealed class LowestLayerWinsTest
{
    private static readonly StateKind<LayerValue<int>> Kind = StateKinds.CreateLowestLayerWins<int>("TestLowestLayerWins");

    private static StateMap At(int depth, int value) => StateMap.FromStates([Kind.CreateState(new LayerValue<int>(depth, value))]);

    [Test]
    public async Task TheDeepestLayer_Wins_WhateverOrderTheyMergeIn()
    {
        var song = At(StateDepths.Song, 1);
        var section = At(StateDepths.Section, 2);
        var note = At(StateDepths.Note, 3);

        await Assert.That(song.MergeWith(section).MergeWith(note).GetStateValue(Kind).Value).IsEqualTo(3);
        await Assert.That(note.MergeWith(song).MergeWith(section).GetStateValue(Kind).Value).IsEqualTo(3);
        await Assert.That(section.MergeWith(song).GetStateValue(Kind).Value).IsEqualTo(2);
    }

    [Test]
    public async Task AnUnsetKind_IsTheDefault_AndTheSameValueTwice_IsOne()
    {
        await Assert.That(StateMap.Default.GetStateValue(Kind).IsSet).IsFalse();
        await Assert.That(At(StateDepths.Section, 2).MergeWith(At(StateDepths.Section, 2)).GetStateValue(Kind).Value).IsEqualTo(2);
    }

    [Test]
    public async Task TwoValues_AtTheSameDepth_AreAnError()
    {
        await Assert.That(() => At(StateDepths.Section, 1).MergeWith(At(StateDepths.Section, 2))).Throws<InvalidOperationException>();
    }
}
