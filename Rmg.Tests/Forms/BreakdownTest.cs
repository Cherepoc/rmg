using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Forms;

/// <summary>A heavy band's breakdown: a bridge in half time, the kick with the riff, the riff chugging, the lines and the pad out.</summary>
public sealed class BreakdownTest
{
    /// <summary>A preset with every section that may break down broken down.</summary>
    private static Genre Always(string preset)
    {
        var genre = GenrePresets.Genre(preset);
        return genre with
        {
            Choices = genre.Choices.SetItem(GenreChoices.Breakdown.Name, ImmutableDictionary<string, ByConvention>.Empty.Add("sections", new ByConvention(1, 1, 1)))
        };
    }

    private static bool[] Breakdowns(CorpusSong song) =>
        [..song.Map.Sections.Select(x => song.Trace.Any(t => t.Point == TracePoints.Breakdown && t.Section == x.SectionId && (bool)t.Value!))];

    /// <summary>
    ///     A breakdown is a bridge or a free form's section, never the first; its drums in half time, its kick with the riff
    ///     where the song has one, and no melody, counter-melody nor pad where it plays but in a solo.
    /// </summary>
    [Test]
    public async Task ABreakdown_IsABridgeInHalfTime_TheKickWithTheRiff_TheLinesAndThePadOut()
    {
        var found = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Always("djent")))))
        {
            var structure = (SongStructure)song.Trace.Single(t => t.Point == TracePoints.SongForm).Value!;
            var breakdowns = Breakdowns(song);
            foreach (var (span, index) in song.Map.Sections.Select((x, i) => (x, i)).Where(x => breakdowns[x.i]))
            {
                found++;
                var because = $"seed {song.Seed} section {index}";
                await Assert.That(structure.Roles[span.SectionId] is SectionRole.Bridge or SectionRole.Free).IsTrue().Because(because);
                await Assert.That(span.SectionId).IsNotEqualTo(song.Map.Sections[0].SectionId).Because(because);
                await Assert.That(song.Trace.Single(t => t.Point == TracePoints.TimeFeel && t.Section == span.SectionId).Value).IsEqualTo(1).Because(because);
                var figures = (ImmutableDictionary<FigurePlayer, string>)song.Trace.Single(t => t.Point == TracePoints.Figures && t.Section == span.SectionId).Value!;
                await Assert.That(figures.GetValueOrDefault(FigurePlayer.Ground)).IsEqualTo("withRiff").Because(because);
                if (song.Solos.ContainsKey(index))
                    continue;
                var lines = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role is TrackRole.Melody or TrackRole.CounterMelody or TrackRole.Pad)
                    .SelectMany(x => x.Value).Count(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9);
                await Assert.That(lines).IsEqualTo(0).Because(because);
            }
        }

        await Assert.That(found).IsGreaterThan(4);
    }

    /// <summary>How many songs and sections break down in the heavy presets as they are, and how many could, every bridge broken down.</summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var name in new[] { "metal", "djent", "nuMetal", "rock", "blackMetal" })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(name))));
            var always = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Always(name))));
            Console.WriteLine($"{name}: songs with a breakdown {songs.Average(x => Breakdowns(x).Any(b => b) ? 1.0 : 0):P0}, of the song's time " +
                              $"{songs.Average(x => x.Map.Sections.Where((s, i) => Breakdowns(x)[i]).Sum(s => s.Duration) / x.Map.Sections.Sum(s => s.Duration)):P0}; " +
                              $"every bridge broken down {always.Average(x => Breakdowns(x).Any(b => b) ? 1.0 : 0):P0} of songs, " +
                              $"{always.Average(x => x.Map.Sections.Where((s, i) => Breakdowns(x)[i]).Sum(s => s.Duration) / x.Map.Sections.Sum(s => s.Duration)):P0} of their time");
        }

        await Task.CompletedTask;
    }
}
