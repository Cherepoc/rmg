using Rmg.Core.Probabilities;

namespace Rmg.Tests.DyadicRankDistributions;

public sealed class DyadicRankDistributionGetExactHalfDistributionSliceTest
{
    [Test]
    public async Task Rank0_ResultsIn_ZeroOffset()
    {
        var result = DyadicRankDistribution.GetExactHalfDistributionSlice(0, 0, 1);

        await Assert.That(result.Select(x => x.Position)).IsEquivalentTo(new[] { 0d });
    }

    [Test]
    public async Task Rank1_ResultsIn_HalfOffset()
    {
        var result = DyadicRankDistribution.GetExactHalfDistributionSlice(1, 0, 1);

        await Assert.That(result.Select(x => x.Position)).IsEquivalentTo(new[] { 0.5 });
    }

    [Test]
    public async Task Rank2_ResultsIn_QuarterOffsets()
    {
        var result = DyadicRankDistribution.GetExactHalfDistributionSlice(2, 0, 1);

        await Assert.That(result.Select(x => x.Position)).IsEquivalentTo(new[] { 0.25, 0.75 });
    }

    [Test]
    public async Task WideRange_NeverContainsOffsetsAtOrAboveOne()
    {
        // Half offsets are positions inside a single period, so they must stay below 1 no matter how wide the range is.
        for (var rank = 0; rank <= DyadicRankDistribution.MaxRank; rank++)
        {
            var result = DyadicRankDistribution.GetExactHalfDistributionSlice(rank, 0, 100);

            await Assert.That(result.All(x => x.Position is >= 0 and < 1)).IsTrue();
        }
    }

    [Test]
    public async Task ItemsCountIsHalfOfFullRankDistribution()
    {
        for (var rank = 1; rank <= DyadicRankDistribution.MaxRank; rank++)
        {
            var result = DyadicRankDistribution.GetExactHalfDistributionSlice(rank, 0, 100);

            await Assert.That(result.Length).IsEqualTo((int)Math.Pow(2, rank) / 2);
        }
    }
}
