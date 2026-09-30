using Rmg.Core.Composition;
using Rmg.Core.Events;
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
        var spans = new Drummer(busyness, FillPath.Loop).WeighSpans(FillLayers.Spans);

        await Assert.That(WeightOf(spans, 0)).IsEqualTo(WeightOf(FillLayers.Spans, 0) * factor).Within(1e-9);
        await Assert.That(WeightOf(spans, 1)).IsEqualTo(WeightOf(FillLayers.Spans, 1)).Within(1e-9);
    }

    [Test]
    public async Task WeighSpans_FavoursLongerFills_TheBusierTheDrummer()
    {
        var spans = FillLayers.Spans;
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
    public async Task WeighPaths_FavoursTheFavourite_TheRandomWalkNeverAtThePlainEnd_AndEveryWalkAsLikelyAtTheWild()
    {
        double WeightOfPath(IEnumerable<Weighted<FillPath>> paths, FillPath path) => paths.SingleOrDefault(x => x.Value == path).Weight;
        var drummer = new Drummer(0.5, FillPath.Loop);
        var middle = drummer.WeighPaths(FillLayers.Paths, 0.5);

        await Assert.That(WeightOfPath(middle, FillPath.Loop) / WeightOfPath(middle, FillPath.OneWay))
            .IsEqualTo(WeightOfPath(FillLayers.Paths, FillPath.Loop) * Drummer.FavouriteWeight / WeightOfPath(FillLayers.Paths, FillPath.OneWay)).Within(1e-9);
        await Assert.That(WeightOfPath(drummer.WeighPaths(FillLayers.Paths, 0), FillPath.Random)).IsEqualTo(0);
        await Assert.That(drummer.WeighPaths(FillLayers.Paths, 1).Select(x => x.Weight).Distinct().Count()).IsEqualTo(1);
    }

    [Test]
    public async Task FullnessOffset_MakesBusierDrummersFuller()
    {
        await Assert.That(new Drummer(0, FillPath.OneWay).FullnessOffset).IsEqualTo(-Drummer.RunFullnessRange).Within(1e-9);
        await Assert.That(new Drummer(0.5, FillPath.OneWay).FullnessOffset).IsEqualTo(0).Within(1e-9);
        await Assert.That(new Drummer(1, FillPath.OneWay).FullnessOffset).IsEqualTo(Drummer.RunFullnessRange).Within(1e-9);
    }

    [Test]
    public async Task ASignature_NeverAtThePlainEnd_NowAndThenBetween_AndAlwaysAtTheWild()
    {
        double Share(double value)
        {
            var context = new GenerationContext(1);
            return Enumerable.Range(0, 4_000)
                .Count(_ => !Drummer.Generate(context, value).Layer!.IsDefault) / 4_000.0;
        }

        await Assert.That(Share(0)).IsEqualTo(0);
        await Assert.That(Share(0.5)).IsEqualTo(FillLayers.SignatureChance).Within(0.02);
        await Assert.That(Share(1)).IsEqualTo(1);
    }

    [Test]
    public async Task ARarerChoice_IsTheBase_AtTheFillsFacet_ItsOddsTimesTheSignature()
    {
        var context = new GenerationContext(1);
        var generator = new FillGenerator(context, SongTracks.Create(context, context, context, context, new RhythmicUnconventionality(0.5), context, context, DrumSetup.KitAndPercussion), new RhythmicUnconventionality(0.5), Meter.FourFour);
        var fade = CompositionStateKinds.Fill.FadeChance;
        var signature = new Drummer(0.5, FillPath.OneWay, StateMap.FromStates([fade.CreateState(FillLayers.SignatureWeight)]));
        var baseChance = FillLayers.Chances.Single(x => x.Kind == fade).Chance;
        var plain = generator.GetChances(new Drummer(0.5, FillPath.OneWay));

        await Assert.That(FillGenerator.GetChance(plain, fade, 0)).IsEqualTo(0);
        await Assert.That(FillGenerator.GetChance(plain, fade, 0.5)).IsEqualTo(baseChance).Within(1e-9);
        await Assert.That(FillGenerator.GetChance(plain, fade, 1)).IsEqualTo(1);
        await Assert.That(FillGenerator.GetChance(generator.GetChances(signature), fade, 0.5))
            .IsEqualTo(Tilt.Of(FillLayers.SignatureWeight, 1).Chance(baseChance, 1)).Within(1e-9);
    }

    [Test]
    public async Task RunChances_AreTheGroups_TheUnconventionalOnesByTheFillsFacet()
    {
        StateMap Drum(PercussionInstrumentDefinition drum) =>
            DrumGroups.All.Single(x => x.Drums.Contains(drum)).ConfigureStateMap(new StateMapBuilder("Test")).ToStateMap(new GenerationContext(1));

        // the snare as likely in any section, the kick never at the plain end and always at the wild
        await Assert.That(FillGenerator.GetRunChance(Drum(DrumDefinitions.AcousticSnare), 0)).IsEqualTo(FillGenerator.GetRunChance(Drum(DrumDefinitions.AcousticSnare), 1));
        await Assert.That(FillGenerator.GetRunChance(Drum(DrumDefinitions.Kick), 0)).IsEqualTo(0);
        await Assert.That(FillGenerator.GetRunChance(Drum(DrumDefinitions.Kick), 1)).IsEqualTo(1);
        await Assert.That(FillGenerator.GetRunChance(Drum(DrumDefinitions.Tom), 0.5))
            .IsGreaterThan(FillGenerator.GetRunChance(Drum(DrumDefinitions.Cymbal), 0.5));
    }

    [Test]
    public async Task Drummers_SpreadAroundTheMiddle_AndFavourAWalk()
    {
        var context = new GenerationContext(1);
        var drummers = Enumerable.Range(0, 5_000).Select(_ => Drummer.Generate(context, 0.5)).ToArray();
        var busyness = drummers.Select(x => x.Busyness).Order().ToArray();

        await Assert.That(busyness[busyness.Length / 2]).IsEqualTo(0.5).Within(0.03);
        await Assert.That(busyness.All(x => x is >= 0 and <= 1)).IsTrue();
        await Assert.That(drummers.Select(x => x.Favourite).Distinct().Count()).IsEqualTo(FillLayers.Paths.Length);
    }
}
