using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Arrangements;

/// <summary>A soloist's vocabulary (<see cref="Soloist" />): a plain one's runs of 8ths and 16ths in cells of three or four, a wild one's of any speed and size, grouped.</summary>
public sealed class SoloistTest
{
    /// <summary>Distortion Guitar and Alto Sax, the one likelier to bend than the other.</summary>
    private const int Guitar = 30, Sax = 65;

    private static Soloist[] Draw(double conventionality, int program = Guitar) =>
        [..Enumerable.Range(0, 400).Select(seed => Soloist.Generate(new GenerationContext((ulong)seed), conventionality, conventionality, program, null, 1))];

    [Test]
    public async Task AWildSoloist_MayShredIn64ths_ForABurstOrForBars_APlainOneNever()
    {
        var (plain, wild) = (Draw(0), Draw(1));

        await Assert.That(plain.Any(x => x.Speeds.Any(speed => speed > 8))).IsFalse();
        await Assert.That(wild.Count(x => x.Speeds.Contains(16))).IsGreaterThan(wild.Length / 5);
        await Assert.That(wild.Count(x => x.Speeds.Contains(12))).IsGreaterThan(wild.Length / 3);
        // a plain one's stamina a beat, a wild one's anything from a burst to bars
        await Assert.That(plain.All(x => x.Stamina is > 0.9 and < 1.1)).IsTrue();
        await Assert.That(wild.Max(x => x.Stamina) / wild.Min(x => x.Stamina)).IsGreaterThan(6);
        await Assert.That(wild.Max(x => x.Stamina)).IsGreaterThan(8);
    }

    [Test]
    public async Task ASoloistChoosesToBendAndToSingAVibrato_NeverAtThePlainEnd_AlwaysAtTheWild_AGuitaristTheLikelierToBend()
    {
        await Assert.That(Draw(0).Any(x => x.Bends || x.Vibrates)).IsFalse();
        await Assert.That(Draw(1).All(x => x.Bends && x.Vibrates)).IsTrue();
        await Assert.That(Draw(0.5).Count(x => x.Bends)).IsGreaterThan(Draw(0.5, Sax).Count(x => x.Bends) + 40);
    }

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
            .Select(x => (Fifth: Math.Clamp((int)(song.Unconventionality[Facet.Melody] * 5), 0, 4),
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
    /// <summary>Every pitched soloist of a song, its instrument's family, a guitar, a synth lead or another, and whether it bends.</summary>
    private static IEnumerable<(string Family, bool Bends)> SoloistsOf(CorpusSong song) => song.Soloists.Where(x => x.Key != TrackRole.Drum).Select(x =>
        (song.Song.TrackDefinitions.Values.OfType<PitchInstrumentTrack>().Single(t => t.Role == x.Key).InstrumentCode switch
        {
            24 or 26 => "nylon or jazz guitar",
            >= 24 and <= 31 => "guitar",
            >= 80 and <= 87 => "synth lead",
            _ => "another"
        }, x.Value.Bends));

    [Test]
    public async Task AGenresSoloists_BendAsItHasThem_NeverWhereItGivesThemNone_ABluesPlayerFarOftenerThanAJazzOne()
    {
        IEnumerable<(string Family, bool Bends)> Of(string genre) =>
            TestCorpus.InParallel(Enumerable.Range(0, 32), seed => SoloistsOf(TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))).ToArray()).SelectMany(x => x);
        var bossa = Of("bossa").ToArray();
        await Assert.That(bossa.Length).IsGreaterThan(10);
        await Assert.That(bossa.Any(x => x.Bends)).IsFalse();
        static double Share(IEnumerable<(string, bool Bends)> some) => some.ToArray() is var all ? all.Count(x => x.Bends) / (double)Math.Max(1, all.Length) : 0;
        var (blues, jazz) = (Share(Of("blues")), Share(Of("jazz")));
        Console.WriteLine($"soloists bending: Blues {blues:P0}, Jazz {jazz:P0}");
        await Assert.That(blues).IsGreaterThan(jazz + 0.3);
    }

    /// <summary>The soloists that bend at all, by genre and by their instrument's family.</summary>
    [Test]
    [Explicit]
    public async Task BendsReport()
    {
        foreach (var name in new string?[] { null, "blues", "rock", "metal", "jazz", "bossa", "nuMetal", "country", "orchestral", "celtic" })
        {
            var soloists = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => SoloistsOf(name is null ? TestCorpus.Get(seed) : TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(name)))).ToArray())
                .SelectMany(x => x).ToArray();
            Console.WriteLine($"{name ?? "the corpus"}: bending {soloists.Count(x => x.Bends) / (double)Math.Max(1, soloists.Length):P0} of {soloists.Length}; " +
                              string.Join(", ", soloists.GroupBy(x => x.Family).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count(s => s.Bends) / (double)x.Count():P0} of {x.Count()}")));
        }

        await Task.CompletedTask;
    }
}
