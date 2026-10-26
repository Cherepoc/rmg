using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Forms;

/// <summary>The presets whose strophic and twice-through forms are cycles: their songs' lengths and sections.</summary>
public sealed class FormFoldReportTest
{
    /// <summary>Each preset's songs: the median length and its spread, the median count of sections, and the share in a free form.</summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var genre in new[] { "blues", "folk", "bluegrass", "dungeon", "bossa", "chamber", "filmPiano" })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))).ToArray();
            double[] seconds = [..songs.Select(x => x.Rendered.Seconds).Order()];
            int[] sections = [..songs.Select(x => x.Map.Sections.Length).Order()];
            var free = songs.Count(x => x.Trace.Single(t => t.Point == TracePoints.SongForm).Value is SongStructure form && form.Roles.Values.Contains(SectionRole.Free));
            Console.WriteLine($"{genre}: length {seconds[seconds.Length / 2]:F0} s, from {seconds[seconds.Length / 10]:F0} to {seconds[seconds.Length * 9 / 10]:F0} the middle eight tenths; sections {sections[sections.Length / 2]}; free {free / (double)songs.Length:P0}");
        }

        await Task.CompletedTask;
    }
}
