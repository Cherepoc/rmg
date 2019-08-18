using System;

namespace RMG.Core.Generation
{
    public sealed class RankProbabilityFunction
    {
        public double Min { get; set; } = 0;

        public double Max { get; set; } = 1;

        public double Multiplier { get; set; } = 0.75;

        public double GetProbability(int rank)
        {
            return Math.Pow(Multiplier, rank) * (Max - Min) + Min;
        }
    }
}
