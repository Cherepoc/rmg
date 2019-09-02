namespace RMG.Core.Generation.Building
{
    public static class GeneratorBuilderTransformationSetupExtensions
    {
        public static (
            IGeneratorTransformationBuilder<T1>,
            IGeneratorTransformationBuilder<T2>
            ) Setup<T1, T2>(
                this IGeneratorBuilder builder,
                GeneratorBuilderSetup<T1> setup1,
                GeneratorBuilderSetup<T2> setup2
            )
        {
            var builder1 = setup1(new GeneratorBuilder<T1>());
            var builder2 = setup2(new GeneratorBuilder<T2>());

            return (builder1, builder2);
        }

        public static (
            IGeneratorTransformationBuilder<T1>,
            IGeneratorTransformationBuilder<T2>,
            IGeneratorTransformationBuilder<T3>
            )
            Setup<T1, T2, T3>(
                this IGeneratorBuilder builder,
                GeneratorBuilderSetup<T1> setup1,
                GeneratorBuilderSetup<T2> setup2,
                GeneratorBuilderSetup<T3> setup3
            )
        {
            var builder1 = setup1(new GeneratorBuilder<T1>());
            var builder2 = setup2(new GeneratorBuilder<T2>());
            var builder3 = setup3(new GeneratorBuilder<T3>());

            return (builder1, builder2, builder3);
        }

        public static (
            IGeneratorTransformationBuilder<T1>,
            IGeneratorTransformationBuilder<T2>,
            IGeneratorTransformationBuilder<T3>,
            IGeneratorTransformationBuilder<T4>
            )
            Setup<T1, T2, T3, T4>(
                this IGeneratorBuilder builder,
                GeneratorBuilderSetup<T1> setup1,
                GeneratorBuilderSetup<T2> setup2,
                GeneratorBuilderSetup<T3> setup3,
                GeneratorBuilderSetup<T4> setup4
            )
        {
            var builder1 = setup1(new GeneratorBuilder<T1>());
            var builder2 = setup2(new GeneratorBuilder<T2>());
            var builder3 = setup3(new GeneratorBuilder<T3>());
            var builder4 = setup4(new GeneratorBuilder<T4>());

            return (builder1, builder2, builder3, builder4);
        }

        public static (
            IGeneratorTransformationBuilder<T1>,
            IGeneratorTransformationBuilder<T2>,
            IGeneratorTransformationBuilder<T3>,
            IGeneratorTransformationBuilder<T4>,
            IGeneratorTransformationBuilder<T5>
            )
            Setup<T1, T2, T3, T4, T5>(
                this IGeneratorBuilder builder,
                GeneratorBuilderSetup<T1> setup1,
                GeneratorBuilderSetup<T2> setup2,
                GeneratorBuilderSetup<T3> setup3,
                GeneratorBuilderSetup<T4> setup4,
                GeneratorBuilderSetup<T5> setup5
            )
        {
            var builder1 = setup1(new GeneratorBuilder<T1>());
            var builder2 = setup2(new GeneratorBuilder<T2>());
            var builder3 = setup3(new GeneratorBuilder<T3>());
            var builder4 = setup4(new GeneratorBuilder<T4>());
            var builder5 = setup5(new GeneratorBuilder<T5>());

            return (builder1, builder2, builder3, builder4, builder5);
        }

        public static (
            IGeneratorTransformationBuilder<T1>,
            IGeneratorTransformationBuilder<T2>,
            IGeneratorTransformationBuilder<T3>,
            IGeneratorTransformationBuilder<T4>,
            IGeneratorTransformationBuilder<T5>,
            IGeneratorTransformationBuilder<T6>
            )
            Setup<T1, T2, T3, T4, T5, T6>(
                this IGeneratorBuilder builder,
                GeneratorBuilderSetup<T1> setup1,
                GeneratorBuilderSetup<T2> setup2,
                GeneratorBuilderSetup<T3> setup3,
                GeneratorBuilderSetup<T4> setup4,
                GeneratorBuilderSetup<T5> setup5,
                GeneratorBuilderSetup<T6> setup6
            )
        {
            var builder1 = setup1(new GeneratorBuilder<T1>());
            var builder2 = setup2(new GeneratorBuilder<T2>());
            var builder3 = setup3(new GeneratorBuilder<T3>());
            var builder4 = setup4(new GeneratorBuilder<T4>());
            var builder5 = setup5(new GeneratorBuilder<T5>());;
            var builder6 = setup6(new GeneratorBuilder<T6>());

            return (builder1, builder2, builder3, builder4, builder5, builder6);
        }
    }
}
