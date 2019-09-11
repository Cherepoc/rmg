using System;
using RMG.Core.ProbabilityCalculation;

namespace RMG.Core.Generation.RandomGenerators
{
    public sealed class BinaryTreePickerGenerator : GeneratorBase<double>
    {
        public IGenerator<IIntProbabilityFunction> ProbabilityFunctionGenerator { get; set; }

        public IGenerator<double> OffsetGenerator { get; set; }

        public IGenerator<double> PeriodGenerator { get; set; }

        public IGenerator<double> MinValueGenerator { get; set; }

        public IGenerator<double> MaxValueGenerator { get; set; }

        public IGenerator<int> MaxPowerGenerator { get; set; }

        public override double Generate(GenerationContext context)
        {
            var (probabilityFunction, offset, period, min, max, maxPower) = context.Generate(
                ProbabilityFunctionGenerator,
                OffsetGenerator,
                PeriodGenerator,
                MinValueGenerator,
                MaxValueGenerator,
                MaxPowerGenerator);
            
            var normalizedMin = (min - offset) / period;
            var normalizedMax = (max - offset) / period;
            var rankProbabilities = GenerateRankProbabilities(
                normalizedMin,
                normalizedMax,
                maxPower,
                probabilityFunction);
            
            var probability = context.Random.NextDouble();
            var rankSum = 0d;
            double rankProbability = 0;
            var rank = 0;
            for (; rank < rankProbabilities.Length; rank++)
            {
                rankProbability = rankProbabilities[rank];
                rankSum += rankProbability;
                if (rankSum > probability)
                {
                    break;
                }
            }

            var rankPeriod = Math.Pow(0.5, rank);
            var rankOffset = rankPeriod / 2;
            var stepMin = (int) Math.Ceiling((normalizedMin - rankOffset) / rankPeriod);
            var stepMax = (int) Math.Floor((normalizedMax - rankOffset) / rankPeriod);
            var stepCount = stepMax - stepMin + 1;
            var normalizedRankProbability = (probability - rankSum + rankProbability) / rankProbability;
            var stepIndex = (int) Math.Floor(normalizedRankProbability * stepCount);
            var position = (rankPeriod * (stepIndex + stepMin) + rankOffset) * period + offset;
            return position;
        }

        private static double[] GenerateRankProbabilities(
            double normalizedMin,
            double normalizedMax,
            int maxPower,
            IIntProbabilityFunction probabilityFunction
        )
        {
            var rankProbabilityBorders = new double[maxPower + 1];
            var rankSum = 0d;
            var rankPeriod = 1d;
            var rankOffset = rankPeriod / 2;
            for (var rank = 0; rank < rankProbabilityBorders.Length; rank++)
            {
                // check if there are any positions that fit into min/max
                var stepMin = (int) Math.Ceiling((normalizedMin - rankOffset) / rankPeriod);
                var stepMax = (int) Math.Floor((normalizedMax - rankOffset) / rankPeriod);
                if (stepMin <= stepMax)
                {
                    var rankProbability = probabilityFunction.GetProbability(rank);
                    rankProbabilityBorders[rank] = rankProbability;
                    rankSum += rankProbability;
                }
                else
                {
                    rankProbabilityBorders[rank] = 0;
                }

                rankPeriod /= 2;
                rankOffset /= 2;
            }

            // normalize probabilities
            for (var rank = 0; rank < rankProbabilityBorders.Length; rank++)
            {
                rankProbabilityBorders[rank] = rankProbabilityBorders[rank] / rankSum;
            }

            return rankProbabilityBorders;
        }
    }
}
