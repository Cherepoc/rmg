using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.StateTimelineGenerators;

public sealed class StateTimelineGeneratorTest
{
    private static readonly StateKind<int> StateKind = StateKinds.KeyOffset;

    // values from a wide range, so that consecutive steps practically never draw the same value
    private static readonly Func<IGenerationContext, int> ValueGenerator = Rmg.Core.Probabilities.Generators.Int(1, 1_000_000);

    [Test]
    [Arguments(4)]
    [Arguments(8)]
    [Arguments(2)]
    public async Task Values_ChangeOnlyAtStepStarts(double stepDuration)
    {
        var generator = StateTimelineGenerator.Create(StateKind, stepDuration, ValueGenerator);

        var result = generator.Generate(new GenerationContext(1), 16);

        await Assert.That(result.Duration).IsEqualTo(16);
        await Assert.That(result.Count).IsEqualTo((int)(16 / stepDuration));
        foreach (var item in result)
            await Assert.That(item.Position % stepDuration).IsEqualTo(0);
    }

    [Test]
    public async Task PoolOfOne_ResultsIn_OneValueAllAlong()
    {
        var generator = StateTimelineGenerator.Create(StateKind, 4, ValueGenerator, poolSize: 1);

        var result = generator.Generate(new GenerationContext(1), 16);

        await Assert.That(result.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Pool_ResultsIn_ValuesFromThePool()
    {
        var generator = StateTimelineGenerator.Create(StateKind, 1, ValueGenerator, poolSize: 2);

        var result = generator.Generate(new GenerationContext(1), 64);

        await Assert.That(result.Select(x => x.Value).Distinct().Count()).IsLessThanOrEqualTo(2);
    }

    [Test]
    public async Task SameContextSeed_ResultsIn_SameTimeline()
    {
        var generator = StateTimelineGenerator.Create(StateKind, 4, ValueGenerator, poolSize: 4);

        var result1 = generator.Generate(new GenerationContext(7), 16);
        var result2 = generator.Generate(new GenerationContext(7), 16);

        await Assert.That(result1.AsEnumerable()).IsEquivalentTo(result2.AsEnumerable());
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task NonPositiveStep_ResultsIn_ThrownException(double stepDuration)
    {
        await Assert.That(() => StateTimelineGenerator.Create(StateKind, stepDuration, ValueGenerator))
            .Throws<ArgumentOutOfRangeException>();
    }
}
