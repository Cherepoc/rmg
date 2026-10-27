using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>A line's last bar before a louder section picked faster and faster into it (<see cref="ArticulationChoice.BuildFrom" />).</summary>
public sealed class TremoloBuildReportTest
{
    /// <summary>A genre's songs' appearances followed by another, the parts building into the next, and a build's strokes a second in each quarter of its bar.</summary>
    [Test]
    [Explicit]
    [Arguments("postRock")]
    [Arguments("surf")]
    [Arguments("blackMetal")]
    public async Task Report(string genre)
    {
        var (lifts, builds) = (0, 0);
        var rates = Enumerable.Range(0, 4).Select(_ => new List<double>()).ToArray();
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))))
        {
            lifts += song.Map.Sections.Length - 1;
            builds += song.Trace.Count(x => x.Point == TracePoints.TremoloBuild);
            foreach (var (_, timeline) in song.Song.Notes!)
            {
                var strokes = timeline.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Articulation).Value.BuildFrom > 0
                                                  && x.Value.State.GetStateValue(CompositionStateKinds.Picked) > 0).ToArray();
                foreach (var (a, b) in strokes.Zip(strokes.Skip(1)).Where(x => x.Second.Value.State.GetStateValue(CompositionStateKinds.Picked) == x.First.Value.State.GetStateValue(CompositionStateKinds.Picked) + 1))
                {
                    var place = song.Map.PlaceOf(a.Position);
                    rates[Math.Min(3, (int)Math.Floor(place.Beat / (place.Meter.BarDuration / 4) + 1e-9))].Add(1 / (song.Rendered.SecondsAt(b.Position) - song.Rendered.SecondsAt(a.Position)));
                }
            }
        }

        Console.WriteLine($"{genre}: {lifts} appearances followed by another, {builds} parts building into the next; strokes a second by quarter: {string.Join(", ", rates.Select(x => x.Count == 0 ? "-" : $"{x.Order().ElementAt(x.Count / 2):F1} ({x.Count})"))}");
        await Task.CompletedTask;
    }
}
