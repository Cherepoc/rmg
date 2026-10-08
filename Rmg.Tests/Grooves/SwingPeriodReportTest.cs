using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Grooves;

/// <summary>Which notes a swinging song swings, its 8ths or its 16ths, by preset: a shuffle's 8ths whatever its tempo.</summary>
public sealed class SwingPeriodReportTest
{
    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var name in new[] { "blues", "jazz", "country", "funk", "hipHop" })
        {
            var swung = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(name))))
                .Select(x => x.Swing).Where(x => x.Delay > 0).ToArray();
            Console.WriteLine($"{name}: songs swinging {swung.Length} of 64, their 8ths {swung.Count(x => x.Period >= 1):0} and their 16ths {swung.Count(x => x.Period < 1)}");
        }

        await Task.CompletedTask;
    }
}
