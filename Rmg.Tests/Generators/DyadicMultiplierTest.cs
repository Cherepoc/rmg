using Rmg.Core.Probabilities;

namespace Rmg.Tests.Generators;

public sealed class DyadicMultiplierTest
{
    private static readonly Func<int, double> EvenWeight = _ => 1;

    private static double[] Draw(Func<IGenerationContext, double> generator, int count = 5000)
    {
        var context = new GenerationContext(0);
        return [..Enumerable.Range(0, count).Select(_ => generator(context))];
    }

    [Test]
    public async Task RangeWithoutAnyMultiplierOfARank_SkipsThatRank()
    {
        // rank 1 is 1/2 and 2 alone, so it has nothing between 3/4 and 3/2 and must never be chosen
        var result = Draw(Core.Probabilities.Generators.DyadicMultiplier(EvenWeight, 2, 0.75, 1.5));

        await Assert.That(result.Distinct().Order().ToArray()).IsEquivalentTo([0.75, 1, 4.0 / 3]);
    }

    [Test]
    public async Task Multipliers_AreKOver2PowRankAndTheirInverses_InRange()
    {
        var result = Draw(Core.Probabilities.Generators.DyadicMultiplier(EvenWeight, 3, 0.5, 2));

        double[] expected = [0.5, 0.625, 0.75, 0.875, 1, 8.0 / 7, 4.0 / 3, 1.6, 2];
        await Assert.That(result.Distinct().Order().ToArray()).IsEquivalentTo(expected);
    }

    [Test]
    public async Task NoMultiplierInRange_ResultsIn_ThrownException()
    {
        await Assert.That(() => Core.Probabilities.Generators.DyadicMultiplier(EvenWeight, 1, 1.1, 1.9))
            .Throws<ArgumentException>();
    }
}
