using Rmg.Core.Probabilities;

namespace Rmg.Tests.DyadicRankTimelines;

public sealed class DyadicRankTimelineGenerateTest
{
    [Test]
    public async Task MaxAllowedRank_ResultsIn_Timeline()
    {
        var result = DyadicRankTimeline.Generate(1, 0, 1, DyadicRankDistribution.MaxRank);

        await Assert.That(result.Count).IsEqualTo((int)Math.Pow(2, DyadicRankDistribution.MaxRank));
    }

    [Test]
    public async Task RankAboveMax_ResultsIn_ArgumentOutOfRange()
    {
        await Assert.That(() => { DyadicRankTimeline.Generate(1, 0, 1, DyadicRankDistribution.MaxRank + 1); })
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task NegativeRank_ResultsIn_ArgumentOutOfRange()
    {
        await Assert.That(() => { DyadicRankTimeline.Generate(1, 0, 1, -1); })
            .Throws<ArgumentOutOfRangeException>();
    }

    // a bar is 4 beats; a cycle of half a bar shifted by half of it is the snare's backbeat
    private const double BarDuration = 4;
    private const double HalfBarPeriod = BarDuration / 2;
    private static readonly double BackbeatPhase = DyadicRankDistribution.GetHalfOffset(1, 0) * HalfBarPeriod;

    [Test]
    public async Task HalfBarCycle_ShiftedByHalf_WithMainHitsOnly_PlaysBeats2And4()
    {
        var result = DyadicRankTimeline.Generate(BarDuration, BackbeatPhase, HalfBarPeriod, 0);

        await Assert.That(result.Select(x => x.Position)).IsEquivalentTo([1.0, 3.0]);
        await Assert.That(result.All(x => x.Value == 0)).IsTrue();
    }

    [Test]
    public async Task HalfBarCycle_ShiftedByHalf_WithWeakerHits_KeepsBeats2And4Strongest()
    {
        var result = DyadicRankTimeline.Generate(BarDuration, BackbeatPhase, HalfBarPeriod, 1);

        await Assert.That(result.Where(x => x.Value == 0).Select(x => x.Position)).IsEquivalentTo([1.0, 3.0]);
        await Assert.That(result.Where(x => x.Value == 1).Select(x => x.Position)).IsEquivalentTo([0.0, 2.0]);
    }
}
