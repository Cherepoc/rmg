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
            var song = TestCorpus.Get(seed).Song;
            var chordTrackSong = new Song(
                song.Duration,
                song.Meter,
                song.TrackDefinitions.Where(x => x.Key == ChordTrackNumber).ToImmutableSortedDictionary(),
                song.TrackEventStateTimelineMap
            );

            // but a solo's line, which the chords may play
            var solos = TestCorpus.Get(seed).Map.Sections.Where(TestCorpus.Get(seed).IsSolo).ToArray();
            var notes = Render.RenderSong(chordTrackSong).Tracks.Single().NoteTimeline;
            var smallestChord = notes
                .Where(x => !solos.Any(span => x.Position >= span.Start - 1 && x.Position < span.End + 1))
                .GroupBy(x => x.Position)
                .Min(x => x.Select(note => note.Value.Offset).Distinct().Count());

            await Assert.That(smallestChord)
                .IsGreaterThanOrEqualTo(2)
                .Because($"seed {seed}");
        }
    }
}
