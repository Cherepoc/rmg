using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.PhraseSchemes;

public sealed class PhraseSchemesTest
{
    private const int DrawCount = 20_000;

    private static Dictionary<string, int> DrawSchemes(double unconventionality)
    {
        var context = new GenerationContext(1);
        return Enumerable.Range(0, DrawCount)
            .Select(_ => Rmg.Core.Composition.PhraseSchemes.Pick(context, unconventionality, Rmg.Core.Composition.Genre.None).ToString().Replace("′", ""))
            .CountBy(x => x)
            .ToDictionary();
    }

    [Test]
    public async Task MiddleOfTheRoad_FollowsTheTable()
    {
        var counts = DrawSchemes(0.5);
        var weightSum = Rmg.Core.Composition.PhraseSchemes.All.Sum(x => x.Weight);

        foreach (var (scheme, weight) in Rmg.Core.Composition.PhraseSchemes.All)
            await Assert.That(counts.GetValueOrDefault(scheme) / (double)DrawCount).IsEqualTo(weight / weightSum).Within(0.01).Because(scheme);
    }

    [Test]
    [Arguments("AAAA", -0.5)]
    [Arguments("AABA", 0.0)]
    [Arguments("ABAB", 0.0)]
    [Arguments("ABAC", 0.5)]
    [Arguments("ABCD", 1.0)]
    public async Task ASchemesUnconventionality_IsHowManyDifferentBarsItBrings(string scheme, double unconventionality)
    {
        await Assert.That(Rmg.Core.Composition.PhraseSchemes.GetUnconventionality(scheme)).IsEqualTo(unconventionality);
    }

    [Test]
    public async Task UnconventionalRhythm_BringsMoreNewBars()
    {
        static double Adventurous(Dictionary<string, int> counts) =>
            (counts.GetValueOrDefault("ABAC") + counts.GetValueOrDefault("ABCD")) / (double)DrawCount;

        await Assert.That(Adventurous(DrawSchemes(1))).IsGreaterThan(Adventurous(DrawSchemes(0.5)) * 2);
        await Assert.That(Adventurous(DrawSchemes(0))).IsLessThan(Adventurous(DrawSchemes(0.5)) / 2);
    }

    [Test]
    public async Task OnlyRepeats_AreVaried_AboutAQuarterOfThem()
    {
        var context = new GenerationContext(1);
        int repeats = 0, varied = 0;
        for (var i = 0; i < DrawCount; i++)
        {
            var scheme = Rmg.Core.Composition.PhraseSchemes.Pick(context, 0.5, Rmg.Core.Composition.Genre.None);
            for (var bar = 0; bar < 4; bar++)
            {
                var isRepeat = scheme.Letters[..bar].Contains(scheme.Letters[bar]);
                if (!isRepeat)
                {
                    await Assert.That(scheme.IsVaried[bar]).IsFalse();
                    continue;
                }

                repeats++;
                if (scheme.IsVaried[bar])
                    varied++;
            }
        }

        await Assert.That(varied / (double)repeats).IsEqualTo(Rmg.Core.Composition.PhraseSchemes.VariedRepeatChance.At(0.5)).Within(0.02);
    }

    [Test]
    public async Task Songs_BarsFollowTheirSectionsScheme_EveryTrackTheSame_ButARiffItsOwn()
    {
        // but a riff, which plays a riff's scheme of its own, in the songs of a few seeds, given the riff
        var riffs = 0;
        var withRiffs = TestCorpus.InParallel(Enumerable.Range(0, 8), seed => TestCorpus.Get(seed, new SongOverrides(Parts: System.Collections.Immutable.ImmutableDictionary<TrackRole, bool>.Empty.Add(TrackRole.Riff, true))));
        foreach (var (song, riff) in withRiffs.SelectMany(song => song.Trace.Where(x => x.Point == TracePoints.BarPattern && x.Track == song.Track(TrackRole.Riff)).GroupBy(x => x.Section).Select(x => (song, x))))
        {
            // fitted to its section's phrase
            var played = riff.Select(x => (PhraseScheme)x.Value!).DistinctBy(x => string.Join(",", x.Letters)).Single();
            await Assert.That(RiffLayers.Schemes.Any(x => x.Value.Fitted(song.MeterOf(riff.Key).PhraseBars).Letters.SequenceEqual(played.Letters))).IsTrue();
            riffs++;
        }

        await Assert.That(riffs).IsGreaterThan(0);
        foreach (var section in TestCorpus.Get(1).Trace.Where(x => x.Point == TracePoints.BarPattern && x.Track != TestCorpus.Get(1).Track(TrackRole.Riff)).GroupBy(x => x.Section))
        {
            var phrases = section.Select(x => (PhraseScheme)x.Value!).DistinctBy(x => string.Join(",", x.Letters)).ToArray();
            await Assert.That(phrases.Length).IsEqualTo(1).Because("every track follows the section's scheme");
            var letters = phrases[0].Letters;

            foreach (var track in section.GroupBy(x => x.Track))
            {
                // a melody's answer plays the same bar patterns again, recorded at their places
                var seeds = track.GroupBy(x => x.Bar).OrderBy(x => x.Key).Select(x => x.First().StateMap.GetStateValue(CompositionStateKinds.ValueSeed)).ToArray();
                for (var a = 0; a < letters.Length; a++)
                for (var b = 0; b < letters.Length; b++)
                    await Assert.That(seeds[a] == seeds[b]).IsEqualTo(letters[a] == letters[b]).Because($"{string.Join(",", letters)}, bars {a} and {b}");
            }
        }
    }

    [Test]
    public async Task VariedRepeat_StartsAsItsFirstPlayDid()
    {
        // the same bar pattern, drawn afresh more often: its first cycle is the same
        for (var seed = 0UL; seed < 30; seed++)
        {
            var weights = WeightUtil.CreateGeometricRankWeightFunc(1, 0, 1.0, 0.5);
            var descriptor = new DyadicTimelineDescriptor(4, 1, 0, 2, 4, 2);
            var first = DyadicRankThresholdPattern.Create(new GenerationContext(0), seed, weights, descriptor, 0.3);
            var varied = DyadicRankThresholdPattern.Create(new GenerationContext(0), seed, weights, descriptor, 0.8);

            static double[] FirstBeat(DyadicRankThresholdPattern pattern) =>
                [..pattern.OutcomeRankTimeline.Where(x => x.Position < 1).Select(x => x.Position)];

            await Assert.That(FirstBeat(varied)).IsEquivalentTo(FirstBeat(first), CollectionOrdering.Matching).Because($"seed {seed}");
        }
    }
}
