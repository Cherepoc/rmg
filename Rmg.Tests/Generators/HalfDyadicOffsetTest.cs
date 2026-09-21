using Rmg.Core.Probabilities;

namespace Rmg.Tests.Generators;

public sealed class HalfDyadicOffsetTest
{
    [Test]
    public async Task MaxRank0_ResultsIn_ZeroOffset()
    {
        var generator = Core.Probabilities.Generators.HalfDyadicOffset(_ => 1, 0, 0, 1);

        var result = generator(new FakeGenerationContext { NextDouble = 0.5 });

        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task Rank0Drawn_ResultsIn_ZeroOffset()
    {
        // ranks 0 and 1 are equally likely: probability 0.1 falls into rank 0
        var generator = Core.Probabilities.Generators.HalfDyadicOffset(_ => 1, 1, 0, 1);

        var result = generator(new FakeGenerationContext { NextDouble = 0.1 });

        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task Rank1Drawn_ResultsIn_HalfPeriodOffset()
    {
        var generator = Core.Probabilities.Generators.HalfDyadicOffset(_ => 1, 1, 0, 1);

        var result = generator(new FakeGenerationContext { NextDouble = 0.9 });

        await Assert.That(result).IsEqualTo(0.5);
    }
}
