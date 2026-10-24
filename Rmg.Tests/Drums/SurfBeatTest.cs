using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Drums;

/// <summary>Surf's beat, the kick on 1 and 3 and the snare on 2, the 2-and and 4.</summary>
public sealed class SurfBeatTest
{
    /// <summary>
    ///     Surf's kick and snare by the 16th of the bar they strike on, the share of its sections' bars struck there, a drum
    ///     solo's left out, and the share of its sections playing each figure the kick and the snare draw (measured over 64 songs
    ///     on 25 October 2026, before its beat: the snare 53% to 57% on every beat and its 2-and 43% as its other ands; with it,
    ///     the kick on 1 and 3 80% and on 2 and 4 11%, the snare on 2, its 2-and and 4 72% to 76% and on 1 and 3 23%, the surf
    ///     beat 63% of the sections).
    /// </summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("surf")))).ToArray();
        foreach (var (name, tracks) in new[] { ("kick", DrumGroups.Kick.Tracks.ToHashSet()), ("snare", DrumGroups.Snare.Tracks.ToHashSet()) })
        {
            var struck = new int[16];
            var bars = 0;
            foreach (var song in songs)
            {
                var solos = song.Solos.Where(x => x.Value.IsDrumSolo).Select(x => x.Key).ToHashSet();
                var notes = song.Song.Notes!.Where(x => tracks.Contains(x.Key)).SelectMany(x => x.Value).Select(x => x.Position).ToArray();
                foreach (var (span, index) in song.Map.Sections.Select((x, i) => (x, i)).Where(x => !solos.Contains(x.i) && x.x.Meter.Equals(Meter.FourFour)))
                    for (var bar = span.Start; bar < span.End - 1e-9; bar += span.Meter.BarDuration)
                    {
                        bars++;
                        foreach (var slot in notes.Where(x => x >= bar - 1e-9 && x < bar + span.Meter.BarDuration - 1e-9).Select(x => (int)Math.Round((x - bar) * 4)).Where(x => x is >= 0 and < 16).Distinct())
                            struck[slot]++;
                    }
            }

            Console.WriteLine($"{name}: " + string.Join("  ", struck.Select((x, i) => $"{(i % 4 == 0 ? $"{i / 4 + 1}" : i % 4 == 2 ? "&" : "e")} {x / (double)bars:P0}")));
        }

        var figures = songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.Figures).Select(t => (System.Collections.Immutable.ImmutableDictionary<FigurePlayer, string>)t.Value!)).ToArray();
        foreach (var player in new[] { FigurePlayer.Ground, FigurePlayer.Backbeat })
            Console.WriteLine($"{player}: {string.Join(", ", figures.GroupBy(x => x.GetValueOrDefault(player) ?? "own").OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count() / (double)figures.Length:P0}"))}");
        await Task.CompletedTask;
    }
}
