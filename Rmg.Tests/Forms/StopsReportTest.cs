using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Forms;

/// <summary>How often a song's band stops: the whole band silent a beat or more before its end, and the devices that stop it.</summary>
public sealed class StopsReportTest
{
    /// <summary>The song's silences of a beat or more before its ending, every part's, the drums' too, their starts.</summary>
    internal static double[] Silences(CorpusSong song)
    {
        var end = song.Map.Plays.Where(x => x.IsEnding).Select(x => x.Span.Start).DefaultIfEmpty(song.Song.Duration).First();
        var sounding = song.Song.Notes!.Values.SelectMany(x => x).Where(x => x.Position < end).Select(x => (Start: x.Position, End: x.Position + Math.Max(x.Value.Duration, 0.25))).OrderBy(x => x.Start).ToArray();
        var gaps = new List<double>();
        var reach = sounding.Length > 0 ? sounding[0].End : 0;
        foreach (var (start, stop) in sounding.Skip(1))
        {
            if (start - reach >= 1 - 1e-9)
                gaps.Add(reach);
            reach = Math.Max(reach, stop);
        }
        return [..gaps];
    }

    private static string Line(string name, IReadOnlyList<CorpusSong> songs)
    {
        var silences = songs.Select(x => Silences(x).Length).ToArray();
        int Count(CorpusSong song, string point) => song.Trace.Where(x => x.Point == point).Sum(x => x.Value is ImmutableArray<double> a ? a.Length : x.Value is true or double ? 1 : 0);
        string Devices(string point) => $"{songs.Count(x => Count(x, point) > 0) / (double)songs.Count:P0} of songs, {songs.Average(x => Count(x, point)):F1} a song";
        return $"  {name}: silences of a beat or more {silences.Average():F1} a song, in {silences.Count(x => x > 0) / (double)silences.Length:P0} of songs; " +
               $"stop time {Devices(TracePoints.StopTime)}; ensemble hits {Devices(TracePoints.EnsembleHits)}; drops {Devices(TracePoints.Drop)}; breakdowns {Devices(TracePoints.Breakdown)}";
    }

    /// <summary>A song that does not stop plays no stop time and no ensemble hits.</summary>
    [Test]
    public async Task ASongThatDoesNotStop_PlaysNoStopTime_NorHits()
    {
        var kept = 0;
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("metal")))))
        {
            if ((bool)song.Trace.Single(x => x.Point == TracePoints.Stops).Value!)
                continue;
            kept++;
            foreach (var entry in song.Trace.Where(x => x.Point is TracePoints.StopTime or TracePoints.EnsembleHits))
                await Assert.That(((ImmutableArray<double>)entry.Value!).Length).IsEqualTo(0).Because($"seed {song.Seed}, section {entry.Section}");
        }

        await Assert.That(kept).IsGreaterThan(10);
    }

    /// <summary>
    ///     A genre's songs play stop time far less often than with every one of them stopping, the same songs (seen 25%
    ///     against 67% of 64 Metal songs).
    /// </summary>
    [Test]
    public async Task MostSongs_DoNotStop()
    {
        double Stopping(Genre genre) => TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre)))
            .Count(x => x.Trace.Any(t => t.Point == TracePoints.StopTime && ((ImmutableArray<double>)t.Value!).Length > 0)) / 64.0;
        var metal = GenrePresets.Genre("metal");
        var (own, all) = (Stopping(metal), Stopping(metal with { Choices = metal.Choices.SetItem(GenreChoices.Stops.Name, ImmutableDictionary<string, Rmg.Core.Probabilities.ByConvention>.Empty.Add("songs", new(1, 1, 1))) }));
        Console.WriteLine($"  Metal's songs in stop time: {own:P0}, {all:P0} with every song stopping");
        await Assert.That(all).IsGreaterThan(own + 0.15);
    }

    /// <summary>Metal with every song stopping and every section in stop time, its later appearances' chance the one given.</summary>
    private static Genre Stopping(double returns)
    {
        var metal = GenrePresets.Genre("metal");
        ImmutableDictionary<string, Rmg.Core.Probabilities.ByConvention> Chances(params (string Option, double Chance)[] options) =>
            options.ToImmutableDictionary(x => x.Option, x => new Rmg.Core.Probabilities.ByConvention(x.Chance, x.Chance, x.Chance));
        return metal with
        {
            Choices = metal.Choices.SetItem(GenreChoices.Stops.Name, Chances(("songs", 1))).SetItem(GenreChoices.StopTime.Name, Chances(("sections", 1), ("returns", returns)))
        };
    }

    /// <summary>A stop-time section whose later appearances never stop again stops in its first alone.</summary>
    [Test]
    public async Task AStopTimeSection_ThatNeverStopsAgain_StopsInItsFirstAppearanceAlone()
    {
        var stopped = 0;
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Stopping(0)))))
        foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.StopTime))
        {
            var starts = (ImmutableArray<double>)entry.Value!;
            stopped += starts.Length;
            var first = song.Map.Sections.First(x => x.SectionId == entry.Section).Start;
            await Assert.That(starts.All(x => Math.Abs(x - first) < 1e-6)).IsTrue().Because($"seed {song.Seed}, section {entry.Section}: {string.Join(", ", starts)}");
        }

        await Assert.That(stopped).IsGreaterThan(20);
    }

    /// <summary>
    ///     A stop-time section stops again in some of its later appearances, far fewer than with every one stopping, the same
    ///     songs, every section of every song in stop time (seen 5.3 stopped phrases a song of 32 Metal songs, against 9.9 with
    ///     every appearance stopping and 3.5 with the first alone).
    /// </summary>
    [Test]
    public async Task AStopTimeSection_StopsAgainNowAndThen()
    {
        double Phrases(Genre genre) => TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre)))
            .Average(x => x.Trace.Where(t => t.Point == TracePoints.StopTime).Sum(t => ((ImmutableArray<double>)t.Value!).Length));
        var (own, every, none) = (Phrases(Stopping(0.3)), Phrases(Stopping(1)), Phrases(Stopping(0)));
        Console.WriteLine($"  stopped phrases a song: {own:F1} at the songs' own chance, {every:F1} with every appearance stopping, {none:F1} with the first alone");
        await Assert.That(own).IsGreaterThan(none);
        await Assert.That(own).IsLessThan(every);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        Console.WriteLine(Line("the songs' own", TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed))));
        foreach (var genre in new[] { "metal", "thrash", "doom", "djent", "nuMetal", "blackMetal", "symphonic", "punk", "rock", "funk", "blues" })
            Console.WriteLine(Line(genre, TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre))))));
        await Task.CompletedTask;
    }
}
