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

    [Test]
    public async Task AHipHopBass_SlidesIntoItsChanges_FarOftenerThanTheSongs()
    {
        var hipHop = Slid(TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("hipHop")))));
        var songs = Slid(TestCorpus.Range(16));
        Console.WriteLine($"changes slid into: Hip-hop {hipHop:P1}, the songs {songs:P1}");

        await Assert.That(hipHop).IsGreaterThan(songs * 3 + 0.03);
    }
}
