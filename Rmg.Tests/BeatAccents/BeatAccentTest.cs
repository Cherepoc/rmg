using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.BeatAccents;

public sealed class BeatAccentTest
{
    [Test]
    [Arguments(0, 0, 0)]
    [Arguments(0, 1, BeatAccent.StrongestAccent)]
    [Arguments(1, 1, 0)]
    [Arguments(0, 2, BeatAccent.StrongestAccent)]
    [Arguments(1, 2, BeatAccent.StrongestAccent / 9)]
    [Arguments(2, 2, 0)]
    [Arguments(3, 3, 0)]
    public async Task Accent_FallsFromTheStrongestBeat_ToNoneOnTheWeakest(int rank, int maxRank, double expected)
    {
        await Assert.That(BeatAccent.GetAccent(rank, maxRank)).IsEqualTo(expected).Within(1e-12);
    }

    [Test]
    [Arguments(-1, 2)]
    [Arguments(3, 2)]
    public async Task RankOutsidePattern_ResultsIn_ThrownException(int rank, int maxRank)
    {
        await Assert.That(() => BeatAccent.GetAccent(rank, maxRank)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    [Arguments(1.0)]
    [Arguments(0.4)]
    public async Task Velocity_IsTheAccent_AndAVariationAroundIt_AsFarAsTheDynamicsHaveThem(double dynamics)
    {
        var context = new GenerationContext(0);
        var strongest = Enumerable.Range(0, 20000).Select(_ => BeatAccent.CreateVelocityGenerator(0, 2, dynamics)(context)).ToArray();
        var weakest = Enumerable.Range(0, 20000).Select(_ => BeatAccent.CreateVelocityGenerator(2, 2, dynamics)(context)).ToArray();

        await Assert.That(strongest.Average()).IsEqualTo(BeatAccent.StrongestAccent * dynamics).Within(0.01);
        await Assert.That(weakest.Average()).IsEqualTo(0).Within(0.01);
        await Assert.That(Math.Sqrt(weakest.Average(x => x * x))).IsEqualTo(0.31 * dynamics).Within(0.01);
    }
}
