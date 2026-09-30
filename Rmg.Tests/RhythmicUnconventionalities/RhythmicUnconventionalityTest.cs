using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.RhythmicUnconventionalities;

public sealed class RhythmicUnconventionalityTest
{
    [Test]
    [Arguments(0.0, 0.25)]
    [Arguments(0.5, 1.0)]
    [Arguments(1.0, 4.0)]
    public async Task ChanceScale_RunsFromAQuarterToFourTimes(double value, double expected)
    {
        await Assert.That(new RhythmicUnconventionality(value).ChanceScale).IsEqualTo(expected).Within(1e-9);
    }

    [Test]
    public async Task Lean_NeverMovesTheGrooveAtThePlainEnd_AsTunedAtTheMiddle_AndAlwaysAtTheWild()
    {
        var layer = RhythmLayers.Section;
        var (plain, middle, wild) = (new RhythmicUnconventionality(0).Lean(layer), new RhythmicUnconventionality(0.5).Lean(layer), new RhythmicUnconventionality(1).Lean(layer));

        await Assert.That((plain.Groove, plain.Speed, plain.Density)).IsEqualTo((0.0, 0.0, 0.0));
        await Assert.That((middle.Groove, middle.Speed, middle.Density)).IsEqualTo((layer.Groove, layer.Speed, layer.Density));
        await Assert.That((wild.Groove, wild.Speed, wild.Density)).IsEqualTo((1.0, 1.0, 1.0));
        // the speed moves its share of the groove's moves as tuned
        await Assert.That(middle.Speed).IsEqualTo(Math.Min(1, layer.Groove * RhythmLayer.SpeedShare)).Within(1e-9);
    }

    [Test]
    public async Task Songs_SpreadOverTheRange_AroundTheMiddle()
    {
        var context = new GenerationContext(1);
        var values = Enumerable.Range(0, 20_000).Select(_ => RhythmicUnconventionality.Generate(context).Value).Order().ToArray();

        await Assert.That(values[values.Length / 2]).IsEqualTo(0.5).Within(0.03);
        await Assert.That(values[values.Length / 4]).IsLessThan(0.35);
        await Assert.That(values[values.Length * 3 / 4]).IsGreaterThan(0.65);
        await Assert.That(values.All(x => x is >= 0 and <= 1)).IsTrue();
    }

    [Test]
    public async Task WildSongs_PlayMoreTuplets_ThanPlainOnes()
    {
        var shares = new List<(double Value, double Tuplets)>();
        // over enough songs that the quarters compared are not left to chance: over 80, the ratio swings from 1.4 to 1.7
        for (var seed = 0; seed < 256; seed++)
        {
            var value = RhythmicUnconventionality.Generate(SongGenerator.CreateStream(seed, SongStream.Rhythm)).Value;
            var hits = TestCorpus.Get(seed).Song.TrackEventStateTimelineMap.TrackTimelineMap
                .Where(x => x.Key >= DrumGroups.FirstTrackNumber)
                .SelectMany(x => x.Value.EventTimeline.Select(e => e.Position))
                .ToArray();
            if (hits.Length > 0)
                // a tuplet's note is off every dyadic grid, where a fill's 32nds are on one
                shares.Add((value, hits.Count(x => Math.Abs(x * 16 - Math.Round(x * 16)) > 1e-6) / (double)hits.Length));
        }

        var ordered = shares.OrderBy(x => x.Value).ToArray();
        var quarter = ordered.Length / 4;
        // the fills play the snare's feel, so a tuplet the other drums play leaves them straight
        await Assert.That(ordered[^quarter..].Average(x => x.Tuplets)).IsGreaterThan(ordered[..quarter].Average(x => x.Tuplets) * 1.5);
    }
}
