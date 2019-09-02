using RMG.Core.ProbabilityCalculation;
using RMG.Core.Utils;

namespace RMG.Core.Generation.RandomGenerators
{
    public sealed class IntPickerGenerator : GeneratorBase<int>
    {
        public IGenerator<IIntProbabilityFunction> ProbabilityFunctionGenerator { get; set; }

        public IGenerator<int> MinValueGenerator { get; set; }

        public IGenerator<int> MaxValueGenerator { get; set; }

        public override int Generate(GenerationContext context)
        {
            var probabilityFunction = ProbabilityFunctionGenerator.Generate(context);
            var minValue = MinValueGenerator.Generate(context);
            var maxValue = MaxValueGenerator.Generate(context);

            var weights = probabilityFunction.GetWeights(minValue, maxValue);
            var testProbability = context.Random.NextDouble();
            var testResult = ProbabilityTester.TestWeights(testProbability, weights);
            return minValue + testResult.ItemIndex;
        }
    }
}
