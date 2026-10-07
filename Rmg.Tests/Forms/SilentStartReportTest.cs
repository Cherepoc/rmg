using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Forms;

/// <summary>How long songs are silent before their first note, by genre.</summary>
public sealed class SilentStartReportTest
{
    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var name in GenrePresets.All.Select(x => x.Id).Prepend(null))
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: name is null ? null : GenrePresets.Genre(name))));
            var bars = songs.Select(x => x.Song.Notes!.Values.SelectMany(n => n).Select(n => n.Position).DefaultIfEmpty(0).Min() / x.Song.Meter.BarDuration).ToArray();
            if (bars.Any(x => x >= 1))
                Console.WriteLine($"{name ?? "the songs"}: silent a bar or more {bars.Count(x => x >= 1) / (double)bars.Length:P0}, two or more {bars.Count(x => x >= 2) / (double)bars.Length:P0}, at most {bars.Max():F1} bars");
        }

        await Task.CompletedTask;
    }
}
