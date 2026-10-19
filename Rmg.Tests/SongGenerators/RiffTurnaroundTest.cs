using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.SongGenerators;

/// <summary>The riff turning round into its next cycle, its last beat walking into the cycle's first note.</summary>
public sealed class RiffTurnaroundTest
{
    /// <summary>
    ///     A riff's turnaround walks its cycle's last beat and ends, most of the time, a scale step from the note the cycle
    ///     starts on as played, three semitones at most, harmonic minor's augmented second, where no stage after the lines moved
    ///     that note, as a pedal or a riff's chords may (seen 97%, of 62 turnarounds into 452 restarts in 64 songs).
    /// </summary>
    [Test]
    public async Task ARiffsTurnaround_WalksItsLastBeat_IntoItsNextCycle()
    {
        var (walks, restarts, stepping) = (0, 0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed)))
        {
            var riff = song.Song.Track(TrackRole.Riff);
            if (!song.Song.Notes!.TryGetValue(riff, out var notes) || notes.Count == 0)
                continue;
            restarts += song.PlayedSpans.Sum(x => Math.Max(0, (int)Math.Round(x.Duration / x.Meter.PatternDuration) - 1));
            foreach (var (line, positions) in song.Trace.Where(x => x.Point == TracePoints.LineFill && x.Track == riff).Select(x => ((double Line, double[] Positions))x.Value!))
            {
                await Assert.That(positions.All(x => x >= line - 1 - 1e-9 && x < line)).IsTrue().Because($"seed {song.Seed} into {line}");
                // as played, but where a later stage took a note out or moved the cycle's first, as a stop or a push does
                var last = notes.LastOrDefault(n => Math.Abs(n.Position - positions[^1]) < 1e-6);
                var target = notes.FirstOrDefault(n => Math.Abs(n.Position - line) < 1e-6);
                if (last.Value is null || target.Value is null)
                    continue;
                walks++;
                stepping += Math.Abs(target.Value.Pitches[0] - last.Value.Pitches[0]) <= 3 ? 1 : 0;
            }
        }

        Console.WriteLine($"  {walks} turnarounds of {restarts} restarts, {stepping / (double)walks:P0} ending a step from the cycle's first note");
        await Assert.That(walks).IsGreaterThan(10);
        await Assert.That(stepping / (double)walks).IsGreaterThan(0.7);
    }
}
