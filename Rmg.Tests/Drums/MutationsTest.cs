using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Drums;

/// <summary>A groove that mutates (<see cref="Mutations" />): its edits kept, the groove straying as a section goes on.</summary>
public sealed class MutationsTest
{
    /// <summary>Every drum hit of a section's bar, by its track and its place in the bar, in 32nds.</summary>
    private static HashSet<(int, int)> Bar(CorpusSong song, SectionSpan span, int bar) =>
    [
        ..song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role == TrackRole.Drum).SelectMany(x => x.Value
            .Where(n => n.Position >= span.Start + bar * span.Meter.BarDuration - 1e-9 && n.Position < span.Start + (bar + 1) * span.Meter.BarDuration - 1e-9)
            .Select(n => (x.Key, (int)Math.Round((n.Position - span.Start - bar * span.Meter.BarDuration) / 0.125))))
    ];

    [Test]
    public async Task AMiddlingSong_NeverMutates()
    {
        var checkedSections = 0;
        // a groove supplied at the middle, as it is, which a base there would let its sections stray from
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Facets: TestCorpus.FacetsOf((Facet.Groove, 0.5))))))
        foreach (var made in song.Trace.Where(x => x.Point == TracePoints.Mutations).Select(x => (((int, int))x.Value!).Item2))
        {
            await Assert.That(made).IsEqualTo(0).Because($"seed {song.Seed}");
            checkedSections++;
        }

        await Assert.That(checkedSections).IsGreaterThan(10);
    }

    /// <summary>
    ///     IDM's songs mutating against the same songs without: a polymeter's drums left as they are, and where the anchors
    ///     are kept, every bar's downbeat kick the song without struck.
    /// </summary>
    [Test]
    public async Task AMutatingGroove_LeavesAPolymeter_AndKeepsItsAnchorsBelowTheWildEnd()
    {
        var (mutated, kept, polymeters) = (0, 0, 0);
        var kicks = DrumGroups.Kick.Tracks.ToHashSet();
        foreach (var seed in Enumerable.Range(0, 32))
        {
            var genre = GenrePresets.Genre("idm");
            var with = TestCorpus.Get(seed, new SongOverrides(Genre: genre));
            var without = SongGenerator.GenerateSong(TestCorpus.SongSeed(seed), new SongOverrides(Genre: genre), System.Collections.Immutable.ImmutableHashSet.Create("Mutations"), (_, _) => { }, CancellationToken.None);
            var edits = with.Trace.Where(x => x.Point == TracePoints.Mutations).Select(x => (((int Index, int Made))x.Value!)).ToDictionary(x => x.Index, x => x.Made);
            var figured = with.Trace.Where(x => x.Point == TracePoints.Figures && ((System.Collections.ICollection)x.Value!).Count > 0).Select(x => x.Section).ToHashSet();
            var polymetric = ((System.Collections.IDictionary)with.Trace.Single(x => x.Point == TracePoints.Polymeter).Value!).Keys.Cast<int>().ToHashSet();
            foreach (var (span, index) in with.Map.Sections.Select((x, i) => (x, i)))
            {
                // the drums' notes in the section's bars after its first, as the stage edits them, with and without it
                (int, double)[] Drums(Song song, Func<int, bool> which) => [..song.Notes!.Where(x => song.TrackDefinitions[x.Key].Role == TrackRole.Drum && which(x.Key))
                    .SelectMany(x => x.Value.Where(n => n.Position >= span.Start + span.Meter.BarDuration - 1e-9 && n.Position < span.End - 1e-9).Select(n => (x.Key, Math.Round(n.Position, 6))))
                    .Order()];
                // a polymeter's drums as they were, whatever the section's edits
                if (polymetric.Contains(span.SectionId))
                {
                    polymeters++;
                    await Assert.That(Drums(with.Song, _ => true)).IsEquivalentTo(Drums(without, _ => true), CollectionOrdering.Matching).Because($"seed {seed} at {span.Start}");
                }

                if (edits.GetValueOrDefault(index) == 0)
                    continue;
                mutated++;
                // a downbeat kick of the song without kept, where the section keeps its anchors every time, its groove at the
                // middle or plainer (Mutations.KeepsAnchors)
                var groove = ((Unconventionality)with.Trace.First(x => x.Point == TracePoints.SectionUnconventionality && x.Section == span.SectionId).Value!).Facets[Facet.Groove];
                if (!figured.Contains(span.SectionId) && groove <= 0.5)
                {
                    var bars = Enumerable.Range(1, span.BarCount - 1).Select(bar => Math.Round(span.Start + bar * span.Meter.BarDuration, 6)).ToHashSet();
                    var downbeats = Drums(without, kicks.Contains).Where(x => bars.Contains(x.Item2)).Select(x => x.Item2).ToHashSet();
                    var still = Drums(with.Song, kicks.Contains).Select(x => x.Item2).ToHashSet();
                    kept += downbeats.Count;
                    await Assert.That(downbeats.All(still.Contains)).IsTrue().Because($"seed {seed} at {span.Start}");
                }
            }
        }

        Console.WriteLine($"{mutated} sections mutated, {polymeters} polymetric");
        await Assert.That(mutated).IsGreaterThan(10);
        await Assert.That(kept).IsGreaterThan(0);
        await Assert.That(polymeters).IsGreaterThan(0);
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
                var bars = span.BarCount;
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
