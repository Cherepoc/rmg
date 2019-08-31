using System;
using System.Linq;
using RMG.Core.Utils;

namespace RMG.Core.Generation
{
    public sealed class RhythmPeriodGenerator : IGenerator
    {
        public IGenerator MaxNumberGenerator { get; set; }

        public IGenerator RankProbabilityFunctionGenerator { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public double Generate(GenerationContext context)
        {
            var maxNumber = MaxNumberGenerator.RunGeneration<int>(context);
            var primeNumbers = PrimeNumbers.Get(2, maxNumber).ToList();

            var rankProbabilityFunction =
                RankProbabilityFunctionGenerator.RunGeneration<GeometricProbabilityFunction>(context);
            var rankWeights = rankProbabilityFunction.GetWeights(0, primeNumbers.Count - 1);
            var testProbability = context.Random.NextDouble();
            var rankTestResult = ProbabilityTester.TestWeights(testProbability, rankWeights);

            var primeNumber = primeNumbers[rankTestResult.ItemIndex];
            if (primeNumber == 2)
            {
                return 1;
            }

            var nearestPower = Math.Round(Math.Log(primeNumber, 2));
            var nearestDivider = Math.Pow(2, nearestPower);
            var signatures = new[]
            {
                primeNumber / nearestDivider,
                nearestDivider / primeNumber
            };
            var signature = ProbabilityTester.PickItem(rankTestResult.TestProbabilityRemainder, signatures);
            return signature;
        }
    }
}
