using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.Fills;

public sealed class DrummerTest
{
    private static double WeightOf(IEnumerable<Weighted<double>> spans, double span) => spans.Single(x => x.Value == span).Weight;

    [Test]
    [Arguments(0.0, 1.5)]
    [Arguments(0.5, 1.0)]
    [Arguments(1.0, 0.5)]
    public async Task WeighSpans_MakesNoFillRarer_TheBusierTheDrummer(double busyness, double factor)
    {
        var spans = new Drummer(busyness, FillPath.Loop).WeighSpans(FillLayers.SectionSpans);

        await Assert.That(WeightOf(spans, 0)).IsEqualTo(WeightOf(FillLayers.SectionSpans, 0) * factor).Within(1e-9);
        await Assert.That(WeightOf(spans, 1)).IsEqualTo(WeightOf(FillLayers.SectionSpans, 1)).Within(1e-9);
    }

    [Test]
    public async Task WeighSpans_FavoursLongerFills_TheBusierTheDrummer()
    {
        var spans = FillLayers.SectionSpans;
        double Ratio(double busyness)
        {
            var weighed = new Drummer(busyness, FillPath.OneWay).WeighSpans(spans);
            return WeightOf(weighed, 4) / WeightOf(weighed, 1);
        }

        await Assert.That(Ratio(0.5)).IsEqualTo(WeightOf(spans, 4) / WeightOf(spans, 1)).Within(1e-9);
        await Assert.That(Ratio(1)).IsGreaterThan(Ratio(0.5));
        await Assert.That(Ratio(0)).IsLessThan(Ratio(0.5));
    }

    [Test]
    public async Task WeighPaths_FavoursTheFavourite_AndTheRandomWalkInWildSections()
    {
        double WeightOfPath(IEnumerable<Weighted<FillPath>> paths, FillPath path) => paths.Single(x => x.Value == path).Weight;
        var paths = new Drummer(0.5, FillPath.Loop).WeighPaths(FillLayers.Paths, 4);

        await Assert.That(WeightOfPath(paths, FillPath.Loop)).IsEqualTo(WeightOfPath(FillLayers.Paths, FillPath.Loop) * Drummer.FavouriteWeight).Within(1e-9);
        await Assert.That(WeightOfPath(paths, FillPath.Random)).IsEqualTo(WeightOfPath(FillLayers.Paths, FillPath.Random) * 4).Within(1e-9);
        await Assert.That(WeightOfPath(paths, FillPath.OneWay)).IsEqualTo(WeightOfPath(FillLayers.Paths, FillPath.OneWay)).Within(1e-9);
    }

    [Test]
    public async Task FullnessOffset_MakesBusierDrummersFuller()
    {
        await Assert.That(new Drummer(0, FillPath.OneWay).FullnessOffset).IsEqualTo(-Drummer.RunFullnessRange).Within(1e-9);
        await Assert.That(new Drummer(0.5, FillPath.OneWay).FullnessOffset).IsEqualTo(0).Within(1e-9);
        await Assert.That(new Drummer(1, FillPath.OneWay).FullnessOffset).IsEqualTo(Drummer.RunFullnessRange).Within(1e-9);
    }

    [Test]
    public async Task Drummers_SpreadAroundTheMiddle_AndFavourAWalk()
    {
        var context = new GenerationContext(1);
        var drummers = Enumerable.Range(0, 5_000).Select(_ => Drummer.Generate(context)).ToArray();
        var busyness = drummers.Select(x => x.Busyness).Order().ToArray();

        await Assert.That(busyness[busyness.Length / 2]).IsEqualTo(0.5).Within(0.03);
        await Assert.That(busyness.All(x => x is >= 0 and <= 1)).IsTrue();
        await Assert.That(drummers.Select(x => x.Favourite).Distinct().Count()).IsEqualTo(FillLayers.Paths.Length);
    }
}
