using System.Collections.Generic;

namespace RMG.Core.ProbabilityCalculation
{
    public static class ProbabilityFunctionExtensions
    {
        public static IReadOnlyList<double> GetWeights(
            this IIntProbabilityFunction probabilityFunction,
            int minValue,
            int maxValue
        )
        {
            var weights = new double[maxValue - minValue + 1];
            for (var index = 0; index < weights.Length; index++)
            {
                weights[index] = probabilityFunction.GetProbability(index + minValue);
            }

            return weights;
        }
    }
}
