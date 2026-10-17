using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>Trance as it is produced: its rolling bass, its builds, its effects stacked, its degrees.</summary>
public sealed class TranceReportTest
{
    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("trance"))));
        var figures = songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.Figures).Select(t => (ImmutableDictionary<FigurePlayer, string>)t.Value!)).ToArray();
        string Figure(FigurePlayer player, string figure) => $"{figure} {figures.Count(x => x.GetValueOrDefault(player) == figure) / (double)figures.Length:P0}";
        var effects = songs.Select(x => (SongEffects)x.Trace.Single(t => t.Point == TracePoints.SongEffects).Value!).ToArray();
        var mix = songs.Select(x => (MixFilterPlan)x.Trace.Single(t => t.Point == TracePoints.MixFilter).Value!).ToArray();
        var drops = songs.Sum(x => x.Trace.Count(t => t.Point == TracePoints.Drop));
        var drawn = songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.Progression).Select(t => (ImmutableArray<int>)t.Value!)).SelectMany(x => x.Skip(1).Select(r => (r % 7 + 7) % 7)).ToArray();
        string Degree(string label, int step) => $"{label} {drawn.Count(x => x == step) / (double)drawn.Length:P0}";
        Console.WriteLine($"trance: sections' bass {Figure(FigurePlayer.Bass, "rolling")}, {Figure(FigurePlayer.Bass, "offbeat")}; chords {Figure(FigurePlayer.Chords, "arpeggio16")}; " +
                          $"{drops / (double)songs.Length:F2} drops a song, songs with one {songs.Count(x => x.Trace.Any(t => t.Point == TracePoints.Drop)) / (double)songs.Length:P0}");
        Console.WriteLine($"trance: strong effects a song {effects.Average(x => (x.Strong == StrongEffect.None ? 0 : 1) + x.Others.Length):F2}, two or more {effects.Count(x => x.Others.Length > 0) / (double)effects.Length:P0}; " +
                          $"{string.Join(", ", effects.SelectMany(x => x.Others.Prepend(x.Strong)).Where(x => x != StrongEffect.None).GroupBy(x => x).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count()}"))}; " +
                          $"mix opening {mix.Count(x => x.Opening) / (double)mix.Length:P0}, dipping {mix.Count(x => !x.Dips.IsEmpty) / (double)mix.Length:P0}");
        Console.WriteLine($"trance: the chords drawn on {Degree("♭III", 2)}, {Degree("iv", 3)}, {Degree("v", 4)}, {Degree("♭VI", 5)}, {Degree("♭VII", 6)}");
        await Task.CompletedTask;
    }
}
