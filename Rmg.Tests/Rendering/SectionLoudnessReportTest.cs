namespace Rmg.Tests.Rendering;

/// <summary>
///     How far a song's sections stray in loudness: every section's notes, of every track, as loud as their velocities
///     sound (40·log10 of the velocity), on average, against the song's loudest section.
/// </summary>
public sealed class SectionLoudnessReportTest
{
    private const int SongCount = 128;

    /// <param name="Gaps">Every song's gap from its loudest section to its quietest, in dB.</param>
    /// <param name="Under">Every section's loudness under its song's loudest, in dB.</param>
    internal sealed record Measures(double[] Gaps, double[] Under);

    internal static Measures Measure(IEnumerable<CorpusSong> songs)
    {
        var gaps = new List<double>();
        var under = new List<double>();
        foreach (var song in songs)
        {
            var notes = song.Rendered.Tracks.SelectMany(x => x.NoteTimeline).ToArray();
            var loudness = song.Map.Sections
                .Select(span => notes.Where(x => x.Position >= span.Start && x.Position < span.End).Select(x => 40 * Math.Log10(Math.Max(1, x.Value.Velocity * 127) / 127)).ToArray())
                .Where(x => x.Length > 0)
                .Select(x => x.Average())
                .ToArray();
            if (loudness.Length < 2)
                continue;

            gaps.Add(loudness.Max() - loudness.Min());
            under.AddRange(loudness.Select(x => loudness.Max() - x));
        }

        return new Measures([..gaps.Order()], [..under.Order()]);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var m = Measure(TestCorpus.Range(SongCount));
        Console.WriteLine($"a song's loudest section over its quietest: median {m.Gaps[m.Gaps.Length / 2]:F1} dB, 90% {m.Gaps[m.Gaps.Length * 9 / 10]:F1}, at most {m.Gaps[^1]:F1}; " +
                          $"sections more than 6 dB under their song's loudest {m.Under.Count(x => x > 6) / (double)m.Under.Length:P0}, more than 3 dB {m.Under.Count(x => x > 3) / (double)m.Under.Length:P0}");
        await Task.CompletedTask;
    }
}
