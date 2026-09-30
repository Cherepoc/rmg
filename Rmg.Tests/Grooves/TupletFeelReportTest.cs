using Rmg.Core.Composition;

namespace Rmg.Tests.Grooves;

/// <summary>
///     How a song's drums play in tuplets, by the feel facet of its unconventionality, in fifths: the share of its drum bars in a tuplet, and
///     whether one tuplet holds the song or its bars change between straight time and tuplets, a pattern's last bar, where a
///     fill may play, left out.
/// </summary>
public sealed class TupletFeelReportTest
{
    private const int SongCount = 256;

    private sealed record SongFeel(int Rhythm, int Bars, int TupletBars, int MainTupletBars, int Tuplets, int Changes, int[] Primes);

    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Measure(SongCount, Measure);
        foreach (var band in songs.GroupBy(x => x.Rhythm).OrderBy(x => x.Key))
        {
            var all = band.ToArray();
            var with = all.Where(x => x.TupletBars > 0).ToArray();
            string Count(Func<SongFeel, bool> of) => $"{with.Count(of)}";
            Console.WriteLine($"feel {band.Key}/5, {all.Length} songs: {all.Sum(x => x.TupletBars) / (double)all.Sum(x => x.Bars):P0} of drum bars in a tuplet; " +
                              $"{with.Length} songs with any, of them in a tuplet in under a tenth of their bars {Count(x => x.TupletBars < x.Bars / 10.0)}, " +
                              $"a tenth to a half {Count(x => x.TupletBars >= x.Bars / 10.0 && x.TupletBars < x.Bars / 2.0)}, over half {Count(x => x.TupletBars >= x.Bars / 2.0)}; " +
                              $"their main tuplet {with.Sum(x => x.MainTupletBars) / (double)Math.Max(1, with.Sum(x => x.TupletBars)):P0} of their tuplet bars, " +
                              $"{with.Average(x => x.Tuplets):F1} tuplets a song, a drum changing between straight and tuplet {with.Average(x => x.Changes):F1} times a song");
            // the tuplets played, by the step of the period they are: -1 a dotted 8th's 3+3+2, 1 triplets, -2 quintuplets
            var tupletBars = all.SelectMany(x => x.Primes).Where(x => x != 0).ToArray();
            Console.WriteLine($"  tuplet bars by step: {string.Join(", ", tupletBars.GroupBy(x => x).OrderBy(x => x.Key).Select(x => $"{x.Key}: {x.Count() / (double)tupletBars.Length:P0} ({RhythmPeriodOf(x.Key)})"))}");
        }

        await Task.CompletedTask;
    }

    private static string RhythmPeriodOf(int step)
    {
        var tuplet = Rmg.Core.Probabilities.RhythmPeriod.ToTuplet(step);
        var factor = Rmg.Core.Probabilities.RhythmPeriod.ToRhythmPeriodValue(step);
        return tuplet != 1 ? $"{tuplet}-tuplet" : $"x{factor:0.###} grouped";
    }

    private static SongFeel Measure(CorpusSong song)
    {
        // the song's feel facet, in fifths
        var rhythm = Math.Clamp((int)(((Unconventionality)song.Trace.Single(x => x.Point == TracePoints.SongUnconventionality).Value!)[Facet.Feel] * 5), 0, 4);
        // every drum's bars in the order the song plays its sections, each section's bars as it first made them
        var entries = song.Trace
            .Where(x => x.Point == TracePoints.BarPattern && song.Song.TrackDefinitions[x.Track].Role == Rmg.Core.Songs.TrackRole.Drum && x.Bar % Meter.PatternBarCount != Meter.PatternBarCount - 1)
            .ToLookup(x => (x.Section, x.Track));
        var tracks = entries.Select(x => x.Key.Track).Distinct();
        var primes = new List<int>();
        var changes = 0;
        foreach (var track in tracks)
        {
            var previous = (int?)null;
            foreach (var span in song.Map.Sections)
            foreach (var entry in entries[(span.SectionId, track)].OrderBy(x => x.Bar))
            {
                var prime = Feels.Of(entry.StateMap);
                primes.Add(prime);
                if (previous is { } p && p == 0 != (prime == 0))
                    changes++;
                previous = prime;
            }
        }

        var tuplets = primes.Where(x => x != 0).ToArray();
        return new SongFeel(
            rhythm,
            primes.Count,
            tuplets.Length,
            tuplets.Length == 0 ? 0 : tuplets.GroupBy(x => x).Max(x => x.Count()),
            tuplets.Distinct().Count(),
            changes,
            [..primes]
        );
    }
}
