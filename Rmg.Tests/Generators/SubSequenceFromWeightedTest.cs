using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.Generators;

public sealed class SubSequenceFromWeightedTest
{
    // Thresholds: a 25%, b 50%, c 100%
    private static readonly ImmutableArray<Weighted<string>> Sequence = [new(1, "a"), new(1, "b"), new(2, "c")];

    [Test]
    public async Task Count_Zero_ResultsIn_Empty()
    {
        var result = Core.Probabilities.Generators.SubSequenceFromWeighted(Sequence, 0)(new ScriptedContext());

        await Assert.That(result.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Count_GreaterThanLength_ResultsIn_ThrownException()
    {
        await Assert.That(() => { Core.Probabilities.Generators.SubSequenceFromWeighted(Sequence, 4); })
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Empty_ResultsIn_ThrownException()
    {
        await Assert.That(() => { Core.Probabilities.Generators.SubSequenceFromWeighted<string>([], 1); })
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task PicksFirstItemByProbability_ThenRenormalizesRemaining_WhenLastItemPicked()
    {
        // 0.9 -> c. Remaining a 50%, b 50%: 0.4 -> a, then only b remains.
        var context = new ScriptedContext(0.9, 0.4, 0.9);

        var result = Core.Probabilities.Generators.SubSequenceFromWeighted(Sequence, 3)(context);

        await Assert.That(result.AsEnumerable()).IsEquivalentTo(new[] { "c", "a", "b" });
    }

    [Test]
    public async Task RenormalizesRemaining_WhenMiddleItemPicked()
    {
        // 0.4 -> b. Remaining a 1/3, c 2/3: 0.3 -> a, then only c remains.
        var context = new ScriptedContext(0.4, 0.3, 0.9);

        var result = Core.Probabilities.Generators.SubSequenceFromWeighted(Sequence, 3)(context);

        await Assert.That(result.AsEnumerable()).IsEquivalentTo(new[] { "b", "a", "c" });
    }

    [Test]
    public async Task RenormalizesRemaining_WhenMiddleItemPicked_AndSecondDrawAboveFirstRemainingThreshold()
    {
        // 0.4 -> b. Remaining a 1/3, c 2/3: 0.4 > 1/3 -> c.
        var context = new ScriptedContext(0.4, 0.4, 0.9);

        var result = Core.Probabilities.Generators.SubSequenceFromWeighted(Sequence, 3)(context);

        await Assert.That(result.AsEnumerable()).IsEquivalentTo(new[] { "b", "c", "a" });
    }

    [Test]
    public async Task RenormalizesRemaining_WhenFirstItemPicked()
    {
        // 0.1 -> a. Remaining b 1/3, c 2/3: 0.3 -> b, then only c remains.
        var context = new ScriptedContext(0.1, 0.3, 0.9);

        var result = Core.Probabilities.Generators.SubSequenceFromWeighted(Sequence, 3)(context);

        await Assert.That(result.AsEnumerable()).IsEquivalentTo(new[] { "a", "b", "c" });
    }

    [Test]
    public async Task PickedItems_AreUnique()
    {
        var context = new GenerationContext(42);

        for (var i = 0; i < 200; i++)
        {
            var result = Core.Probabilities.Generators.SubSequenceFromWeighted(Sequence, 3)(context);

            await Assert.That(result.Distinct().Count()).IsEqualTo(3);
        }
    }

    private sealed class ScriptedContext(params double[] doubles) : IGenerationContext
    {
        private int _next;

        public double GenerateDouble() => doubles[_next++];

        public int GenerateInt() => throw new NotImplementedException();

        public int GenerateInt(int min, int max) => throw new NotImplementedException();

        public bool TestProbability(double probability) => throw new NotImplementedException();

        public IGenerationContext CreateContext(int seed) => throw new NotImplementedException();
    }
}
