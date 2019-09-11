using System;
using RMG.Core.Generation;
using RMG.Core.Generation.RandomGenerators;
using RMG.Core.ProbabilityCalculation;
using Xunit;

namespace RMG.Tests
{
    public sealed class RankedPositionGeneratorTests
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
            double period,
            double offset,
            int maxPower,
            double minValue,
            double maxValue
        )
        {
            var generator = new BinaryTreePickerGenerator
            {
                OffsetGenerator = new ConstantGenerator<double>(offset),
                PeriodGenerator = new ConstantGenerator<double>(period),
                MaxPowerGenerator = new ConstantGenerator<int>(maxPower),
                MinValueGenerator = new ConstantGenerator<double>(minValue),
                MaxValueGenerator = new ConstantGenerator<double>(maxValue),
                ProbabilityFunctionGenerator =
                    new ConstantGenerator<IIntProbabilityFunction>(new GeometricProbabilityFunction(rankMultiplier))
            };
            var context = new GenerationContext(new Random(0));

            var results = new double[100];
            for (var i = 0; i < results.Length; i++)
            {
                results[i] = generator.Generate(context);
            }

            foreach (var result in results)
            {
                Assert.InRange(result, minValue, maxValue);
            }
        }
    }
}
