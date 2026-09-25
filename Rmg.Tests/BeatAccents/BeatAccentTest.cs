using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.BeatAccents;

public sealed class BeatAccentTest
{
    [Test]
    [Arguments(0, 0, 1)]
    [Arguments(0, 1, 0.25)]
    [Arguments(1, 1, 1)]
    [Arguments(0, 2, 0.25)]
    [Arguments(1, 2, 0.75)]
    [Arguments(2, 2, 1)]
    [Arguments(0, 3, 0.25)]
    [Arguments(1, 3, 0.25 + 0.75 * 0.5 / 0.875)]
    [Arguments(2, 3, 0.25 + 0.75 * 0.75 / 0.875)]
    [Arguments(3, 3, 1)]
    public async Task VelocitySkew_GoesFromQuarterToOne_InHalvingSteps(int rank, int maxRank, double expected)
    {
        await Assert.That(BeatAccent.GetVelocitySkew(rank, maxRank)).IsEqualTo(expected).Within(1e-12);
    }

    [Test]
    [Arguments(0, 0, 1)]
    [Arguments(0, 1, 0.5)]
    [Arguments(1, 1, 2)]
    [Arguments(0, 2, 0.5)]
    [Arguments(1, 2, 1)]
    [Arguments(2, 2, 2)]
    [Arguments(2, 4, 1)]
    public async Task VelocitySpread_GoesFromHalfToTwo_WithRankAsPower(int rank, int maxRank, double expected)
    {
        await Assert.That(BeatAccent.GetVelocitySpread(rank, maxRank)).IsEqualTo(expected).Within(1e-12);
    }

    [Test]
    [Arguments(-1, 2)]
    [Arguments(3, 2)]
    public async Task RankOutsidePattern_ResultsIn_ThrownException(int rank, int maxRank)
    {
        await Assert.That(() => BeatAccent.GetVelocitySkew(rank, maxRank)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => BeatAccent.GetVelocitySpread(rank, maxRank)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    [Arguments(0, 0.94)]
    [Arguments(1, 0.6)]
    [Arguments(2, 0.5)]
    public async Task Velocity_IsLouderThanMiddle_MoreOftenOnStrongerBeats(int rank, double expectedLouderShare)
    {
        var generator = BeatAccent.CreateVelocityGenerator(rank, 2);
        var context = new GenerationContext(0);

        var louderShare = Enumerable.Range(0, 20000).Count(_ => generator(context) > 0) / 20000.0;

        await Assert.That(louderShare).IsEqualTo(expectedLouderShare).Within(0.015);
    }
}
