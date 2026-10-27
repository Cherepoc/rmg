using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>A dub's echo: the rhythm part's stabs echoing, and the snare's backbeat with them, at the same delay.</summary>
public sealed class DubEchoTest
{
    [Test]
    public async Task AnEchoingRhythmPart_EchoesItsChords_AndTheSnaresBackbeat_AtItsDelay()
    {
        var (songs, dubbed) = (0, 0);
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("reggae")))))
        {
            var notes = song.Song.Notes!;
            int Echo(int track) => notes.GetValueOrDefault(track)?.Select(x => x.Value.State.GetStateValue(CompositionStateKinds.Echo)).FirstOrDefault(x => x > 0) ?? 0;
            songs++;
            var snares = DrumGroups.Snare.Drums.Select(DrumGroups.GetTrackNumber).Where(notes.ContainsKey).ToArray();
            var snareEcho = snares.Select(Echo).FirstOrDefault(x => x > 0);
            if (Echo(song.Track(TrackRole.Rhythm)) is not (> 0 and var delay))
            {
                await Assert.That(snareEcho).IsEqualTo(0);
                continue;
            }

            dubbed++;
            await Assert.That(notes[song.Track(TrackRole.Rhythm)].Any(x => x.Value.Pitches.Length > 1 && x.Value.State.GetStateValue(CompositionStateKinds.Echo) > 0)).IsTrue();
            // every stroke of the snare at the part's delay, as its send to the delay is its note's
            foreach (var note in snares.SelectMany(x => notes[x]))
                await Assert.That(note.Value.State.GetStateValue(CompositionStateKinds.Echo)).IsEqualTo(delay);
        }

        Console.WriteLine($"Reggae's rhythm part and snare echoing in {dubbed / (double)songs:P0} of its songs");
        // a few at least, against the mechanism breaking (measured over 128: 16% at offset 0, 11% at a million)
        await Assert.That(dubbed).IsGreaterThan(songs / 16);
    }
}
