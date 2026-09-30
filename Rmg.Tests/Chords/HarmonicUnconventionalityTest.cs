using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.Chords;

public sealed class HarmonicUnconventionalityTest
{
    private static int[] DrawLevels(double chords, int count = 20000)
    {
        var context = new GenerationContext(0);
        var unconventionality = new HarmonicUnconventionality(chords);
        return [..Enumerable.Range(0, count).Select(_ => unconventionality.GenerateChord(context).Shape.Unconventionality)];
    }

    [Test]
    public async Task ThePlainestChords_AreTriadsAndLevelOnesColours_ANinthNowAndThen()
    {
        var levels = DrawLevels(0);

        await Assert.That(levels.All(x => x <= 2)).IsTrue();
        await Assert.That(levels.Count(x => x == 0)).IsGreaterThan(levels.Count(x => x == 1));
        await Assert.That(levels.Count(x => x == 2) / (double)levels.Length).IsBetween(0.02, 0.1);
    }

    [Test]
    public async Task TheWildestChords_AreNoTriads_TheStrangerTheLikelier()
    {
        var levels = DrawLevels(1);

        await Assert.That(levels.All(x => x >= 2)).IsTrue();
        var counts = Enumerable.Range(2, 4).Select(level => levels.Count(x => x == level)).ToArray();
        await Assert.That(counts.Zip(counts.Skip(1)).All(x => x.Second > x.First)).IsTrue();
    }

    [Test]
    public async Task MiddlingChords_PlayEveryLevel_AsTheCorpusDid()
    {
        var levels = DrawLevels(0.5);

        foreach (var (level, weight) in ChordShapes.Levels)
            await Assert.That(levels.Count(x => x == level) / (double)levels.Length).IsEqualTo(weight.Tuned / ChordShapes.Levels.Sum(x => x.Weight.Tuned)).Within(0.01);
    }
}
