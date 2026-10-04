using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Arrangements;

/// <summary>How often songs solo, and how often the soloist does, from the plainest to the wildest base.</summary>
public sealed class SoloistReportTest
{
    private const int Count = 128;

    [Test]
    [Explicit]
    public async Task Solos_ByBase()
    {
        foreach (var @base in new[] { 0.0, 0.25, 0.5, 0.75, 1.0 })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, Count), seed => TestCorpus.Get(seed, new SongOverrides(Base: @base)));
            var soloing = songs.Count(x => x.Solos.Count > 0);
            var soloist = songs.Count(x => x.Solos.Values.Any(s => s.Soloists.Contains(TrackRole.Solo)));
            Console.WriteLine($"base {@base:0.00}: a solo {100.0 * soloing / Count:0}%, the soloist's {100.0 * soloist / Count:0}%, "
                              + $"solo sections {songs.Average(x => x.Solos.Count):0.00} a song, the soloist in the song {100.0 * songs.Count(x => x.Song.Draws!.Parts.Contains(TrackRole.Solo)) / Count:0}%");
        }

        await Task.CompletedTask;
    }
}
