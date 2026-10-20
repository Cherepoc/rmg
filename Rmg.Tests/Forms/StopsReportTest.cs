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
