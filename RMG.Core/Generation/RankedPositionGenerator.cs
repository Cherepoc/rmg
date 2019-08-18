using System;

namespace RMG.Core.Generation
{
    public sealed class RankedPositionGenerator : IGenerator
    {
        public int MaxRank { get; set; } = 5;

        public double RankMultiplier { get; set; }

        public double Offset { get; set; }

        public double Period { get; set; }

        public double Min { get; set; }

        public double Max { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public double Generate(GenerationContext context)
        {
            var rankProbabilities = GenerateRankProbabilities();
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
            var stepMin = (int) Math.Ceiling(((Min - Offset) / Period - rankOffset) / rankPeriod);
            var stepMax = (int) Math.Floor(((Max - Offset) / Period - rankOffset) / rankPeriod);
            var stepCount = stepMax - stepMin + 1;
            var normalizedRankProbability = (probability - rankSum + rankProbability) / rankProbability;
            var stepIndex = (int) Math.Floor(normalizedRankProbability * stepCount);
            var position = (rankPeriod * (stepIndex + stepMin) + rankOffset) * Period + Offset;
            return position;
        }

        private double[] GenerateRankProbabilities()
        {
            var normalizedMin = (Min - Offset) / Period;
            var normalizedMax = (Max - Offset) / Period;
            var rankProbabilityBorders = new double[MaxRank + 1];
            var rankProbability = 1d;
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
                    rankProbabilityBorders[rank] = rankProbability;
                    rankSum += rankProbability;
                }
                else
                {
                    rankProbabilityBorders[rank] = 0;
                }

                rankProbability *= RankMultiplier;
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
