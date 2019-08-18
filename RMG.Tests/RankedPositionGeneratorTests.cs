using System;
using RMG.Core.Generation;
using Xunit;

namespace RMG.Tests
{
    public class RankedPositionGeneratorTests
    {
        [Theory]
        [InlineData(0.5, 1, 0, 4, 0, 1)]
        [InlineData(0.5, 0.25, 0, 4, 0, 1)]
        [InlineData(0.5, 4, 0, 4, 0, 1)]
        [InlineData(0.5, 1, 0.5, 4, 0, 1)]
        [InlineData(0.5, 1, 1, 4, 0, 1)]
        [InlineData(0.5, 1, 0, 4, 0.75, 1)]
        [InlineData(0.5, 1, 0, 4, 0, 0.25)]
        [InlineData(0.5, 1, 0, 4, 1, 2.25)]
        [InlineData(0.5, 2, 0.5, 4, 1, 2.25)]
        [InlineData(0.5, 3, 0.5, 4, 1, 2.25)]
        [InlineData(0.5, 0.3, 0.2, 4, 0.1, 1.99)]
        public void ValuesInValidRange(
            double rankMultiplier,
            double scale,
            double offset,
            int maxRank,
            double min,
            double max
        )
        {
            var generator = new RankedPositionGenerator
            {
                RankMultiplier = rankMultiplier,
                Period = scale,
                Offset = offset,
                MaxRank = maxRank,
                Min = min,
                Max = max
            };
            var context = new GenerationContext(new Random(0));

            var results = new double[100];
            for (var i = 0; i < results.Length; i++)
            {
                results[i] = generator.Generate(context);
            }

            foreach (var result in results)
            {
                Assert.InRange(result, min, max);
            }
        }
    }
}
