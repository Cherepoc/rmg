using Rmg.Core.Composition;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Grooves;

/// <summary>How often a song's sections leave its time: half or double time, a tempo or a meter of their own.</summary>
public sealed class FeelChangesReportTest
{
    /// <summary>A song's sections as played, but the ending: each one's time feel, tempo and meter, as its trace and map have them.</summary>
    internal static (int Feel, double Tempo, string Meter)[] Sections(CorpusSong song)
    {
        var feels = song.Trace.Where(x => x.Point == TracePoints.TimeFeel).GroupBy(x => x.Section).ToDictionary(x => x.Key, x => (int)x.Last().Value!);
        var tempos = (System.Collections.Immutable.ImmutableArray<double>)song.Trace.Single(x => x.Point == TracePoints.SectionTempo).Value!;
        return [..song.PlayedSpans.Select((span, i) => (feels[span.SectionId], i < tempos.Length ? Math.Round(tempos[i], 3) : 1, span.Meter.ToString()))];
    }

    private static string Line(string name, IReadOnlyList<CorpusSong> songs)
    {
        var all = songs.Select(Sections).ToArray();
        int Changes((int Feel, double Tempo, string Meter)[] s, Func<(int Feel, double Tempo, string Meter), object> by) => s.Zip(s.Skip(1)).Count(x => !Equals(by(x.First), by(x.Second)));
        var feelChanges = all.Select(s => Changes(s, x => x.Feel)).ToArray();
        var anyChanges = all.Select(s => Changes(s, x => (x.Feel, x.Tempo, x.Meter))).ToArray();
        return $"  {name}: sections off the song's time {all.SelectMany(x => x).Count(x => x.Feel != 0) / (double)all.Sum(x => x.Length):P0} (half {all.SelectMany(x => x).Count(x => x.Feel == 1) / (double)all.Sum(x => x.Length):P0}), " +
               $"songs whose feel never changes {feelChanges.Count(x => x == 0) / (double)all.Length:P0}, whose feel, tempo and meter never change {anyChanges.Count(x => x == 0) / (double)all.Length:P0}, " +
               $"feel changes a song {feelChanges.Average():F1}, any {anyChanges.Average():F1}";
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        Console.WriteLine(Line("the songs' own", TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed))));
        foreach (var genre in new[] { "metal", "thrash", "doom", "djent", "nuMetal", "blackMetal", "symphonic", "punk", "rock" })
            Console.WriteLine(Line(genre, TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre))))));
        await Task.CompletedTask;
    }
}
