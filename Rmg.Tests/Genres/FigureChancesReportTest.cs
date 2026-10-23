using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>
///     The loose figures (<see cref="Figure.Loose" />), each in a preset that names it and given every section, on the same
///     songs at the feel facet's plain end, its middle and its wild end: how many of its player's notes fall on the
///     figure's onsets, and how many of its bars play the figure exactly.
/// </summary>
public sealed class FigureChancesReportTest
{
    /// <summary>Each loose figure by its player, and a preset that plays it.</summary>
    private static readonly (FigurePlayer Player, string Figure, string Genre)[] Loose =
    [
        (FigurePlayer.Bass, "tumbao", "latin"), (FigurePlayer.Bass, "bossaBass", "bossa"), (FigurePlayer.Bass, "tresillo", "jazzFunk"), (FigurePlayer.Bass, "halves", "country"),
        (FigurePlayer.Chords, "backbeat", "bluegrass"), (FigurePlayer.Chords, "charleston", "jazz"), (FigurePlayer.Ground, "surdo", "bossa"), (FigurePlayer.Ground, "twoStep", "dnb"),
        (FigurePlayer.Ground, "breakbeat", "dnb"), (FigurePlayer.Backbeat, "breakbeat", "dnb")
    ];

    /// <summary>The player's tracks.</summary>
    private static int[] TracksOf(CorpusSong song, FigurePlayer player) => player switch
    {
        FigurePlayer.Ground => [..DrumGroups.Kick.Tracks],
        FigurePlayer.Backbeat => [..DrumGroups.Snare.Tracks],
        _ => [song.Song.Track(Figures.RoleOf(player))]
    };

    /// <summary>The player's notes on the figure's onsets, and its bars playing the figure exactly, over the sections it plays the figure in.</summary>
    private static (int Notes, int On, int Bars, int Exact) Measure(CorpusSong song, FigurePlayer player, string figure)
    {
        var onsets = Figures.Of(player, figure)!.Onsets.ToHashSet();
        var sections = song.Trace.Where(t => t.Point == TracePoints.Figures && ((ImmutableDictionary<FigurePlayer, string>)t.Value!).GetValueOrDefault(player) == figure)
            .Select(t => t.Section).ToHashSet();
        var tracks = TracksOf(song, player);
        var notes = song.Song.Notes!.Where(x => tracks.Contains(x.Key)).SelectMany(x => x.Value).Select(x => x.Position).ToArray();
        var (count, on, bars, exact) = (0, 0, 0, 0);
        foreach (var span in song.Map.Sections.Where(x => sections.Contains(x.SectionId) && x.Meter.Sixteenths == 16))
        foreach (var bar in span.Bars.Where(x => x.End - x.Start >= span.Meter.BarDuration - 1e-9))
        {
            var at = notes.Where(x => x >= bar.Start - 1e-9 && x < bar.End - 1e-9).Select(x => (int)Math.Round((x - bar.Start) * 4)).ToHashSet();
            if (at.Count == 0)
                continue;
            (count, on, bars, exact) = (count + at.Count, on + at.Count(onsets.Contains), bars + 1, exact + (at.SetEquals(onsets) ? 1 : 0));
        }

        return (count, on, bars, exact);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        // every loose figure measured, a figure added loose given a preset here
        var missing = Figures.All.Where(x => x.Loose).Select(x => (x.Player, x.Name)).Except(Loose.Select(x => (x.Player, x.Figure))).ToArray();
        await Assert.That(missing).IsEmpty().Because(string.Join(", ", missing));
        foreach (var (player, figure, genre) in Loose)
        foreach (var feel in new[] { 0.0, 0.5, 1 })
        {
            var given = GenrePresets.With(genre, GenreChoices.Figures[player], (figure, 1));
            var measured = TestCorpus.InParallel(Enumerable.Range(0, 32), seed => Measure(TestCorpus.Get(seed, new SongOverrides(Genre: given, Facets: TestCorpus.FacetsOf((Facet.Feel, feel)))), player, figure)).ToArray();
            var (notes, on, bars, exact) = (measured.Sum(x => x.Notes), measured.Sum(x => x.On), measured.Sum(x => x.Bars), measured.Sum(x => x.Exact));
            Console.WriteLine($"{player,-8} {figure,-10} {genre,-9} feel {feel:0.0}: {bars,4} bars, notes on the figure {on / (double)Math.Max(1, notes):P0}, bars the figure exactly {exact / (double)Math.Max(1, bars):P0}");
        }

        await Task.CompletedTask;
    }
}
