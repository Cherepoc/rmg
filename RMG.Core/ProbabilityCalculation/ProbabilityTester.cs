using System.Collections.Generic;
using System.Linq;

namespace RMG.Core.ProbabilityCalculation
{
    public static class ProbabilityTester
    {
        public static bool TestProbability(double testProbability, double probability)
        {
            if (probability <= 0)
            {
                return false;
            }

            if (probability >= 1)
            {
                return true;
            }

            return testProbability < probability;
        }

        public static ProbabilityTestResult TestWeights(double testProbability, IEnumerable<double> weights)
        {
            var weightList = weights.ToList();
            if (weightList.Count == 0)
            {
                return new ProbabilityTestResult(-1, 0);
            }

            double minWeight = 0;
            double weight = 1;
            var index = 0;
            for (; index < weightList.Count; index++)
            {
                weight = weightList[index];
                var maxWeight = minWeight + weight;
                if (maxWeight >= testProbability)
                {
                    break;
                }

                minWeight = maxWeight;
            }

            var randomRemainder = (testProbability - minWeight) / weight;
            return new ProbabilityTestResult(index, randomRemainder);
        }

        public static T PickItem<T>(double testProbability, IEnumerable<T> items)
        {
            var itemList = items.ToList();
            if (itemList.Count == 0)
            {
                return default;
            }

            if (itemList.Count == 1)
            {
                return itemList[0];
            }

            var index = (int) (testProbability / itemList.Count);
            return itemList[index];
        }
    }
}
