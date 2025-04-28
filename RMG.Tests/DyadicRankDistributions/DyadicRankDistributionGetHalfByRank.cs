using Rmg.Core.Probabilities;

namespace Rmg.Tests.DyadicRankDistributions;

public sealed class DyadicRankDistributionGetHalfByRank
{
    [Test]
    public async Task Rank0()
    {
        var items = DyadicRankDistribution.GetCombinedHalfDistributionByRank(0);

        var expectedItems = new DyadicRankDistribution.Item[]
        {
            new(0, 0),
        };

        await Assert.That(items).IsEquivalentTo(expectedItems);
    }
    
    [Test]
    public async Task Rank1()
    {
        var items = DyadicRankDistribution.GetCombinedHalfDistributionByRank(1);

        var expectedItems = new DyadicRankDistribution.Item[]
        {
            new(0, 0),
            new(0.5, 1),
        };

        await Assert.That(items).IsEquivalentTo(expectedItems);
    }
    
    [Test]
    public async Task Rank2()
    {
        var items = DyadicRankDistribution.GetCombinedHalfDistributionByRank(2);

        var expectedItems = new DyadicRankDistribution.Item[]
        {
            new(0, 0),
            new(0.25, 2),
            new(0.5, 1),
            new(0.75, 2),
        };

        await Assert.That(items).IsEquivalentTo(expectedItems);
    }
    
    [Test]
    public async Task Rank3()
    {
        var items = DyadicRankDistribution.GetCombinedHalfDistributionByRank(3);

        var expectedItems = new DyadicRankDistribution.Item[]
        {
            new(0, 0),
            new(0.125, 3),
            new(0.25, 2),
            new(0.375, 3),
            new(0.5, 1),
            new(0.625, 3),
            new(0.75, 2),
            new(0.875, 3),
        };

        await Assert.That(items).IsEquivalentTo(expectedItems);
    }
}