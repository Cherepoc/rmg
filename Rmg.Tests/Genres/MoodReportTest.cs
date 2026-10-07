using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>A genre's mood as its harmony makes it: its sections' scales, its chords' degrees, and how long its chords sound major or minor.</summary>
public sealed class MoodReportTest
{
    /// <summary>A chord's quality by its pitch classes: major or minor where a triad of either is in it, the major first, else other.</summary>
    private static string Quality(IEnumerable<int> pitches)
    {
        var classes = pitches.Select(x => x % 12).ToHashSet();
        if (classes.Count < 3)
            return "two notes or fewer";
        bool Has(int root, int third) => classes.Contains(root) && classes.Contains((root + third) % 12) && classes.Contains((root + 7) % 12);
        if (Enumerable.Range(0, 12).Any(r => Has(r, 3)) && Enumerable.Range(0, 12).Any(r => Has(r, 4)))
            return "both";
        return Enumerable.Range(0, 12).Any(r => Has(r, 4)) ? "major" : Enumerable.Range(0, 12).Any(r => Has(r, 3)) ? "minor" : "other";
    }

    private static void Describe(string label, IReadOnlyList<CorpusSong> songs)
    {
        var scales = songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.SectionScale).Select(t => ((Scale)t.Value!).Name)).ToArray();
        var drawn = songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.Progression).Select(t => (ImmutableArray<int>)t.Value!)).SelectMany(x => x.Select(r => (r % 7 + 7) % 7)).ToArray();
        var chords = songs.SelectMany(x => x.Song.Notes!.Where(n => x.Song.TrackDefinitions[n.Key].Role is TrackRole.Chords or TrackRole.Pad).SelectMany(n => n.Value))
            .Where(x => x.Value.Pitches.Length >= 3).Select(x => (Quality: Quality(x.Value.Pitches), x.Value.Duration)).ToArray();
        var total = chords.Sum(x => x.Duration);
        Console.WriteLine($"{label}: scales {string.Join(", ", scales.GroupBy(x => x).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count() / (double)scales.Length:P0}"))}");
        Console.WriteLine($"{label}: degrees {string.Join(", ", drawn.GroupBy(x => x).OrderBy(x => x.Key).Select(x => $"{x.Key + 1}: {x.Count() / (double)drawn.Length:P0}"))}");
        var kinds = songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.ChordScales).SelectMany(t => (IEnumerable<ChordScale>)t.Value!)).Select(x => x.Kind).ToArray();
        Console.WriteLine($"{label}: chords' scales {string.Join(", ", kinds.GroupBy(x => x).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count() / (double)kinds.Length:P0}"))}");
        Console.WriteLine($"{label}: chords sounding {string.Join(", ", chords.GroupBy(x => x.Quality).OrderByDescending(x => x.Sum(c => c.Duration)).Select(x => $"{x.Key} {x.Sum(c => c.Duration) / total:P0}"))}");
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var name in new[] { "blackMetal", "idm", "metal", "djent", "nuMetal", "dungeon", "trap", "dnb", "techno", "medieval", "blues", "pop" })
        {
            Describe(name, TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(name)))));
        }

        await Task.CompletedTask;
    }
}
