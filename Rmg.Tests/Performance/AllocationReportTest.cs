using System.Collections.Immutable;
using System.Diagnostics;
using Rmg.Core.Composition;

namespace Rmg.Tests.Performance;

/// <summary>What generating a song allocates and how long it takes, in all and stage by stage, the stages' sums before them in their own line.</summary>
public sealed class AllocationReportTest
{
    [Test]
    [Explicit]
    public async Task Report()
    {
        const int count = 16;
        // one song first, so that the runtime's own first allocations are not counted
        SongGenerator.GenerateSong(1000, SongOverrides.None);
        var stages = new Dictionary<string, long>();
        var (allocated, decided) = (0L, 0L);
        var clock = Stopwatch.StartNew();
        for (var seed = 0UL; seed < count; seed++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            SongDecisions.Draw(seed, SongOverrides.None, CancellationToken.None);
            decided += GC.GetAllocatedBytesForCurrentThread() - before;
        }

        var drawing = clock.Elapsed;
        clock.Restart();
        for (var seed = 0UL; seed < count; seed++)
        {
            var last = GC.GetAllocatedBytesForCurrentThread();
            var start = last;
            SongGenerator.GenerateSong(seed, SongOverrides.None, ImmutableHashSet<string>.Empty, (stage, _) =>
            {
                var now = GC.GetAllocatedBytesForCurrentThread();
                stages[stage] = stages.GetValueOrDefault(stage) + now - last;
                last = now;
            }, CancellationToken.None);
            allocated += GC.GetAllocatedBytesForCurrentThread() - start;
        }

        var generating = clock.Elapsed;
        // the first stage's count holds the song's decisions, its sections and its assembly, which come before it
        var first = stages.Keys.First();
        stages[first] -= decided;
        Console.WriteLine($"a song: {allocated / count / 1e6:F1} MB allocated in {generating.TotalMilliseconds / count:F0} ms, its decisions, sections and assembly "
                          + $"{decided / count / 1e6:F1} MB in {drawing.TotalMilliseconds / count:F0} ms");
        foreach (var (stage, bytes) in stages.OrderByDescending(x => x.Value).Where(x => x.Value / count > 100_000))
            Console.WriteLine($"  {stage}: {bytes / count / 1e6:F1} MB");
        await Task.CompletedTask;
    }
}
