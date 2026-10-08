using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Drums;

/// <summary>A groove that mutates (<see cref="Mutations" />): its edits kept, the groove straying as a section goes on.</summary>
public sealed class MutationsTest
{
    /// <summary>Every drum hit of a section's bar, by its track and its place in the bar, in 16ths.</summary>
    private static HashSet<(int, int)> Bar(CorpusSong song, SectionSpan span, int bar) =>
    [
        ..song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role == TrackRole.Drum).SelectMany(x => x.Value
            .Where(n => n.Position >= span.Start + bar * span.Meter.BarDuration - 1e-9 && n.Position < span.Start + (bar + 1) * span.Meter.BarDuration - 1e-9)
            .Select(n => (x.Key, (int)Math.Round((n.Position - span.Start - bar * span.Meter.BarDuration) / 0.125))))
    ];

    [Test]
    public async Task AMiddlingSong_NeverMutates()
    {
        foreach (var song in TestCorpus.Range(32))
        foreach (var made in song.Trace.Where(x => x.Point == TracePoints.Mutations).Select(x => (((int, int))x.Value!).Item2))
            await Assert.That(made == 0 || song.Trace.Any(x => x.Point == TracePoints.Mutations)).IsTrue();
        // a groove supplied at the middle, as it is, which a base there would let its sections stray from
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Facets: System.Collections.Immutable.ImmutableDictionary<Facet, double>.Empty.Add(Facet.Groove, 0.5)))))
        foreach (var made in song.Trace.Where(x => x.Point == TracePoints.Mutations).Select(x => (((int, int))x.Value!).Item2))
            await Assert.That(made).IsEqualTo(0).Because($"seed {song.Seed}");
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var name in new[] { "idm", "dnb", "techno", "trap", "rock" })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(name))));
            var edits = songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.Mutations).Select(t => (((int, int))t.Value!).Item2)).ToArray();
            // a bar's hits not in the section's first bar's, as a share of its hits, by the bar's place, the fills' last bars left out
            var differing = new Dictionary<int, List<double>>();
            foreach (var song in songs)
            foreach (var span in song.Map.Sections)
            {
                var first = Bar(song, span, 0);
                var bars = (int)Math.Round(span.Duration / span.Meter.BarDuration);
                foreach (var bar in new[] { 1, 2, 5, 9, 13 }.Where(x => x < bars))
                {
                    var hits = Bar(song, span, bar);
                    if (hits.Count > 0)
                        (differing.TryGetValue(bar, out var list) ? list : differing[bar] = []).Add(hits.Count(x => !first.Contains(x)) / (double)hits.Count);
                }
            }

            Console.WriteLine($"{name}: sections mutating {edits.Count(x => x > 0) / (double)Math.Max(1, edits.Length):P0}, {edits.DefaultIfEmpty().Average():F1} edits an appearance; " +
                              $"hits unlike the first bar's by bar {string.Join(", ", differing.OrderBy(x => x.Key).Select(x => $"{x.Key + 1}: {x.Value.Average():P0}"))}");
        }

        await Task.CompletedTask;
    }
}
