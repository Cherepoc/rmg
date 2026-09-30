using Rmg.Core.Probabilities;

namespace Rmg.Tests.Tilts;

public sealed class ByConventionTest
{
    private static readonly double[] Conventionalities = [..Enumerable.Range(0, 101).Select(x => x / 100.0)];

    [Test]
    [Arguments(1.0, 0.3, 0.0)]
    [Arguments(0.0, 0.07, 1.0)]
    [Arguments(0.2, 0.2, 0.3)]
    [Arguments(0.0, 0.5, 0.0)]
    public async Task TheCurve_PassesThroughItsThreeValues(double plain, double tuned, double wild)
    {
        var weight = new ByConvention(plain, tuned, wild);

        await Assert.That(weight.At(0)).IsEqualTo(plain).Within(1e-12);
        await Assert.That(weight.At(0.5)).IsEqualTo(tuned).Within(1e-12);
        await Assert.That(weight.At(1)).IsEqualTo(wild).Within(1e-12);
    }

    [Test]
    [Arguments(1.0, 0.3, 0.0)]
    [Arguments(0.0, 0.07, 1.0)]
    [Arguments(0.0, 0.9, 1.0)]
    [Arguments(0.0, 0.5, 0.0)]
    [Arguments(0.6, 0.02, 0.4)]
    public async Task TheCurve_OnlyRisesOrFallsBetweenTwoValues_NeverLeavingThem(double plain, double tuned, double wild)
    {
        var weight = new ByConvention(plain, tuned, wild);
        foreach (var (from, to, first, last) in new[] { (0.0, 0.5, plain, tuned), (0.5, 1.0, tuned, wild) })
        {
            var values = Conventionalities.Where(x => x >= from && x <= to).Select(weight.At).ToArray();
            var rising = last >= first;
            await Assert.That(values.Zip(values.Skip(1)).All(x => rising ? x.Second >= x.First - 1e-12 : x.Second <= x.First + 1e-12)).IsTrue();
            await Assert.That(values.All(x => x >= Math.Min(first, last) - 1e-12 && x <= Math.Max(first, last) + 1e-12)).IsTrue();
        }
    }

    [Test]
    public async Task TheCurve_StaysNearItsTunedValue_AboutTheMiddle()
    {
        var chance = new ByConvention(0, 0.07, 1);

        // a tenth of the way to an end has come the ease's power of it
        await Assert.That(chance.At(0.55) - 0.07).IsEqualTo((1 - 0.07) * Math.Pow(0.1, ByConvention.Ease)).Within(1e-12);
        await Assert.That(0.07 - chance.At(0.45)).IsEqualTo(0.07 * Math.Pow(0.1, ByConvention.Ease)).Within(1e-12);
    }

    [Test]
    public async Task AChance_OfAnUnconventionalThing_NeverHappensAtTheStart_AndAlwaysAtTheEnd()
    {
        var chance = new ByConvention(0, 0.07, 1);

        await Assert.That(chance.At(0)).IsEqualTo(0);
        await Assert.That(chance.At(0.5)).IsEqualTo(0.07).Within(1e-12);
        await Assert.That(chance.At(1)).IsEqualTo(1);
        await Assert.That(chance.At(0.25)).IsBetween(0.0, 0.07);
    }

    [Test]
    public async Task Weigh_PlaysOnlyTheOptionsAnEndAllows_AtTheirWeights()
    {
        (string, ByConvention)[] options = [("straight", new(1, 1, 0)), ("triplets", new(0.08, 0.1, 0.5)), ("fives", new(0, 0.01, 1))];

        var plain = ByConvention.Weigh(options, 0);
        var wild = ByConvention.Weigh(options, 1);
        var middle = ByConvention.Weigh(options, 0.5);

        // every end's weights as shares of the end's, as they are picked there
        await Assert.That(plain.Select(x => x.Value)).IsEquivalentTo(["straight", "triplets"]);
        await Assert.That(plain[1].Weight / plain[0].Weight).IsEqualTo(0.08).Within(1e-9);
        await Assert.That(wild.Select(x => x.Value)).IsEquivalentTo(["triplets", "fives"]);
        await Assert.That(wild[0].Weight / wild[1].Weight).IsEqualTo(0.5).Within(1e-9);
        await Assert.That(middle.Length).IsEqualTo(3);
        await Assert.That(middle.Sum(x => x.Weight)).IsEqualTo(1).Within(1e-9);
    }

    [Test]
    public async Task AChoiceThatAllowsNothingAtAnEnd_Fails()
    {
        (string, ByConvention)[] options = [("straight", new(1, 1, 0))];

        await Assert.That(() => ByConvention.Weigh(options, 1)).Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments(-0.01)]
    [Arguments(1.01)]
    public async Task AConventionalityOutsideItsRange_Fails(double conventionality)
    {
        await Assert.That(() => new ByConvention(0, 0.5, 1).At(conventionality)).Throws<ArgumentOutOfRangeException>();
    }
}
