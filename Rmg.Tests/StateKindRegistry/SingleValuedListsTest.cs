using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.StateKindRegistry;

/// <summary>
///     The scale and a chord's shape are single values that are lists, whose kinds join the lists of every layer that
///     sets them: set by one layer each, a note's scale has as many notes as a scale has, and its chord no more notes
///     than the largest shape and none of them twice, where two layers setting one would join them into one twice as
///     long.
/// </summary>
public sealed class SingleValuedListsTest
{
    [Test]
    public async Task EveryNote_HasOneScale_AndOneChordShape()
    {
        var scaleSizes = Core.Composition.Scales.All.Select(x => x.Offsets.Length).ToHashSet();
        var maxChordSize = ChordShapes.All.Max(x => x.Targets.Length);
        var checkedNotes = 0;
        foreach (var song in TestCorpus.Range(20))
        foreach (var note in song.Song.Notes!.Values.SelectMany(x => x))
        {
            var scale = note.Value.State.GetStateValue(StateKinds.ScaleOffsets);
            var chord = note.Value.State.GetStateValue(StateKinds.ChordNotePitchOffsets);
            if (scale.IsEmpty && chord.IsEmpty)
                continue;

            checkedNotes++;
            if (!scale.IsEmpty)
                await Assert.That(scaleSizes).Contains(scale.Length).Because($"seed {song.Seed} at {note.Position}");
            await Assert.That(chord.Length).IsLessThanOrEqualTo(maxChordSize).Because($"seed {song.Seed} at {note.Position}");
            // a voicing moves a shape's notes by octaves, and keeps them apart, where two shapes joined share their root
            await Assert.That(chord.Distinct().Count()).IsEqualTo(chord.Length).Because($"seed {song.Seed} at {note.Position}");
        }

        await Assert.That(checkedNotes).IsGreaterThan(1000);
    }
}
