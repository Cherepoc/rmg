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

    /// <summary>Songs given a length: how far from it they come, how many within 15% of it, and how they get there.</summary>
    [Test]
    [Explicit]
    public async Task Lengths_ByTarget()
    {
        foreach (var target in new[] { 60.0, 120, 180, 300, 600 })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, Count), seed => TestCorpus.Get(seed, new SongOverrides(Length: target)));
            var errors = songs.Select(x => x.Rendered.Seconds / target - 1).ToArray();
            var sorted = errors.Select(Math.Abs).Order().ToArray();
            // the most times a section plays in a row
            int Run(CorpusSong song) => song.Map.Sections.Select(x => x.SectionId).Aggregate((Best: 0, Run: 0, Last: -1), (a, id) => id == a.Last ? (Math.Max(a.Best, a.Run + 1), a.Run + 1, id) : (Math.Max(a.Best, 1), 1, id)).Best;
            Console.WriteLine($"{Minutes(target)}: {Spread([..songs.Select(x => x.Rendered.Seconds)])}; error middle {100 * sorted[sorted.Length / 2]:0}%, within 15% {100.0 * sorted.Count(x => x <= 0.15) / Count:0}%, "
                              + $"short {100.0 * errors.Count(x => x < -0.15) / Count:0}%, long {100.0 * errors.Count(x => x > 0.15) / Count:0}%; sections {songs.Average(x => x.Map.Sections.Length):0.0}, "
                              + $"longest run {songs.Max(Run)}, of a form {100.0 * songs.Count(x => x.Trace.Any(t => t.Point == Rmg.Core.Composition.TracePoints.SongForm && ((SongStructure)t.Value!).Roles.Values.Any(r => r != SectionRole.Free))) / Count:0}%");
        }

        await Task.CompletedTask;
    }
}
