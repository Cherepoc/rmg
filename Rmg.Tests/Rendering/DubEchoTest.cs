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
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("reggae")))))
        {
            var notes = song.Song.Notes!;
            int Echo(int track) => notes.GetValueOrDefault(track)?.Select(x => x.Value.State.GetStateValue(CompositionStateKinds.Echo)).FirstOrDefault(x => x > 0) ?? 0;
            songs++;
            var snares = DrumGroups.Snare.Drums.Select(DrumGroups.GetTrackNumber).Where(notes.ContainsKey).ToArray();
            var snareEcho = snares.Select(Echo).FirstOrDefault(x => x > 0);
            if (Echo(SongTracks.RhythmTrack) is not (> 0 and var delay))
            {
                await Assert.That(snareEcho).IsEqualTo(0);
                continue;
            }

            dubbed++;
            await Assert.That(notes[SongTracks.RhythmTrack].Any(x => x.Value.Pitches.Length > 1 && x.Value.State.GetStateValue(CompositionStateKinds.Echo) > 0)).IsTrue();
            // the snare on the beat at the part's delay, off it none, in the sections, which the effects play in
            foreach (var note in snares.SelectMany(x => notes[x]).Where(x => song.Map.Sections.Any(s => x.Position >= s.Start - 1e-9 && x.Position < s.End - 1e-9)))
                await Assert.That(note.Value.State.GetStateValue(CompositionStateKinds.Echo))
                    .IsEqualTo(Math.Abs(note.Position - Math.Round(note.Position)) < 1e-9 ? delay : 0);
        }

        await Assert.That(dubbed).IsGreaterThan(songs / 8);
        Console.WriteLine($"Reggae's rhythm part and snare echoing in {dubbed / (double)songs:P0} of its songs");
    }
}
