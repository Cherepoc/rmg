using System;

namespace RMG.Core.ProbabilityCalculation
{
    public sealed class GeometricProbabilityFunction : IIntProbabilityFunction
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
    }
}
