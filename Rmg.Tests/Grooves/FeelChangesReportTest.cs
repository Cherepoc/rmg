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
        // a section's tempo by its place among the map's sections, which an intro played as one shares
        return [..song.Map.Plays.Where(x => !x.IsEnding).Select(x => (feels[x.Span.SectionId], x.Index < tempos.Length ? Math.Round(tempos[x.Index], 3) : 1, x.Span.Meter.ToString()))];
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

    /// <summary>A song that keeps its time leaves it in no section, a calm one's and a breakdown's among them.</summary>
    [Test]
    public async Task ASongKeepingItsTime_LeavesItNowhere()
    {
        var kept = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("metal")))))
        {
            if ((bool)song.Trace.Single(x => x.Point == TracePoints.TimeChanges).Value!)
                continue;
            kept++;
            foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.TimeFeel))
                await Assert.That((int)entry.Value!).IsEqualTo(0).Because($"seed {song.Seed}, section {entry.Section}");
        }

        await Assert.That(kept).IsGreaterThan(10);
    }

    /// <summary>
    ///     A genre's songs hold one feel throughout far oftener than with every one of them leaving its time, the same songs
    ///     (seen 56% against 9% of 64 Metal songs).
    /// </summary>
    [Test]
    public async Task MostSongs_KeepTheirTime()
    {
        double Steady(Genre genre) => TestCorpus.InParallel(Enumerable.Range(0, 64), seed => Sections(TestCorpus.Get(seed, new SongOverrides(Genre: genre))))
            .Count(x => x.Zip(x.Skip(1)).All(p => p.First.Feel == p.Second.Feel)) / 64.0;
        var metal = GenrePresets.Genre("metal");
        var (own, departing) = (Steady(metal), Steady(metal with { Choices = metal.Choices.SetItem(GenreChoices.TimeChanges.Name, System.Collections.Immutable.ImmutableDictionary<string, Rmg.Core.Probabilities.ByConvention>.Empty.Add("songs", new(1, 1, 1))) }));
        Console.WriteLine($"  Metal's songs holding one feel: {own:P0}, {departing:P0} with every song leaving its time");
        await Assert.That(own).IsGreaterThan(departing + 0.2);
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
