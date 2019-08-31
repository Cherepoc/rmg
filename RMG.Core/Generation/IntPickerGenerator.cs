using RMG.Core.Utils;

namespace RMG.Core.Generation
{
    public sealed class IntPickerGenerator : IGenerator
    {
        public IGenerator ProbabilityFunctionGenerator { get; set; }

        public IGenerator MinValueGenerator { get; set; }

        public IGenerator MaxValueGenerator { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public int Generate(GenerationContext context)
        {
            var probabilityFunction = ProbabilityFunctionGenerator.RunGeneration<GeometricProbabilityFunction>(context);
            var minValue = MinValueGenerator.RunGeneration<int>(context);
            var maxValue = MaxValueGenerator.RunGeneration<int>(context);

            var weights = probabilityFunction.GetWeights(minValue, maxValue);
            var testProbability = context.Random.NextDouble();
            var testResult = ProbabilityTester.TestWeights(testProbability, weights);
            return minValue + testResult.ItemIndex;
        }
    }
}
