using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>A gliding bass: a genre's, sliding into its changes of pitch from the note before, as an 808 does.</summary>
public sealed class GlideTest
{
    /// <summary>The share of a bass's changes of pitch it slides into, over the songs given.</summary>
    private static double Slid(IEnumerable<CorpusSong> songs)
    {
        var changes = songs.SelectMany(song => song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Bass))?.ToArray() is { } bass
            ? bass.Skip(1).Where((x, i) => x.Value.Pitches[0] != bass[i].Value.Pitches[0])
            : []).ToArray();
        return changes.Count(x => x.Value.State.GetStateValue(CompositionStateKinds.Bend) == (int)BendKind.Slide) / (double)changes.Length;
    }

    /// <summary>
    ///     Hip-hop's songs slide into their bass's changes oftener with the genre's glide than the same songs with the songs'
    ///     own, a few of them gliding (measured over 128: 3.4% against 0.9% at offset 0, 2.2% against 1.5% at a million).
    /// </summary>
    [Test]
    public async Task AHipHopBass_SlidesIntoItsChanges_OftenerWithItsGlide()
    {
        var genre = GenrePresets.Genre("hipHop");
        var own = genre with { Choices = genre.Choices.Remove(GenreChoices.Glide.Name) };
        var hipHop = Slid(TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))));
        var songs = Slid(TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, new SongOverrides(Genre: own))));
        Console.WriteLine($"changes slid into, over 128 Hip-hop songs: with its glide {hipHop:P1}, with the songs' own {songs:P1}");

        await Assert.That(hipHop).IsGreaterThan(songs);
    }
}
