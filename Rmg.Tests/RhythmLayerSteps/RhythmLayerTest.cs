using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.RhythmLayerSteps;

public sealed class RhythmLayerTest
{
    private const int DrawCount = 100_000;

    // every layer a drum's rhythm is drawn in; a pitched track has all but the drum group's
    private static readonly RhythmLayer[] DrumLayers =
    [
        RhythmLayers.Song,
        RhythmLayers.Section,
        RhythmLayers.Track,
        RhythmLayers.SectionTrack,
        RhythmLayers.DrumGroup,
        RhythmLayers.SectionDrumGroup,
        RhythmLayers.BarPattern
    ];

    [Test]
    [Arguments(0.0)]
    [Arguments(0.15)]
    [Arguments(0.5)]
    [Arguments(1.0)]
    public async Task Step_MovesWithTheChance_EitherWayAsLikely(double chance)
    {
        var generator = RhythmLayer.CreateStepGenerator(chance);
        var context = new GenerationContext(1);
        var steps = Enumerable.Range(0, DrawCount).Select(_ => generator(context)).ToArray();

        var up = steps.Count(x => x == 1) / (double)DrawCount;
        var down = steps.Count(x => x == -1) / (double)DrawCount;

        await Assert.That(steps.All(x => x is -1 or 0 or 1)).IsTrue();
        await Assert.That(up + down).IsEqualTo(chance).Within(0.01);
        await Assert.That(up).IsEqualTo(down).Within(0.01);
    }

    [Test]
    [Arguments(-0.1)]
    [Arguments(1.1)]
    public async Task Step_WithChanceOutOfRange_Throws(double chance)
    {
        await Assert.That(() => RhythmLayer.CreateStepGenerator(chance)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task DrumGroove_IsKeptByMostSongs()
    {
        // the chance that no layer moves a groove setting, such as the snare's backbeat
        var keptChance = DrumLayers.Aggregate(1.0, (chance, layer) => chance * (1 - layer.Groove));

        await Assert.That(keptChance).IsGreaterThan(0.75);
    }

    [Test]
    public async Task DrumTuplets_PlayInAboutAFifthOfTheBars()
    {
        var keptChance = DrumLayers.Aggregate(1.0, (chance, layer) => chance * (1 - layer.Tuplet));

        await Assert.That(1 - keptChance).IsEqualTo(0.2).Within(0.03);
    }

    [Test]
    public async Task Tuplets_ComeMostlyFromSectionsAndBars()
    {
        RhythmLayer[] songLayers = [RhythmLayers.Song, RhythmLayers.Track, RhythmLayers.DrumGroup];
        RhythmLayer[] passageLayers =
            [RhythmLayers.Section, RhythmLayers.SectionTrack, RhythmLayers.SectionDrumGroup, RhythmLayers.BarPattern];

        await Assert.That(passageLayers.Sum(x => x.Tuplet)).IsGreaterThan(3 * songLayers.Sum(x => x.Tuplet));
    }

    [Test]
    public async Task Tuplet_MovesWithItsOwnChance()
    {
        var context = new GenerationContext(1);
        var layer = RhythmLayers.Section;
        var tuplet = layer.CreateTupletGenerator();

        var tupletMoves = Enumerable.Range(0, DrawCount).Count(_ => tuplet(context) != 0) / (double)DrawCount;

        await Assert.That(tupletMoves).IsEqualTo(layer.Tuplet).Within(0.005);
    }

    [Test]
    public async Task DrumSpeed_IsKeptInAboutHalfOfTheBars()
    {
        // a drum's speed is the sum of the speed steps of all its layers; unchanged when they add up to 0
        var context = new GenerationContext(1);
        var generators = DrumLayers.Select(x => x.CreateSpeedGenerator()).ToArray();
        var keptCount = Enumerable.Range(0, DrawCount).Count(_ => generators.Sum(x => x(context)) == 0);

        await Assert.That(keptCount / (double)DrawCount).IsEqualTo(0.5).Within(0.03);
    }

    [Test]
    public async Task EveryLayer_StillMovesTheGroove()
    {
        await Assert.That(DrumLayers.All(x => x.Groove > 0)).IsTrue();
    }

    [Test]
    public async Task SongAndSection_MoveTheGrooveMost()
    {
        var innerLayers = DrumLayers.Except([RhythmLayers.Song, RhythmLayers.Section]).ToArray();

        await Assert.That(innerLayers.All(x => x.Groove < RhythmLayers.Section.Groove)).IsTrue();
        await Assert.That(RhythmLayers.Section.Groove).IsLessThan(RhythmLayers.Song.Groove);
    }

    [Test]
    public async Task BarPattern_MovesTheDensityMost()
    {
        var otherLayers = DrumLayers.Except([RhythmLayers.BarPattern]).ToArray();

        await Assert.That(otherLayers.All(x => x.Density < RhythmLayers.BarPattern.Density)).IsTrue();
        await Assert.That(RhythmLayers.BarPattern.Groove).IsLessThan(RhythmLayers.BarPattern.Density);
    }
}
