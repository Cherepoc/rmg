using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.Chords;

public sealed class ChordWeirdnessTest
{
    private static int[] DrawRanks(ChordWeirdness weirdness, int count = 20000)
    {
        var context = new GenerationContext(0);
        return [..Enumerable.Range(0, count).Select(_ => weirdness.GenerateRank(context))];
    }

    [Test]
    public async Task NarrowSpreadAtZero_ResultsIn_OnlyTriads()
    {
        var ranks = DrawRanks(new ChordWeirdness(0, 0.4, 1, 1));

        await Assert.That(ranks.All(x => x == 0)).IsTrue();
    }

    [Test]
    public async Task Ranks_StayInTheTable()
    {
        var ranks = DrawRanks(new ChordWeirdness(4, 5, 0, 0.5));

        await Assert.That(ranks.All(x => x is >= 0 and <= ChordShapes.MaxRank)).IsTrue();
    }

    [Test]
    public async Task FlatPeakAndWideSpread_ResultsIn_RanksAboutEven()
    {
        var ranks = DrawRanks(new ChordWeirdness(2.5, 2, 0, 1));

        // an even share would be about 17% each
        foreach (var rank in Enumerable.Range(0, ChordShapes.MaxRank + 1))
            await Assert.That(ranks.Count(x => x == rank) / (double)ranks.Length).IsBetween(0.12, 0.24).Because($"rank {rank}");
    }

    [Test]
    public async Task SkewBelowOne_TipsWeirder_AndAboveOne_Plainer()
    {
        var plainer = DrawRanks(new ChordWeirdness(2.5, 2, 1, 2)).Average();
        var even = DrawRanks(new ChordWeirdness(2.5, 2, 1, 1)).Average();
        var weirder = DrawRanks(new ChordWeirdness(2.5, 2, 1, 0.5)).Average();

        await Assert.That(plainer).IsLessThan(even);
        await Assert.That(weirder).IsGreaterThan(even);
    }

    [Test]
    public async Task SongAnchors_AreMostlyPlain()
    {
        var context = new GenerationContext(0);
        var anchors = Enumerable.Range(0, 20000).Select(_ => ChordWeirdness.Generate(context).Anchor).ToArray();

        await Assert.That(anchors.Count(x => x < 1) / 20000.0).IsEqualTo(0.6).Within(0.03);
        await Assert.That(anchors.Count(x => x > 2.5) / 20000.0).IsEqualTo(0.13).Within(0.02);
    }

    [Test]
    public async Task SongPeaks_AreMostlyNearOne_AndSometimesFlat()
    {
        var context = new GenerationContext(0);
        var peaks = Enumerable.Range(0, 20000).Select(_ => ChordWeirdness.Generate(context).Peak).ToArray();

        await Assert.That(peaks.All(x => x is >= 0 and <= 1)).IsTrue();
        await Assert.That(peaks.Count(x => x < 0.5) / 20000.0).IsEqualTo(0.13).Within(0.02);
    }
}
