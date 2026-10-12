using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.SongGenerators;

public sealed class SongGeneratorChordTrackTest
{

    [Test]
    public async Task ChordTrack_PlaysEveryChordWithAtLeastTwoNotes()
    {
        // every shape has two notes or more, and snapping and fitting into the range never merges them
        var chords = 0;
        for (var seed = 0; seed < 64; seed++)
        {
            // as the song realizes them, but a solo's line, which the chords may play, and broken chords, one note at a time
            var sizes = TestCorpus.Get(seed).Song.Notes![TestCorpus.Get(seed).Track(TrackRole.Chords)]
                .Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Arpeggio) == 0 && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0)
                .Select(x => x.Value.Pitches.Distinct().Count())
                .ToArray();
            chords += sizes.Length;
            await Assert.That(sizes.All(x => x >= 2)).IsTrue().Because($"seed {seed}");
        }

        await Assert.That(chords).IsGreaterThan(1000);
    }
}
