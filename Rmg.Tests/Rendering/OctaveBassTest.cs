using Rmg.Core.Events;
using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>An octave bass: a section's bass off the beat the note before an octave away, a genre's the more.</summary>
public sealed class OctaveBassTest
{
    /// <summary>The share of a bass's notes an octave from the note before, in the songs given that have a bass.</summary>
    private static double Octaves(IEnumerable<CorpusSong> songs)
    {
        var notes = songs.Select(x => x.Song.Notes!.GetValueOrDefault(x.Song.Track(TrackRole.Bass))?.ToArray()).OfType<TimelineItem<RealizedNote>[]>().ToArray();
        return notes.Sum(bass => bass.Skip(1).Where((x, i) => Math.Abs(x.Value.Pitches[0] - bass[i].Value.Pitches[0]) == 12).Count()) / (double)notes.Sum(x => x.Length);
    }

    [Test]
    public async Task AFunkBass_JumpsAnOctave_OftenerThanWithTheSongsChance()
    {
        // the same songs, Funk's octave bass against the songs' own chance of it
        var funk = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("funk")))).ToArray();
        var without = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Without("funk", GenreChoices.OctaveBass.Name)))).ToArray();
        Console.WriteLine($"the bass an octave from the note before: funk's {Octaves(funk):P1}, with the songs' chance {Octaves(without):P1}");

        // measured: 27.5% to 29.1% against 2.8% to 5.3%, at offsets 0 and a million
        await Assert.That(Octaves(funk)).IsGreaterThan(Octaves(without) * 1.5);
    }
}
