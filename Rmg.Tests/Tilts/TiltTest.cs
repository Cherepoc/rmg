using Rmg.Core.Probabilities;

namespace Rmg.Tests.Tilts;

public sealed class TiltTest
{
    private const int DrawCount = 100_000;

    [Test]
    public async Task NoTilt_DrawsWhatTheUntiltedDrawsDo()
    {
        var splines = (Tilt.None.SplineValue(), Core.Probabilities.Generators.SplineValue());
        var steps = (Tilt.None.Step(0.3), RhythmLayerStep(0.3));
        var (a, b) = (new GenerationContext(7), new GenerationContext(7));
        for (var i = 0; i < 1_000; i++)
        {
            await Assert.That(splines.Item1(a)).IsEqualTo(splines.Item2(b));
            await Assert.That(steps.Item1(a)).IsEqualTo(steps.Item2(b));
        }

        await Assert.That(Tilt.None.Weigh(0.3, 1)).IsEqualTo(0.3);
        await Assert.That(Tilt.None.Chance(0.3, -1)).IsEqualTo(0.3);
    }

    // the step the rhythm layers drew before tilts: whether it moves, then either way as likely
    private static Func<IGenerationContext, int> RhythmLayerStep(double chance) =>
        context => !context.TestProbability(chance) ? 0 : context.GenerateInt(0, 2) == 0 ? -1 : 1;

    [Test]
    [Arguments(4.0)]
    [Arguments(0.25)]
    public async Task SplineValue_IsPositive_WithTheHighChance(double odds)
    {
        var tilt = Tilt.Of(odds, 1);
        var generator = tilt.SplineValue();
        var context = new GenerationContext(1);

        var positive = Enumerable.Range(0, DrawCount).Count(_ => generator(context) > 0) / (double)DrawCount;

        await Assert.That(tilt.HighChance).IsEqualTo(odds / (1 + odds)).Within(1e-9);
        await Assert.That(positive).IsEqualTo(tilt.HighChance).Within(0.01);
    }

    [Test]
    public async Task Step_IsLikelier_AndLikelierToGoTheTiltsWay()
    {
        var generator = Tilt.Of(4, 1).Step(0.2);
        var context = new GenerationContext(1);

        var steps = Enumerable.Range(0, DrawCount).Select(_ => generator(context)).ToArray();
        var up = steps.Count(x => x == 1) / (double)DrawCount;
        var down = steps.Count(x => x == -1) / (double)DrawCount;

        // the choice of -1, 0 and 1 weighs 0.1 / 4, 0.8 and 0.1 * 4
        var total = 0.1 / 4 + 0.8 + 0.1 * 4;
        await Assert.That(up).IsEqualTo(0.4 / total).Within(0.01);
        await Assert.That(down).IsEqualTo(0.025 / total).Within(0.01);
    }

    [Test]
    public async Task Weigh_MultipliesByTheOddsToThePowerOfTheLean()
    {
        var tilt = Tilt.Of(4, 1);
        Weighted<int>[] options = [new(1, -1), new(1, 0), new(2, 1)];

        var weighed = tilt.Weigh(options, x => x);

        await Assert.That(weighed.Select(x => x.Weight).ToArray()).IsEquivalentTo([0.25, 1.0, 8.0]);
    }

    [Test]
    public async Task Chance_MultipliesItsOdds_AndStaysAChance()
    {
        var tilt = Tilt.Of(3, 1);

        await Assert.That(tilt.Chance(0.5, 1)).IsEqualTo(0.75).Within(1e-9);
        await Assert.That(tilt.Chance(0.5, -1)).IsEqualTo(0.25).Within(1e-9);
        await Assert.That(tilt.Chance(0.97, 1)).IsLessThan(1);
        await Assert.That(tilt.Chance(1, -1)).IsEqualTo(1);
        await Assert.That(tilt.Chance(0, 1)).IsEqualTo(0);
    }
}
