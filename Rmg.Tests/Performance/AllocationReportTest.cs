using System.Collections.Immutable;
using System.Diagnostics;
using Rmg.Core.Composition;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Performance;

/// <summary>What generating a song allocates and how long it takes, in all and stage by stage, and each preset's slowest stages.</summary>
public sealed class AllocationReportTest
{
    [Test]
    [Explicit]
    public async Task Report()
    {
        const int count = 16;
        // one song first, so that the runtime's own first allocations are not counted
        SongGenerator.GenerateSong(1000, SongOverrides.None);
        var decided = 0L;
        var clock = Stopwatch.StartNew();
        for (var seed = 0UL; seed < count; seed++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            SongDecisions.Draw(seed, SongOverrides.None, CancellationToken.None);
            decided += GC.GetAllocatedBytesForCurrentThread() - before;
        }

        var drawing = clock.Elapsed;
        clock.Restart();
        var (stages, times, allocated) = Stages(Enumerable.Range(0, count).Select(x => (ulong)x), SongOverrides.None);
        var generating = clock.Elapsed;
        // the first stage's count holds the song's decisions, its sections and its assembly, which come before it
        var first = stages.Keys.First();
        stages[first] -= decided;
        Console.WriteLine($"a song: {allocated / count / 1e6:F1} MB allocated in {generating.TotalMilliseconds / count:F0} ms, its decisions, sections and assembly "
                          + $"{decided / count / 1e6:F1} MB in {drawing.TotalMilliseconds / count:F0} ms");
        foreach (var (stage, bytes) in stages.OrderByDescending(x => x.Value).Where(x => x.Value / count > 100_000))
            Console.WriteLine($"  {stage}: {bytes / count / 1e6:F1} MB, {times[stage] / count:F1} ms");

        // a genre plays some stages far more than the songs do, mutations, polymeters, solos and figures: each preset's
        // slowest stages over a few of its songs
        const int presetSongs = 4;
        foreach (var preset in GenrePresets.All)
        {
            var (_, presetTimes, _) = Stages(Enumerable.Range(0, presetSongs).Select(x => (ulong)x), new SongOverrides(Genre: GenrePresets.Genre(preset)));
            Console.WriteLine($"{preset.Id}: " + string.Join(", ", presetTimes.OrderByDescending(x => x.Value).Take(3).Select(x => $"{x.Key} {x.Value / presetSongs:F1} ms")));
        }

        await Task.CompletedTask;
    }

    /// <summary>What each stage allocates and how long it takes, summed over the songs, and the songs' allocation in all.</summary>
    private static (Dictionary<string, long> Bytes, Dictionary<string, double> Milliseconds, long Allocated) Stages(IEnumerable<ulong> seeds, SongOverrides overrides)
    {
        var (bytes, times, allocated) = (new Dictionary<string, long>(), new Dictionary<string, double>(), 0L);
        foreach (var seed in seeds)
        {
            var last = GC.GetAllocatedBytesForCurrentThread();
            var start = last;
            var clock = Stopwatch.StartNew();
            SongGenerator.GenerateSong(seed, overrides, ImmutableHashSet<string>.Empty, (stage, _) =>
            {
                var now = GC.GetAllocatedBytesForCurrentThread();
                bytes[stage] = bytes.GetValueOrDefault(stage) + now - last;
                times[stage] = times.GetValueOrDefault(stage) + clock.Elapsed.TotalMilliseconds;
                last = now;
                clock.Restart();
            }, CancellationToken.None);
            allocated += GC.GetAllocatedBytesForCurrentThread() - start;
        }

        return (bytes, times, allocated);
    }
}
