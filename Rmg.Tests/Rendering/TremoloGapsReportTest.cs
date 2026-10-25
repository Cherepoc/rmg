using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>The silences a tremolo-picked line leaves between its notes.</summary>
public sealed class TremoloGapsReportTest
{
    /// <summary>
    ///     In the appearances a part picks in, the gaps between one note's end and the next note's start, by their length, within
    ///     a phrase and across a phrase's end, and how many cross a change of chord, start at one, or fall in stop time
    ///     (measured over 64 songs on 25 October 2026, before a picked line was held through its rests: Black metal's riff
    ///     silent 6% of its picking appearances' phrases in 2564 gaps, its rhythm part 13% in 3627, Surf's riff 9%, its
    ///     melody 10%; after, Black metal's 1% in 220 and 2% in 143, Surf's as it was).
    /// </summary>
    [Test]
    [Explicit]
    [Arguments("blackMetal")]
    [Arguments("surf")]
    public async Task Report(string genre)
    {
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))).ToArray();
        foreach (var role in new[] { TrackRole.Riff, TrackRole.Rhythm, TrackRole.Melody })
        {
            var (within, across, changing, atChange, stopped) = (new List<double>(), new List<double>(), 0, 0, 0);
            var picked = 0.0;
            foreach (var song in songs.Where(x => x.Song.TrackDefinitions.Values.Any(t => t.Role == role)))
            {
                var notes = song.Song.Notes!.GetValueOrDefault(song.Track(role))?.ToArray() ?? [];
                // stop time's phrases and a bar of ensemble hits, whose rests are the stop's
                var stops = song.Trace.Where(x => x.Point == TracePoints.StopTime).SelectMany(x => (System.Collections.Immutable.ImmutableArray<double>)x.Value!).ToArray();
                var hits = song.Trace.Where(x => x.Point == TracePoints.EnsembleHits).SelectMany(x => (System.Collections.Immutable.ImmutableArray<double>)x.Value!).ToArray();
                bool Stopped(double at) => song.Map.Sections.Any(s => stops.Any(x => Math.Abs(x - s.Start) < 1e-6) && at >= s.Start - 1e-9 && at < s.Start + s.Meter.PhraseDuration - 1e-9)
                                           || song.Map.Sections.Any(s => hits.Any(x => at >= x - 1e-9 && at < x + s.Meter.BarDuration - 1e-9 && s.IsWithin(x)));
                var picking = notes.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Picked) > 0).Select(x => song.Map.PlayingAt(x.Position)?.Index).ToHashSet();
                foreach (var (a, b) in notes.Zip(notes.Skip(1)))
                {
                    if (song.Map.PlayingAt(a.Position) is not { } play || !picking.Contains(play.Index) || song.Map.PlayingAt(b.Position)?.Index != play.Index)
                        continue;
                    picked += b.Position - a.Position;
                    var gap = b.Position - (a.Position + a.Value.Duration);
                    if (gap < 1e-6)
                        continue;
                    var phrase = play.Span.Phrases.First(p => a.Position >= p.Start - 1e-9 && a.Position < p.End - 1e-9);
                    (b.Position < phrase.End - 1e-9 ? within : across).Add(gap);
                    changing += song.ChordChanges.Any(x => x > a.Position + a.Value.Duration + 1e-9 && x < b.Position - 1e-9) ? 1 : 0;
                    atChange += song.ChordChanges.Any(x => Math.Abs(x - (a.Position + a.Value.Duration)) < 1e-6) ? 1 : 0;
                    stopped += Stopped(a.Position + a.Value.Duration) ? 1 : 0;
                }
            }

            string Of(List<double> gaps) => $"{gaps.Count} ({gaps.Sum() / Math.Max(picked, 1e-9):P0} of the time), {string.Join(", ", new[] { 0.25, 0.5, 1, 2, 4, double.MaxValue }.Select((x, i) => (Up: x, From: i == 0 ? 0 : new[] { 0.25, 0.5, 1, 2, 4 }[i - 1])).Select(x => $"to {x.Up}: {gaps.Count(g => g > x.From + 1e-9 && g <= x.Up + 1e-9)}"))}";
            Console.WriteLine($"{genre} {role}: within a phrase {Of(within)}; across its end {Of(across)}; {changing} across a chord change, {atChange} from one, {stopped} in stop time");
        }

        await Task.CompletedTask;
    }
}
