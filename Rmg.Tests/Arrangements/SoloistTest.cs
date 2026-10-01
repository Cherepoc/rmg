using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.Arrangements;

/// <summary>A soloist's vocabulary (<see cref="Soloist" />): a plain one's runs of 8ths and 16ths in cells of three or four, a wild one's of any speed and size, grouped.</summary>
public sealed class SoloistTest
{
    private static Soloist[] Draw(double conventionality) =>
        [..Enumerable.Range(0, 400).Select(seed => Soloist.Generate(new GenerationContext((ulong)seed), conventionality, conventionality))];

    [Test]
    public async Task APlainSoloist_RunsIn8thsAnd16ths_InCellsOfThreeOrFour_TheScaleASequenceOrTheChord()
    {
        foreach (var soloist in Draw(0))
        {
            await Assert.That(soloist.Speeds.All(x => x is 2 or 4)).IsTrue();
            await Assert.That(soloist.CellSizes.Length).IsEqualTo(1);
            await Assert.That(soloist.CellSizes[0] is 3 or 4).IsTrue();
            await Assert.That(soloist.Groups).IsFalse();
            await Assert.That(soloist.Shapes.All(x => x is SoloShape.Scale or SoloShape.Sequence or SoloShape.Arpeggio)).IsTrue();
        }
    }

    [Test]
    public async Task AWildSoloist_RunsFaster_InOddCells_Grouped_AndSwingsMore()
    {
        var wild = Draw(1);
        var plain = Draw(0);

        await Assert.That(wild.Count(x => x.Speeds.Contains(6) || x.Speeds.Contains(8))).IsGreaterThan(wild.Length / 2);
        await Assert.That(wild.Count(x => x.CellSizes.Any(size => size is 5 or 7 or 9))).IsGreaterThan(wild.Length / 3);
        await Assert.That(wild.Count(x => x.Groups)).IsGreaterThan(wild.Length / 2);
        await Assert.That(wild.Average(x => x.Volatility)).IsGreaterThan(plain.Average(x => x.Volatility) * 4);
        await Assert.That(wild.Average(x => x.RegisterSteps)).IsGreaterThan(plain.Average(x => x.RegisterSteps));
        // and soloists differ, one to the next
        await Assert.That(wild.Select(x => string.Join(",", x.CellSizes)).Distinct().Count()).IsGreaterThan(10);
    }

    /// <summary>The soloists of the corpus, by fifth of the melody facet: their speeds, their cells, their grouping and how they swing.</summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        var soloists = TestCorpus.Measure(512, song => song.Trace.Where(x => x.Point == TracePoints.Soloist)
            .Select(x => (Fifth: Math.Clamp((int)(((Unconventionality)song.Trace.Single(t => t.Point == TracePoints.SongUnconventionality).Value!)[Facet.Melody] * 5), 0, 4),
                Soloist: (((TrackRole, Soloist))x.Value!).Item2))
            .ToArray()).SelectMany(x => x).ToArray();
        foreach (var band in soloists.GroupBy(x => x.Fifth).OrderBy(x => x.Key))
        {
            var all = band.Select(x => x.Soloist).ToArray();
            string Share<T>(IEnumerable<T> values) => string.Join(" ", values.GroupBy(x => x).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count() / (double)all.Length:P0}"));
            Console.WriteLine($"  melody {band.Key}/5, {all.Length} soloists: speeds {Share(all.SelectMany(x => x.Speeds))}; cells {Share(all.SelectMany(x => x.CellSizes))}; " +
                              $"grouping {all.Count(x => x.Groups) / (double)all.Length:P0}; volatility {all.Average(x => x.Volatility):F2}, pauses {all.Average(x => x.PauseRate):F2}, register {all.Average(x => x.RegisterSteps):F1} steps");
        }

        await Task.CompletedTask;
    }
}
