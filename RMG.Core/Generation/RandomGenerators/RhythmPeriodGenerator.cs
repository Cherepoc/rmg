using System;
using System.Linq;
using RMG.Core.ProbabilityCalculation;
using RMG.Core.Utils;

namespace RMG.Core.Generation.RandomGenerators
{
    public sealed class RhythmPeriodGenerator : GeneratorBase<double>
    {
        public IGenerator<int> MaxNumberGenerator { get; set; }

        public IGenerator<GeometricProbabilityFunction> RankProbabilityFunctionGenerator { get; set; }

        public override double Generate(GenerationContext context)
        {
            var maxNumber = MaxNumberGenerator.Generate(context);
            var primeNumbers = PrimeNumbers.Get(2, maxNumber).ToList();

            var rankProbabilityFunction = RankProbabilityFunctionGenerator.Generate(context);
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
