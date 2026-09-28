using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.MelodyRhythms;

/// <summary>
///     How closely the melody follows the shape its phrases aim at: within every 4-bar pattern, each bar's mean pitch
///     against the register the bar aims at, both from the pattern's mean, so that where the phrase sits and the octave
///     it plays in are left out.
/// </summary>
public sealed class MelodyContourTest
{
    private const int SongCount = 100;

    /// <param name="Bars">Bars with a melody note, in patterns with two or more of them.</param>
    /// <param name="Correlation">Of a bar's mean pitch with its aim, both from their pattern's mean.</param>
    /// <param name="Slope">How many semitones the bar's mean pitch moves for one of its aim.</param>
    /// <param name="Waves">Patterns whose aim turns more than once, such as up, down and up.</param>
    internal sealed record Measures(int Bars, double Correlation, double Slope, int Patterns, int Waves);

    internal static Measures Measure(IEnumerable<CorpusSong> songs)
    {
        var pairs = new List<(double Aim, double Pitch)>();
        int patterns = 0, waves = 0;
        foreach (var song in songs)
        {
            var melody = song.Song.Notes![SongTracks.MelodyTrack];
            var contours = song.Trace.Where(x => x.Point == TracePoints.MelodyContour).ToDictionary(x => x.Section, x => (ImmutableArray<double>)x.Value!);
            foreach (var span in song.Map.Sections)
            for (var start = span.Start; start < span.End - 1e-9; start += Meter.PatternDuration)
            {
                var contour = contours[span.SectionId];
                var bars = contour
                    .Select((aim, bar) =>
                        {
                            var from = start + bar * Meter.BarDuration;
                            var notes = melody.Where(x => x.Position >= from && x.Position < from + Meter.BarDuration).ToArray();
                            return (Aim: aim, Pitch: notes.Length == 0 ? (double?)null : notes.Average(x => x.Value.Pitches[0]));
                        }
                    )
                    .Where(x => x.Pitch is not null)
                    .Select(x => (x.Aim, Pitch: x.Pitch!.Value))
                    .ToArray();
                if (bars.Length < 2)
                    continue;

                patterns++;
                waves += CountTurns([..contour]) > 1 ? 1 : 0;
                var (aimMean, pitchMean) = (bars.Average(x => x.Aim), bars.Average(x => x.Pitch));
                pairs.AddRange(bars.Select(x => (x.Aim - aimMean, x.Pitch - pitchMean)));
            }
        }

        var covariance = pairs.Sum(x => x.Aim * x.Pitch);
        var aimVariance = pairs.Sum(x => x.Aim * x.Aim);
        var pitchVariance = pairs.Sum(x => x.Pitch * x.Pitch);
        return new Measures(pairs.Count, covariance / Math.Sqrt(aimVariance * pitchVariance), covariance / aimVariance, patterns, waves);
    }

    private static int CountTurns(double[] values)
    {
        var moves = values.Zip(values.Skip(1), (a, b) => Math.Sign(b - a)).Where(x => x != 0).ToArray();
        return moves.Zip(moves.Skip(1)).Count(x => x.First != x.Second);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var m = Measure(TestCorpus.Range(SongCount));
        Console.WriteLine($"{m.Bars} bars: a bar's mean pitch follows its aim {m.Correlation:F2} (correlation), " +
                          $"{m.Slope:F2} semitones for one; waves {m.Waves / (double)m.Patterns:P0} of {m.Patterns} patterns");
        await Task.CompletedTask;
    }
}
