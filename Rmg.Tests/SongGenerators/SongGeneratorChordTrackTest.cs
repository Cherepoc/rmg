using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.SongGenerators;

public sealed class SongGeneratorChordTrackTest
{
    private const int ChordTrackNumber = 4;

    [Test]
    public async Task ChordTrack_PlaysEveryChordWithAtLeastTwoNotes()
    {
        // every shape has two notes or more, and snapping and fitting into the range never merges them
        for (var seed = 0; seed < 64; seed++)
        {
            // as the song realizes them, but a solo's line, which the chords may play, and broken chords, one note at a time
            var smallestChord = TestCorpus.Get(seed).Song.Notes![ChordTrackNumber]
                .Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Arpeggio) == 0 && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0)
                .Select(x => x.Value.Pitches.Distinct().Count())
                .DefaultIfEmpty(2)
                .Min();

            await Assert.That(smallestChord)
                .IsGreaterThanOrEqualTo(2)
                .Because($"seed {seed}");
        }
    }
}
