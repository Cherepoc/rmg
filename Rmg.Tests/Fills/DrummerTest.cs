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
    public async Task WeighPaths_FavoursTheFavourite_AndTheRandomWalkInWildSections()
    {
        double WeightOfPath(IEnumerable<Weighted<FillPath>> paths, FillPath path) => paths.Single(x => x.Value == path).Weight;
        var paths = new Drummer(0.5, FillPath.Loop).WeighPaths(FillLayers.Paths, Tilt.Of(4, 1));

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
    public async Task WildSongs_HaveASignatureMoreOften()
    {
        double Share(double value)
        {
            var context = new GenerationContext(1);
            return Enumerable.Range(0, 4_000)
                .Count(_ => !Drummer.Generate(context, new RhythmicUnconventionality(value)).Layer!.IsDefault) / 4_000.0;
        }

        await Assert.That(Share(0.5)).IsEqualTo(FillLayers.SignatureChance).Within(0.02);
        await Assert.That(Share(1)).IsEqualTo(Tilt.Of(4, 1).Chance(FillLayers.SignatureChance, 1)).Within(0.03);
        await Assert.That(Share(0)).IsLessThan(Share(0.5));
    }

    [Test]
    public async Task Chances_AreTheBase_TimesTheSignature_AndTheSectionsChanceScale()
    {
        var context = new GenerationContext(1);
        var generator = new FillGenerator(context, SongTracks.Create(context, context, context, context, new RhythmicUnconventionality(0.5), context, context, DrumSetup.KitAndPercussion), new RhythmicUnconventionality(0.5), Meter.FourFour);
        var fade = CompositionStateKinds.Fill.FadeChance;
        var signature = new Drummer(0.5, FillPath.OneWay, StateMap.FromStates([fade.CreateState(FillLayers.SignatureWeight)]));
        var baseChance = FillLayers.Chances.Single(x => x.Kind == fade).Chance;
        var wild = new RhythmicUnconventionality(1);

        await Assert.That(generator.GetChances(new Drummer(0.5, FillPath.OneWay), new RhythmicUnconventionality(0.5)).GetStateValue(fade))
            .IsEqualTo(baseChance).Within(1e-9);
        await Assert.That(generator.GetChances(signature, wild).GetStateValue(fade))
            .IsEqualTo(baseChance * FillLayers.SignatureWeight * wild.ChanceScale).Within(1e-9);
    }

    [Test]
    public async Task RunChances_AreTheGroups_TheUnconventionalOnesScaledByTheSection()
    {
        StateMap Drum(PercussionInstrumentDefinition drum) =>
            DrumGroups.All.Single(x => x.Drums.Contains(drum)).ConfigureStateMap(new StateMapBuilder("Test")).ToStateMap(new GenerationContext(1));

        // the snare as likely in any section, the kick at four times the odds in a wild one
        await Assert.That(FillGenerator.GetRunChance(Drum(DrumDefinitions.AcousticSnare), Tilt.Of(4, 1))).IsEqualTo(FillGenerator.GetRunChance(Drum(DrumDefinitions.AcousticSnare), Tilt.Of(1, 1)));
        await Assert.That(FillGenerator.GetRunChance(Drum(DrumDefinitions.Kick), Tilt.Of(4, 1)))
            .IsEqualTo(Tilt.Of(4, 1).Chance(FillGenerator.GetRunChance(Drum(DrumDefinitions.Kick), Tilt.Of(1, 1)), 1)).Within(1e-9);
        await Assert.That(FillGenerator.GetRunChance(Drum(DrumDefinitions.Tom), Tilt.Of(1, 1)))
            .IsGreaterThan(FillGenerator.GetRunChance(Drum(DrumDefinitions.Cymbal), Tilt.Of(1, 1)));
    }

    [Test]
    public async Task Drummers_SpreadAroundTheMiddle_AndFavourAWalk()
    {
        var context = new GenerationContext(1);
        var drummers = Enumerable.Range(0, 5_000).Select(_ => Drummer.Generate(context, new RhythmicUnconventionality(0.5))).ToArray();
        var busyness = drummers.Select(x => x.Busyness).Order().ToArray();

        await Assert.That(busyness[busyness.Length / 2]).IsEqualTo(0.5).Within(0.03);
        await Assert.That(busyness.All(x => x is >= 0 and <= 1)).IsTrue();
        await Assert.That(drummers.Select(x => x.Favourite).Distinct().Count()).IsEqualTo(FillLayers.Paths.Length);
    }
}
