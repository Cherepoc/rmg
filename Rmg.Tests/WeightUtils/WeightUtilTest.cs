using Rmg.Core.Probabilities;

namespace Rmg.Tests.WeightUtils;

public sealed class WeightUtilTest
{
    [Test]
    public async Task CreateGeometricRankWeightFunc_AtOffset_ResultsIn_MaxWeight()
    {
        var func = WeightUtil.CreateGeometricRankWeightFunc(2, 1, 5, 0.5);

        await Assert.That(func(2)).IsEqualTo(5);
    }

    [Test]
    [Arguments(3, 3)]
    [Arguments(1, 3)]
    [Arguments(4, 2)]
    [Arguments(0, 2)]
    public async Task CreateGeometricRankWeightFunc_DecaysGeometricallyWithDistanceFromOffset(int rank, double expected)
    {
        var func = WeightUtil.CreateGeometricRankWeightFunc(2, 1, 5, 0.5);

        await Assert.That(func(rank)).IsEqualTo(expected);
    }

    [Test]
    public async Task CreateGeometricRankWeightFunc_ZeroMultiplier_ResultsIn_MinWeightAwayFromOffset()
    {
        var func = WeightUtil.CreateGeometricRankWeightFunc(0, 1, 5, 0);

        await Assert.That(func(0)).IsEqualTo(5);
        await Assert.That(func(1)).IsEqualTo(1);
        await Assert.That(func(-3)).IsEqualTo(1);
    }

    [Test]
    public async Task CreateGeometricRankWeightFunc_EqualMinAndMax_ResultsIn_ConstantWeight()
    {
        var func = WeightUtil.CreateGeometricRankWeightFunc(0, 2, 2, 0.5);

        await Assert.That(func(0)).IsEqualTo(2);
        await Assert.That(func(5)).IsEqualTo(2);
    }

    [Test]
    public async Task CreateGeometricRankWeightFunc_NegativeArguments_ResultsIn_ThrownException()
    {
        await Assert.That(() => { WeightUtil.CreateGeometricRankWeightFunc(0, -1, 1, 0.5); })
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => { WeightUtil.CreateGeometricRankWeightFunc(0, 0, -1, 0.5); })
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => { WeightUtil.CreateGeometricRankWeightFunc(0, 0, 1, -0.5); })
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task CreateGeometricRankWeightFunc_MaxBelowMin_ResultsIn_ThrownException()
    {
        await Assert.That(() => { WeightUtil.CreateGeometricRankWeightFunc(0, 5, 1, 0.5); })
            .Throws<ArgumentOutOfRangeException>();
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
            .IsEquivalentTo(new[] { new Weighted<int>(30, 3), new Weighted<int>(10, 1), new Weighted<int>(20, 2) });
    }
}
