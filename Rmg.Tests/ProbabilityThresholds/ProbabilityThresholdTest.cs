using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.ProbabilityThresholds;

public sealed class ProbabilityThresholdTest
{
    private const double Tolerance = 1e-9;

    // 25% -> "a", 25% -> "b", 50% -> "c"
    private static readonly ImmutableArray<ProbabilityThreshold<string>> Thresholds =
    [
        new(0.25, "a"),
        new(0.5, "b"),
        new(1, "c"),
    ];

    [Test]
    [Arguments(0)]
    [Arguments(-0.1)]
    [Arguments(1.1)]
    public async Task Constructor_InvalidThreshold_ResultsIn_ThrownException(double threshold)
    {
        await Assert.That(() => { _ = new ProbabilityThreshold<int>(threshold, 0); })
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    [Arguments(0.0001)]
    [Arguments(1)]
    public async Task Constructor_ValidThreshold_KeepsValues(double threshold)
    {
        var result = new ProbabilityThreshold<int>(threshold, 7);

        await Assert.That(result.Threshold).IsEqualTo(threshold);
        await Assert.That(result.Value).IsEqualTo(7);
    }

    [Test]
    [Arguments(0.0001, 0)]
    [Arguments(0.25, 0)]
    [Arguments(0.26, 1)]
    [Arguments(0.5, 1)]
    [Arguments(0.51, 2)]
    [Arguments(1, 2)]
    public async Task FindIndexByProbability_ResultsIn_FirstThresholdNotBelowProbability(double probability, int expected)
    {
        await Assert.That(Thresholds.FindIndexByProbability(probability)).IsEqualTo(expected);
    }

    [Test]
    public async Task FindIndexByProbability_ZeroProbability_ResultsIn_FirstThreshold()
    {
        // a random double can be exactly 0
        await Assert.That(Thresholds.FindIndexByProbability(0)).IsEqualTo(0);
    }

    [Test]
    [Arguments(-0.5)]
    [Arguments(1.5)]
    public async Task FindIndexByProbability_InvalidProbability_ResultsIn_ThrownException(double probability)
    {
        await Assert.That(() => { Thresholds.FindIndexByProbability(probability); })
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task FindIndexByProbability_EmptyThresholds_ResultsIn_ThrownException()
    {
        ImmutableArray<ProbabilityThreshold<string>> input = [];

        await Assert.That(() => { input.FindIndexByProbability(0.5); }).Throws<ArgumentException>();
    }

    [Test]
    public async Task ToProbabilityThresholds_Empty_ResultsIn_Empty()
    {
        ImmutableArray<Weighted<string>> input = [];

        await Assert.That(input.ToProbabilityThresholds().IsEmpty).IsTrue();
    }

    [Test]
    public async Task ToProbabilityThresholds_ResultsIn_CumulativeNormalizedThresholds()
    {
        ImmutableArray<Weighted<string>> input = [new(1, "a"), new(1, "b"), new(2, "c")];

        var result = input.ToProbabilityThresholds();

        await Assert.That(result.Select(x => x.Threshold)).IsEquivalentTo(new[] { 0.25, 0.5, 1 });
        await Assert.That(result.Select(x => x.Value)).IsEquivalentTo(new[] { "a", "b", "c" });
    }

    [Test]
    public async Task ToProbabilityThresholds_SingleItem_ResultsIn_FullThreshold()
    {
        ImmutableArray<Weighted<string>> input = [new(5, "a")];

        var result = input.ToProbabilityThresholds();

        await Assert.That(result.Length).IsEqualTo(1);
        await Assert.That(result[0].Threshold).IsEqualTo(1);
    }

    [Test]
    public async Task ToProbabilityThresholds_LastThresholdIsExactlyOne_DespiteRounding()
    {
        ImmutableArray<Weighted<int>> input = [new(1, 0), new(1, 1), new(1, 2)];

        var result = input.ToProbabilityThresholds();

        await Assert.That(result[^1].Threshold).IsEqualTo(1);
    }
}
