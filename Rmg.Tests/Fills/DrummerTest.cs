using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.Fills;

public sealed class DrummerTest
{
    private static double WeightOf(IEnumerable<Weighted<FillKind>> fills, FillKind kind) => fills.Single(x => x.Value == kind).Weight;

    [Test]
    [Arguments(0.0, 1.5)]
    [Arguments(0.5, 1.0)]
    [Arguments(1.0, 0.5)]
    public async Task Weigh_MakesNoFillRarer_TheBusierTheDrummer(double busyness, double factor)
    {
        var fills = new Drummer(busyness, FillKind.TomRun).Weigh(FillLayers.SectionFills);

        await Assert.That(WeightOf(fills, FillKind.None)).IsEqualTo(WeightOf(FillLayers.SectionFills, FillKind.None) * factor).Within(1e-9);
        await Assert.That(WeightOf(fills, FillKind.TomRun))
            .IsEqualTo(WeightOf(FillLayers.SectionFills, FillKind.TomRun) * Drummer.FavouriteWeight)
            .Within(1e-9);
        await Assert.That(WeightOf(fills, FillKind.Pickup)).IsEqualTo(WeightOf(FillLayers.SectionFills, FillKind.Pickup)).Within(1e-9);
    }

    [Test]
    public async Task WeighSpans_FavoursLongerFills_TheBusierTheDrummer()
    {
        var spans = FillLayers.Spans[FillKind.TomRun];
        double Ratio(double busyness)
        {
            var weighed = new Drummer(busyness, FillKind.None).WeighSpans(spans);
            return weighed.Single(x => x.Value == 4).Weight / weighed.Single(x => x.Value == 1).Weight;
        }

        await Assert.That(Ratio(0.5)).IsEqualTo(spans.Single(x => x.Value == 4).Weight / spans.Single(x => x.Value == 1).Weight).Within(1e-9);
        await Assert.That(Ratio(1)).IsGreaterThan(Ratio(0.5));
        await Assert.That(Ratio(0)).IsLessThan(Ratio(0.5));
    }

    [Test]
    public async Task RunFullness_StaysAChance()
    {
        await Assert.That(new Drummer(0, FillKind.None).RunFullness).IsBetween(0.5, FillLayers.RunFullness);
        await Assert.That(new Drummer(1, FillKind.None).RunFullness).IsBetween(FillLayers.RunFullness, 1);
    }

    [Test]
    public async Task Drummers_SpreadAroundTheMiddle_AndFavourAFill()
    {
        var context = new GenerationContext(1);
        var drummers = Enumerable.Range(0, 5_000).Select(_ => Drummer.Generate(context)).ToArray();
        var busyness = drummers.Select(x => x.Busyness).Order().ToArray();

        await Assert.That(busyness[busyness.Length / 2]).IsEqualTo(0.5).Within(0.03);
        await Assert.That(busyness.All(x => x is >= 0 and <= 1)).IsTrue();
        await Assert.That(drummers.Any(x => x.Favourite == FillKind.None)).IsFalse();
        await Assert.That(drummers.Select(x => x.Favourite).Distinct().Count()).IsEqualTo(FillLayers.SectionFills.Length - 1);
    }
}
