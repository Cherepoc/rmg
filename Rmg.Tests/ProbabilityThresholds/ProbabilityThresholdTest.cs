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
    [Arguments(0, true)]
    [Arguments(0.5, true)]
    [Arguments(0.6, false)]
    [Arguments(1, false)]
    public async Task Test_ResultsIn_ThresholdAtLeastProbability(double probability, bool expected)
    {
        var input = new ProbabilityThreshold<int>(0.5, 0);

        await Assert.That(input.Test(probability)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(-0.1)]
    [Arguments(1.1)]
    public async Task Test_ProbabilityOutOfRange_ResultsIn_ThrownException(double probability)
    {
        var input = new ProbabilityThreshold<int>(0.5, 0);

        await Assert.That(() => { input.Test(probability); }).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task CompareTo_OrdersByThreshold()
    {
        var low = new ProbabilityThreshold<string>(0.2, "z");
        var high = new ProbabilityThreshold<string>(0.8, "a");

        await Assert.That(low.CompareTo(high)).IsLessThan(0);
        await Assert.That(high.CompareTo(low)).IsGreaterThan(0);
        await Assert.That(low.CompareTo(new ProbabilityThreshold<string>(0.2, "other"))).IsEqualTo(0);
    }

    [Test]
    public async Task CompareTo_Object_OtherType_ResultsIn_ThrownException()
    {
        var input = new ProbabilityThreshold<string>(0.2, "z");

        await Assert.That(() => { input.CompareTo("not a threshold"); }).Throws<ArgumentException>();
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
    [Arguments(0.25, "a")]
    [Arguments(0.4, "b")]
    [Arguments(0.9, "c")]
    [Arguments(1, "c")]
    public async Task FindValueByProbability_ResultsIn_ValueOfMatchingThreshold(double probability, string expected)
    {
        await Assert.That(Thresholds.FindValueByProbability(probability)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(0)]
    [Arguments(-0.5)]
    [Arguments(1.5)]
    public async Task FindByProbability_InvalidProbability_ResultsIn_ThrownException(double probability)
    {
        await Assert.That(() => { Thresholds.FindIndexByProbability(probability); })
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => { Thresholds.FindValueByProbability(probability); })
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task FindByProbability_EmptyThresholds_ResultsIn_ThrownException()
    {
        ImmutableArray<ProbabilityThreshold<string>> input = [];

        await Assert.That(() => { input.FindIndexByProbability(0.5); }).Throws<ArgumentException>();
        await Assert.That(() => { input.FindValueByProbability(0.5); }).Throws<ArgumentException>();
    }

    [Test]
    public async Task FindValueByProbability_ThresholdsNotReachingProbability_ResultsIn_ThrownException()
    {
        ImmutableArray<ProbabilityThreshold<string>> input = [new(0.3, "a"), new(0.6, "b")];

        await Assert.That(() => { input.FindValueByProbability(0.9); }).Throws<ArgumentOutOfRangeException>();
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

    [Test]
    public async Task RemoveThresholdAtIndex_Empty_ResultsIn_ThrownException()
    {
        ImmutableArray<ProbabilityThreshold<string>> input = [];

        await Assert.That(() => { input.RemoveThresholdAtIndex(0); }).Throws<ArgumentException>();
    }

    [Test]
    [Arguments(-1)]
    [Arguments(3)]
    public async Task RemoveThresholdAtIndex_IndexOutOfRange_ResultsIn_ThrownException(int index)
    {
        await Assert.That(() => { Thresholds.RemoveThresholdAtIndex(index); })
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task RemoveThresholdAtIndex_SingleItem_ResultsIn_Empty()
    {
        ImmutableArray<ProbabilityThreshold<string>> input = [new(1, "a")];

        await Assert.That(input.RemoveThresholdAtIndex(0).IsEmpty).IsTrue();
    }

    [Test]
    public async Task RemoveThresholdAtIndex_Last_RenormalizesRemaining()
    {
        // a:25% b:25% c:50% -> without c: a:50% b:50%
        var result = Thresholds.RemoveThresholdAtIndex(2);

        await Assert.That(result.Select(x => x.Value)).IsEquivalentTo(new[] { "a", "b" });
        await Assert.That(result[0].Threshold).IsEqualTo(0.5).Within(Tolerance);
        await Assert.That(result[1].Threshold).IsEqualTo(1).Within(Tolerance);
    }

    [Test]
    public async Task RemoveThresholdAtIndex_Middle_RenormalizesRemaining()
    {
        // a:25% b:25% c:50% -> without b: a:1/3 c:2/3
        var result = Thresholds.RemoveThresholdAtIndex(1);

        await Assert.That(result.Select(x => x.Value)).IsEquivalentTo(new[] { "a", "c" });
        await Assert.That(result[0].Threshold).IsEqualTo(1.0 / 3).Within(Tolerance);
        await Assert.That(result[1].Threshold).IsEqualTo(1).Within(Tolerance);
    }

    [Test]
    public async Task RemoveThresholdAtIndex_First_RenormalizesRemaining()
    {
        // a:25% b:25% c:50% -> without a: b:1/3 c:2/3
        var result = Thresholds.RemoveThresholdAtIndex(0);

        await Assert.That(result.Select(x => x.Value)).IsEquivalentTo(new[] { "b", "c" });
        await Assert.That(result[0].Threshold).IsEqualTo(1.0 / 3).Within(Tolerance);
        await Assert.That(result[1].Threshold).IsEqualTo(1).Within(Tolerance);
    }
}
