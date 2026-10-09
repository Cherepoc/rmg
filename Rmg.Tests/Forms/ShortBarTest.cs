using Rmg.Core.Composition;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Forms;

/// <summary>A section's last bar cut short before the next, as a 2/4 bar before a chorus (<see cref="ShortBars" />).</summary>
public sealed class ShortBarTest
{
    [Test]
    public async Task ThePlainestSongs_CutNoBar()
    {
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Base: 0))))
            await Assert.That(song.Map.Sections.All(x => x.Cut == 0)).IsTrue().Because($"seed {song.Seed}");
    }

    /// <summary>
    ///     A cut section's last bar is its meter's first half of groups, never the song's last section's, wherever the
    ///     section is drawn to cut it, the file signing the short bar where it starts.
    /// </summary>
    [Test]
    public async Task ACutSection_EndsOnItsMetersShortBar_TheFileSigningIt()
    {
        var cut = 0;
        foreach (var song in TestCorpus.Range(128))
        {
            var drawn = (System.Collections.Immutable.ImmutableHashSet<int>)song.Trace.Single(x => x.Point == TracePoints.ShortBars).Value!;
            var map = song.Map;
            await Assert.That(map.Sections[^1].Cut).IsEqualTo(0);
            foreach (var (span, index) in map.Sections.Select((x, i) => (x, i)))
            {
                var because = $"seed {song.Seed} at {span.Start}";
                var countsBars = index == 0 && map.Intro.Kind == IntroKind.Entries && !map.Intro.Window.IsBefore;
                var expected = drawn.Contains(span.SectionId) && index < map.Sections.Length - 1 && !countsBars;
                await Assert.That(span.Cut > 0).IsEqualTo(expected).Because(because);
                if (span.Cut == 0)
                    continue;
                cut++;
                var bar = ShortBars.Of(span.Meter);
                await Assert.That(span.Cut).IsEqualTo(span.Meter.BarDuration - bar.BarDuration).Within(1e-9).Because(because);
                await Assert.That(map.MeterAt(span.End - 1e-6).BarDuration).IsEqualTo(bar.BarDuration).Within(1e-9).Because(because);
                await Assert.That(song.Rendered.Meters.Any(x => Math.Abs(x.Position - (span.End - bar.BarDuration)) < 1e-9 && x.Meter.HasSameBar(bar))).IsTrue().Because(because);
            }
        }

        Console.WriteLine($"{cut} sections' appearances cut short in 128 songs");
        await Assert.That(cut).IsGreaterThan(10);
    }

    /// <summary>The sections cut short, by fifths of the form facet and by genre.</summary>
    [Test]
    [Arguments("none")]
    [Arguments("djent")]
    [Arguments("orchestral")]
    [Explicit]
    public async Task Report(string id)
    {
        var genre = id == "none" ? Genre.None : GenrePresets.Genre(id);
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre)));
        var spans = songs.SelectMany(s => s.Map.Sections.Take(s.Map.Sections.Length - 1).Select(x => (Fifth: Math.Clamp((int)(s.Unconventionality[Facet.Form] * 5), 0, 4), Cut: x.Cut > 0))).ToArray();
        Console.WriteLine($"{id}: " + string.Join(", ", spans.GroupBy(x => x.Fifth).OrderBy(x => x.Key).Select(x => $"fifth {x.Key} {x.Count(y => y.Cut) / (double)x.Count():P0} of {x.Count()}")));
        await Task.CompletedTask;
    }
}
