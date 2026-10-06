using System.Collections.Immutable;
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
    public async Task GeneratedSongs_GatherAboutTheMiddle_EveryValuePossible_TheEndsRarely()
    {
        var context = new GenerationContext(1);
        var values = Enumerable.Range(0, 20_000).Select(_ => Unconventionality.DrawBase(context)).Order().ToArray();

        await Assert.That(values[values.Length / 2]).IsEqualTo(Unconventionality.BaseCentre).Within(0.01);
        await Assert.That(values[values.Length / 4]).IsBetween(0.33, 0.41);
        await Assert.That(values[values.Length * 3 / 4]).IsBetween(0.59, 0.67);
        // beyond a quarter of the way from either end in about one song in ten each, and now and then the plainest and the wildest
        await Assert.That(values.Count(x => x > 0.75) / (double)values.Length).IsBetween(0.05, 0.15);
        await Assert.That(values.Count(x => x < 0.25) / (double)values.Length).IsBetween(0.05, 0.15);
        await Assert.That(values[0]).IsLessThan(0.1);
        await Assert.That(values[^1]).IsGreaterThan(0.9);
    }

    [Test]
    public async Task WildSongs_PlayMoreTuplets_ThanPlainOnes()
    {
        // the same songs at a plainer and a wilder feel, given, so that the comparison does not hang on how the songs drawn
        // spread
        double Tuplets(double feel) => TestCorpus.InParallel(Enumerable.Range(0, 128), seed =>
            {
                var song = TestCorpus.Get(seed, new SongOverrides(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Feel, feel)));
                var hits = song.Song.TrackEventStateTimelineMap.TrackTimelineMap
                    .Where(x => x.Key >= DrumGroups.FirstTrackNumber)
                    .SelectMany(x => x.Value.EventTimeline.Select(e => e.Position))
                    .ToArray();
                // a tuplet's note is off every dyadic grid, where a fill's 32nds are on one
                return hits.Length == 0 ? 0 : hits.Count(x => Math.Abs(x * 16 - Math.Round(x * 16)) > 1e-6) / (double)hits.Length;
            })
            .Average();

        var (plain, wild) = (Tuplets(0.25), Tuplets(0.75));
        Console.WriteLine($"Tuplets: {plain:P1} at a feel of 0.25, {wild:P1} at 0.75");
        // the fills play the snare's feel, so a tuplet the other drums play leaves them straight
        await Assert.That(wild).IsGreaterThan(plain * 1.5);
    }
}
