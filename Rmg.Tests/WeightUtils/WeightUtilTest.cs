using Rmg.Core.Probabilities;

namespace Rmg.Tests.WeightUtils;

public sealed class WeightUtilTest
{
    [Test]
    public async Task CreateGeometricRankWeightFunc_AtOffset_ResultsIn_1()
    {
        await Assert.That(WeightUtil.CreateGeometricRankWeightFunc(2, 0.5)(2)).IsEqualTo(1);
    }

    [Test]
    [Arguments(3, 0.5)]
    [Arguments(1, 0.5)]
    [Arguments(4, 0.25)]
    [Arguments(0, 0.25)]
    public async Task CreateGeometricRankWeightFunc_DecaysGeometricallyWithDistanceFromOffset(int rank, double expected)
    {
        await Assert.That(WeightUtil.CreateGeometricRankWeightFunc(2, 0.5)(rank)).IsEqualTo(expected);
    }

    [Test]
    public async Task CreateGeometricRankWeightFunc_ZeroMultiplier_WeighsTheOffsetAlone()
    {
        var func = WeightUtil.CreateGeometricRankWeightFunc(0, 0);

        await Assert.That(func(0)).IsEqualTo(1);
        await Assert.That(func(1)).IsEqualTo(0);
        await Assert.That(func(-3)).IsEqualTo(0);
    }

    [Test]
    public async Task CreateGeometricRankWeightFunc_NegativeMultiplier_ResultsIn_ThrownException()
    {
        await Assert.That(() => { WeightUtil.CreateGeometricRankWeightFunc(0, -0.5); }).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task WeightUsing_Empty_ResultsIn_Empty()
    {
        var result = Array.Empty<int>().WeightUsing(x => x);

        await Assert.That(result.IsEmpty).IsTrue();
    }

    [Test]
    public async Task WeightUsing_PairsEachItemWithItsWeight_InOrder()
    {
        var result = new[] { 3, 1, 2 }.WeightUsing(x => x * 10.0);

        await Assert.That(result.AsEnumerable())
            .IsEquivalentTo(new[] { new Weighted<int>(30, 3), new Weighted<int>(10, 1), new Weighted<int>(20, 2) }, CollectionOrdering.Matching);
    }
}
