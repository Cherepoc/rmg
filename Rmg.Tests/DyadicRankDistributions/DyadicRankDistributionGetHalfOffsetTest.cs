using Rmg.Core.Probabilities;

namespace Rmg.Tests.DyadicRankDistributions;

public sealed class DyadicRankDistributionGetHalfOffsetTest
{
    [Test]
    [Arguments(-1)]
    [Arguments(0)]
    [Arguments(1)]
    public async Task Rank0_IsZero(double rankedOffset)
    {
        await Assert.That(DyadicRankDistribution.GetHalfOffset(0, rankedOffset)).IsEqualTo(0);
    }

    [Test]
    [Arguments(-1)]
    [Arguments(0)]
    [Arguments(1)]
    public async Task Rank1_IsTheHalf(double rankedOffset)
    {
        await Assert.That(DyadicRankDistribution.GetHalfOffset(1, rankedOffset)).IsEqualTo(0.5);
    }

    [Test]
    [Arguments(-1, 0.25)]
    [Arguments(-0.01, 0.25)]
    [Arguments(0, 0.75)]
    [Arguments(1, 0.75)]
    public async Task Rank2_SplitsTheRangeInHalves(double rankedOffset, double expected)
    {
        await Assert.That(DyadicRankDistribution.GetHalfOffset(2, rankedOffset)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(-1, 0.125)]
    [Arguments(-0.6, 0.125)]
    [Arguments(-0.4, 0.375)]
    [Arguments(-0.1, 0.375)]
    [Arguments(0.1, 0.625)]
    [Arguments(0.4, 0.625)]
    [Arguments(0.6, 0.875)]
    [Arguments(1, 0.875)]
    public async Task Rank3_SplitsTheRangeInQuarters(double rankedOffset, double expected)
    {
        await Assert.That(DyadicRankDistribution.GetHalfOffset(3, rankedOffset)).IsEqualTo(expected);
    }

    [Test]
    public async Task EveryItem_IsReached()
    {
        const int rank = 4;
        var offsets = Enumerable.Range(0, 1001)
            .Select(i => DyadicRankDistribution.GetHalfOffset(rank, -1 + i * 0.002))
            .Distinct()
            .Count();

        await Assert.That(offsets).IsEqualTo(8);
    }
}
