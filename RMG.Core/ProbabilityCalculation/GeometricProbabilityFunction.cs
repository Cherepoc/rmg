using System;

namespace RMG.Core.ProbabilityCalculation
{
    public sealed class GeometricProbabilityFunction : IIntProbabilityFunction
    {
        public GeometricProbabilityFunction()
        {
        }

        public GeometricProbabilityFunction(double probabilityMultiplier)
        {
            ProbabilityMultiplier = probabilityMultiplier;
        }

        public GeometricProbabilityFunction(double minProbability, double maxProbability, double probabilityMultiplier)
        {
            MinProbability = minProbability;
            MaxProbability = maxProbability;
            ProbabilityMultiplier = probabilityMultiplier;
        }

        public GeometricProbabilityFunction(
            double minProbability,
            double maxProbability,
            double probabilityMultiplier,
            int offset
        )
        {
            MinProbability = minProbability;
            MaxProbability = maxProbability;
            ProbabilityMultiplier = probabilityMultiplier;
            Offset = offset;
        }

        public double MinProbability { get; set; }

        public double MaxProbability { get; set; } = 1;

        public double ProbabilityMultiplier { get; set; } = 0.75;

        public int Offset { get; set; }

        public double GetProbability(int value)
        {
            return Math.Pow(ProbabilityMultiplier, Math.Abs(value - Offset))
                   * (MaxProbability - MinProbability)
                   + MinProbability;
        }
    }
}
