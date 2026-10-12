using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Bass;

/// <summary>The bass on the kick (<see cref="Figures" />): its notes struck where the kick strikes, by genre.</summary>
public sealed class BassOnKickTest
{
    private static readonly string[] Genres = ["trap", "hipHop", "rock", "punk", "country", "funk", "dnb", "metal"];

    /// <summary>The bass's onsets and the kick's in each bar of the song's sections, with the bass's figure there.</summary>
    private static IEnumerable<(string? Figure, double[] Bass, HashSet<double> Kick)> Bars(CorpusSong song)
    {
        if (song.Absent.Contains(TrackRole.Bass) || !song.PlaysDrums)
            yield break;
        var kicks = DrumGroups.Kick.Drums.Select(DrumGroups.GetTrackNumber).ToHashSet();
        var bass = song.Song.Notes!.GetValueOrDefault(song.Track(TrackRole.Bass))?.Select(x => x.Position).Distinct().ToArray() ?? [];
        var kick = song.Song.Notes!.Where(x => kicks.Contains(x.Key)).SelectMany(x => x.Value).Select(x => Math.Round(x.Position, 4)).ToHashSet();
        var figures = song.Trace.Where(t => t.Point == TracePoints.Figures).ToDictionary(t => t.Section, t => (ImmutableDictionary<FigurePlayer, string>)t.Value!);
        foreach (var span in song.Map.Sections)
        foreach (var (start, end) in Enumerable.Range(0, span.BarCount).Select(span.Bar))
        {
            var inBar = bass.Where(x => x >= start - 1e-9 && x < end - 1e-9).ToArray();
            if (inBar.Length > 0 && kick.Any(x => x >= start - 1e-9 && x < end - 1e-9))
                yield return (figures.GetValueOrDefault(span.SectionId)?.GetValueOrDefault(FigurePlayer.Bass), inBar, kick);
        }
    }

    /// <summary>A bass on the kick strikes with it, but where a fill or a walk into the next chord moves it.</summary>
    [Test]
    public async Task ABassOnTheKick_StrikesWithIt()
    {
        var bars = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("trap"))))
            .SelectMany(x => Bars(x).Where(b => b.Figure == "withKick").ToArray()).ToArray();
        var share = bars.Sum(x => x.Bass.Count(b => x.Kick.Contains(Math.Round(b, 4)))) / (double)Math.Max(1, bars.Sum(x => x.Bass.Length));
        Console.WriteLine($"{bars.Length} bars on the kick, {share:P0} of the bass's notes with it");
        // measured 87% at offset 0 and 86% at a million
        await Assert.That(bars.Length).IsGreaterThan(500);
        await Assert.That(share).IsGreaterThan(0.75);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var genre in Genres)
        {
            var bars = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))).SelectMany(x => Bars(x).ToArray()).ToArray();
            var notes = bars.Sum(x => x.Bass.Length);
            var onKick = bars.Sum(x => x.Bass.Count(b => x.Kick.Contains(Math.Round(b, 4))));
            Console.WriteLine($"{genre}: {bars.Length} bars, the bass's notes on the kick's {onKick / (double)Math.Max(1, notes):P0}, " +
                              $"its bars on the kick's figure {bars.Count(x => x.Figure == "withKick") / (double)Math.Max(1, bars.Length):P0}, its notes on the kick there " +
                              $"{bars.Where(x => x.Figure == "withKick").Sum(x => x.Bass.Count(b => x.Kick.Contains(Math.Round(b, 4)))) / (double)Math.Max(1, bars.Where(x => x.Figure == "withKick").Sum(x => x.Bass.Length)):P0}");
        }

        // the bass's notes sounding past the next's start, as written, in the songs whose bass is on the kick somewhere and not
        foreach (var genre in new[] { "trap", "hipHop" })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))).Where(x => !x.Absent.Contains(TrackRole.Bass)).ToArray();
            foreach (var group in songs.GroupBy(x => x.Trace.Any(t => t.Point == TracePoints.Figures && ((ImmutableDictionary<FigurePlayer, string>)t.Value!).GetValueOrDefault(FigurePlayer.Bass) == "withKick")))
            {
                var notes = group.SelectMany(x => x.Notes(x.Track(TrackRole.Bass)).Zip(x.Notes(x.Track(TrackRole.Bass)).Skip(1))).ToArray();
                Console.WriteLine($"{genre}, {(group.Key ? "on the kick" : "its own")}: {notes.Count(x => x.First.Position + x.First.Value.Duration > x.Second.Position + 1e-6 && x.Second.Position > x.First.Position + 1e-6) / (double)Math.Max(1, notes.Length):P1} of its notes rendered overlapping the next");
            }
        }

        await Task.CompletedTask;
    }
}
