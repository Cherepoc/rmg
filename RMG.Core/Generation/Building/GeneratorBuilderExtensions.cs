using RMG.Core.Generation.RandomGenerators;
using RMG.Core.ProbabilityCalculation;

namespace RMG.Core.Generation.Building
{
    public static class GeneratorBuilderExtensions
    {
        public static ObjectGeneratorBuilder<T> Object<T>(this GeneratorBuilder<T> builder)
        {
            return new ObjectGeneratorBuilder<T>();
        }

        public static GeneratorTransformationBuilder<T> Value<T>(this GeneratorBuilder<T> builder, T value)
        {
            return new GeneratorTransformationBuilder<T>(() => new ConstantGenerator<T>(value));
        }

        public static GeneratorTransformationBuilder<int> RandomInt<T>(
            this GeneratorBuilder<int> builder,
            GeneratorBuilderSetup<int> minSetup,
            GeneratorBuilderSetup<int> maxSetup
        )
        {
            var (minBuilder, maxBuilder) = builder.Setup(minSetup, maxSetup);
            return new GeneratorTransformationBuilder<int>(
                () => new IntGenerator
                {
                    MinGenerator = minBuilder.Build(),
                    MaxGenerator = maxBuilder.Build()
                });
        }

        public static GeneratorTransformationBuilder<int> PickInt<T>(
            this GeneratorBuilder<int> builder,
            GeneratorBuilderSetup<IIntProbabilityFunction> probabilityFunctionSetup,
            GeneratorBuilderSetup<int> minSetup,
            GeneratorBuilderSetup<int> maxSetup
        )
        {
            var (probabilityFunctionBuilder, minBuilder, maxBuilder) = builder.Setup(
                probabilityFunctionSetup,
                minSetup,
                maxSetup);
            return new GeneratorTransformationBuilder<int>(
                () => new IntPickerGenerator
                {
                    ProbabilityFunctionGenerator = probabilityFunctionBuilder.Build(),
                    MinValueGenerator = minBuilder.Build(),
                    MaxValueGenerator = maxBuilder.Build()
                });
        }

        public static GeneratorTransformationBuilder<double> RandomDouble<T>(
            this GeneratorBuilder<double> builder,
            GeneratorBuilderSetup<double> minSetup,
            GeneratorBuilderSetup<double> maxSetup
        )
        {
            var (minBuilder, maxBuilder) = builder.Setup(minSetup, maxSetup);
            return new GeneratorTransformationBuilder<double>(
                () => new DoubleGenerator
                {
                    MinGenerator = minBuilder.Build(),
                    MaxGenerator = maxBuilder.Build()
                });
        }

        public static GeneratorTransformationBuilder<double> PickFromBinaryTree<T>(
            this GeneratorBuilder<double> builder,
            GeneratorBuilderSetup<double> minSetup,
            GeneratorBuilderSetup<double> maxSetup
        )
        {
            var (minBuilder, maxBuilder) = builder.Setup(minSetup, maxSetup);
            return new GeneratorTransformationBuilder<double>(
                () => new DoubleGenerator
                {
                    MinGenerator = minBuilder.Build(),
                    MaxGenerator = maxBuilder.Build()
                });
        }
    }
}
