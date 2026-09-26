using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.Chords;

public sealed class HarmonicUnconventionalityTest
{
    private static int[] DrawLevels(HarmonicUnconventionality unconventionality, int count = 20000)
    {
        var context = new GenerationContext(0);
        return [..Enumerable.Range(0, count).Select(_ => unconventionality.GenerateChordUnconventionality(context))];
    }

    [Test]
    public async Task NarrowSpreadAtZero_ResultsIn_OnlyTriads()
    {
        var levels = DrawLevels(new HarmonicUnconventionality(0, 0.4, 1, 1));

        await Assert.That(levels.All(x => x == 0)).IsTrue();
    }

    [Test]
    public async Task Levels_StayInTheTable()
    {
        var levels = DrawLevels(new HarmonicUnconventionality(4, 5, 0, 0.5));

        await Assert.That(levels.All(x => x is >= 0 and <= ChordShapes.MaxUnconventionality)).IsTrue();
    }

    [Test]
    public async Task FlatPeakAndWideSpread_ResultsIn_LevelsAboutEven()
    {
        var levels = DrawLevels(new HarmonicUnconventionality(2.5, 2, 0, 1));

        // an even share would be about 17% each
        foreach (var level in Enumerable.Range(0, ChordShapes.MaxUnconventionality + 1))
            await Assert.That(levels.Count(x => x == level) / (double)levels.Length).IsBetween(0.12, 0.24).Because($"level {level}");
    }

    [Test]
    public async Task SkewBelowOne_TipsLessConventional_AndAboveOne_MoreConventional()
    {
        var moreConventional = DrawLevels(new HarmonicUnconventionality(2.5, 2, 1, 2)).Average();
        var even = DrawLevels(new HarmonicUnconventionality(2.5, 2, 1, 1)).Average();
        var lessConventional = DrawLevels(new HarmonicUnconventionality(2.5, 2, 1, 0.5)).Average();

        await Assert.That(moreConventional).IsLessThan(even);
        await Assert.That(lessConventional).IsGreaterThan(even);
    }

    [Test]
    public async Task SongAnchors_AreMostlyPlain()
    {
        var context = new GenerationContext(0);
        var anchors = Enumerable.Range(0, 20000).Select(_ => HarmonicUnconventionality.Generate(context).Anchor).ToArray();

        await Assert.That(anchors.Count(x => x < 1) / 20000.0).IsEqualTo(0.6).Within(0.03);
        await Assert.That(anchors.Count(x => x > 2.5) / 20000.0).IsEqualTo(0.13).Within(0.02);
    }

    [Test]
    public async Task SongPeaks_AreMostlyNearOne_AndSometimesFlat()
    {
        var context = new GenerationContext(0);
        var peaks = Enumerable.Range(0, 20000).Select(_ => HarmonicUnconventionality.Generate(context).Peak).ToArray();

        await Assert.That(peaks.All(x => x is >= 0 and <= 1)).IsTrue();
        await Assert.That(peaks.Count(x => x < 0.5) / 20000.0).IsEqualTo(0.13).Within(0.02);
    }
}
