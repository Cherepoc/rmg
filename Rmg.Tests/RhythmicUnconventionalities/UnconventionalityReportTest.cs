using Rmg.Core.Composition;

namespace Rmg.Tests.RhythmicUnconventionalities;

/// <summary>
///     How the songs' unconventionality spreads: the base, every facet's range and how far it strays from the base, how
///     closely the facets go together, and how often a generated song is near an end, below 0.1 or above 0.9: as a whole,
///     its facets' mean, its base, a facet, any facet, or any facet of five sections of it.
/// </summary>
public sealed class UnconventionalityReportTest
{
    private const int SongCount = 1024;

    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.InParallel(Enumerable.Range(0, SongCount), seed => Unconventionality.Generate(
            Unconventionality.DrawBase(SongGenerator.CreateStream(seed, SongStream.Rhythm)),
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

        // near an end, the song's and five of its sections' facets
        static bool Near(double value) => value < 0.1 || value > 0.9;
        string Share(Func<Unconventionality, bool> of) => $"{songs.Count(of) / (double)songs.Length:P1}";
        Console.WriteLine($"near an end: the song as a whole, its facets' mean, {Share(x => Near(x.Facets.Values.Average()))}, the base {Share(x => Near(x.Base))}, any facet {Share(x => x.Facets.Values.Any(Near))}, " +
                          $"any facet of a section {Share(x => Enumerable.Range(0, 5).Any(section => x.GenerateSection(facet => new Rmg.Core.Probabilities.GenerationContext(Rmg.Core.Probabilities.Seeds.Derive((int)(x.Base * 1e6) + section, (int)facet))).Facets.Values.Any(Near)))}; " +
                          $"by facet {string.Join(", ", Enum.GetValues<Facet>().Select(f => $"{f} {Share(x => Near(x[f]))}"))}");
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
