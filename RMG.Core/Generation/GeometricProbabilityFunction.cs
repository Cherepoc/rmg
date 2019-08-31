using System;
using System.Collections.Generic;

namespace RMG.Core.Generation
{
    public sealed class GeometricProbabilityFunction
    {
        public double MinProbability { get; set; } = 0;

        public double MaxProbability { get; set; } = 1;

        public double ProbabilityMultiplier { get; set; } = 0.75;

        public int Offset { get; set; } = 0;

        public double GetProbability(int value)
        {
            return Math.Pow(ProbabilityMultiplier, Math.Abs(value + Offset))
                   * (MaxProbability - MinProbability)
                   + MinProbability;
        }

        public IReadOnlyList<double> GetWeights(int minValue, int maxValue)
        {
            var weights = new double[maxValue - minValue + 1];
            for (var index = 0; index < weights.Length; index++)
            {
                weights[index] = GetProbability(index + minValue);
            }

            return weights;
        }
    }
}
