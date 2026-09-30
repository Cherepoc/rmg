using Rmg.Core.Composition;

namespace Rmg.Tests.RhythmicUnconventionalities;

/// <summary>
///     How the songs' unconventionality spreads: the base, every facet's range and how far it strays from the base, and
///     how closely the facets go together.
/// </summary>
public sealed class UnconventionalityReportTest
{
    private const int SongCount = 1024;

    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.InParallel(Enumerable.Range(0, SongCount), seed => Unconventionality.Generate(
            RhythmicUnconventionality.Generate(SongGenerator.CreateStream(seed, SongStream.Rhythm)).Value,
            facet => new Rmg.Core.Probabilities.GenerationContext(Rmg.Core.Probabilities.Seeds.Derive(Rmg.Core.Probabilities.Seeds.Derive(seed, (int)SongStream.Unconventionality), (int)facet))
        ));

        string Spread(double[] values) => $"10% {Percentile(values, 0.1):F2}, median {Percentile(values, 0.5):F2}, 90% {Percentile(values, 0.9):F2}";
        Console.WriteLine($"base: {Spread([..songs.Select(x => x.Base)])}");
        foreach (var facet in Enum.GetValues<Facet>())
        {
            var values = songs.Select(x => x[facet]).ToArray();
            var strays = songs.Select(x => Math.Abs(x[facet] - x.Base)).ToArray();
            Console.WriteLine($"{facet,-12} {Spread(values)}; from the base {strays.Average():F3} on average, 90% within {Percentile(strays, 0.9):F3}");
        }

        Console.WriteLine($"chords with groove {Correlation([..songs.Select(x => x[Facet.Chords])], [..songs.Select(x => x[Facet.Groove])]):F2}, " +
                          $"chords with the base {Correlation([..songs.Select(x => x[Facet.Chords])], [..songs.Select(x => x.Base)]):F2}");
        await Task.CompletedTask;
    }

    private static double Percentile(double[] values, double share) => values.Order().ElementAt((int)(share * (values.Length - 1)));

    private static double Correlation(double[] a, double[] b)
    {
        var (ma, mb) = (a.Average(), b.Average());
        var covariance = a.Zip(b, (x, y) => (x - ma) * (y - mb)).Sum();
        return covariance / Math.Sqrt(a.Sum(x => (x - ma) * (x - ma)) * b.Sum(y => (y - mb) * (y - mb)));
    }
}
