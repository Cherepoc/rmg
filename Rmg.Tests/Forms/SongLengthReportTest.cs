using Rmg.Core.Composition;

namespace Rmg.Tests.Forms;

/// <summary>How long songs play, from the plainest to the wildest base, and as their bases are drawn.</summary>
public sealed class SongLengthReportTest
{
    private const int Count = 128;

    internal static string Spread(IReadOnlyList<double> seconds)
    {
        var sorted = seconds.Order().ToArray();
        string At(double share) => Minutes(sorted[(int)(share * (sorted.Length - 1))]);
        return $"10% {At(0.1)}, 50% {At(0.5)}, 90% {At(0.9)}, from {Minutes(sorted[0])} to {Minutes(sorted[^1])}";
    }

    internal static string Minutes(double seconds) => $"{(int)seconds / 60}:{(int)seconds % 60:00}";

    [Test]
    [Explicit]
    public async Task Lengths_ByBase()
    {
        foreach (var @base in new double?[] { null, 0, 0.5, 1 })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, Count), seed => TestCorpus.Get(seed, new SongOverrides(Base: @base)));
            Console.WriteLine($"base {@base?.ToString("0.0") ?? "drawn"}: {Spread([..songs.Select(x => x.Rendered.Seconds)])}, sections {songs.Average(x => x.Map.Sections.Length):0.0} a song");
        }

        await Task.CompletedTask;
    }
}
