using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>
///     A figure laid as a player varies it (<see cref="Figure.Loose" />) against the figure laid exactly, Latin's bass
///     tumbao, on the same songs at the feel facet's plain end, its middle and its wild end: how many of the bass's notes
///     fall on the figure's onsets, and how many of its bars play the figure exactly.
/// </summary>
public sealed class FigureChancesReportTest
{
    /// <summary>The bass's notes on the figure's onsets, and its bars playing the figure exactly, over the sections it plays the figure in.</summary>
    private static (int Notes, int On, int Bars, int Exact) Measure(CorpusSong song, string figure)
    {
        var onsets = Figures.Of(FigurePlayer.Bass, figure)!.Onsets.ToHashSet();
        var sections = song.Trace.Where(t => t.Point == TracePoints.Figures && ((ImmutableDictionary<FigurePlayer, string>)t.Value!).GetValueOrDefault(FigurePlayer.Bass) == figure)
            .Select(t => t.Section).ToHashSet();
        var bass = song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Bass))?.ToArray() ?? [];
        var (notes, on, bars, exact) = (0, 0, 0, 0);
        foreach (var span in song.Map.Sections.Where(x => sections.Contains(x.SectionId) && x.Meter.Sixteenths == 16))
        foreach (var bar in span.Bars.Where(x => x.End - x.Start >= span.Meter.BarDuration - 1e-9))
        {
            var at = bass.Where(x => x.Position >= bar.Start - 1e-9 && x.Position < bar.End - 1e-9).Select(x => (int)Math.Round((x.Position - bar.Start) * 4)).ToHashSet();
            if (at.Count == 0)
                continue;
            (notes, on, bars, exact) = (notes + at.Count, on + at.Count(onsets.Contains), bars + 1, exact + (at.SetEquals(onsets) ? 1 : 0));
        }

        return (notes, on, bars, exact);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var figure in new[] { "tumbao", "tumbaoChances" })
        foreach (var feel in new[] { 0.0, 0.5, 1 })
        {
            var genre = GenrePresets.With("latin", GenreChoices.Figures[FigurePlayer.Bass], (figure, 1));
            var measured = TestCorpus.InParallel(Enumerable.Range(0, 32), seed => Measure(TestCorpus.Get(seed, new SongOverrides(Genre: genre, Facets: TestCorpus.FacetsOf((Facet.Feel, feel)))), figure)).ToArray();
            var (notes, on, bars, exact) = (measured.Sum(x => x.Notes), measured.Sum(x => x.On), measured.Sum(x => x.Bars), measured.Sum(x => x.Exact));
            Console.WriteLine($"{figure,-14} feel {feel:0.0}: {bars} bars, the bass's notes on the figure {on / (double)Math.Max(1, notes):P0}, its bars the figure exactly {exact / (double)Math.Max(1, bars):P0}");
        }

        await Task.CompletedTask;
    }
}
