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
}
