using System;

namespace RMG.Core.Utils
{
    public static class RandomExtensions
    {
        public static bool TestProbability(this Random random, double probability)
        {
            if (probability <= 0)
            {
                return false;
            }

            if (probability >= 1)
            {
                return true;
            }

            return random.NextDouble() < probability;
        }
    }
}
