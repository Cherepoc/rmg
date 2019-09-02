namespace RMG.Core.Generation
{
    public static class GenerationContextExtensions
    {
        public static (T1, T2) Generate<T1, T2>(
            this GenerationContext context,
            IGenerator<T1> generator1,
            IGenerator<T2> generator2
        )
        {
            var value1 = generator1.Generate(context);
            var value2 = generator2.Generate(context);

            return (value1, value2);
        }

        public static (T1, T2, T3) Generate<T1, T2, T3>(
            this GenerationContext context,
            IGenerator<T1> generator1,
            IGenerator<T2> generator2,
            IGenerator<T3> generator3
        )
        {
            var value1 = generator1.Generate(context);
            var value2 = generator2.Generate(context);
            var value3 = generator3.Generate(context);

            return (value1, value2, value3);
        }

        public static (T1, T2, T3, T4) Generate<T1, T2, T3, T4>(
            this GenerationContext context,
            IGenerator<T1> generator1,
            IGenerator<T2> generator2,
            IGenerator<T3> generator3,
            IGenerator<T4> generator4
        )
        {
            var value1 = generator1.Generate(context);
            var value2 = generator2.Generate(context);
            var value3 = generator3.Generate(context);
            var value4 = generator4.Generate(context);

            return (value1, value2, value3, value4);
        }

        public static (T1, T2, T3, T4, T5) Generate<T1, T2, T3, T4, T5>(
            this GenerationContext context,
            IGenerator<T1> generator1,
            IGenerator<T2> generator2,
            IGenerator<T3> generator3,
            IGenerator<T4> generator4,
            IGenerator<T5> generator5
        )
        {
            var value1 = generator1.Generate(context);
            var value2 = generator2.Generate(context);
            var value3 = generator3.Generate(context);
            var value4 = generator4.Generate(context);
            var value5 = generator5.Generate(context);

            return (value1, value2, value3, value4, value5);
        }

        public static (T1, T2, T3, T4, T5, T6) Generate<T1, T2, T3, T4, T5, T6>(
            this GenerationContext context,
            IGenerator<T1> generator1,
            IGenerator<T2> generator2,
            IGenerator<T3> generator3,
            IGenerator<T4> generator4,
            IGenerator<T5> generator5,
            IGenerator<T6> generator6
        )
        {
            var value1 = generator1.Generate(context);
            var value2 = generator2.Generate(context);
            var value3 = generator3.Generate(context);
            var value4 = generator4.Generate(context);
            var value5 = generator5.Generate(context);
            var value6 = generator6.Generate(context);

            return (value1, value2, value3, value4, value5, value6);
        }

        public static (T1, T2, T3, T4, T5, T6, T7) Generate<T1, T2, T3, T4, T5, T6, T7>(
            this GenerationContext context,
            IGenerator<T1> generator1,
            IGenerator<T2> generator2,
            IGenerator<T3> generator3,
            IGenerator<T4> generator4,
            IGenerator<T5> generator5,
            IGenerator<T6> generator6,
            IGenerator<T7> generator7
        )
        {
            var value1 = generator1.Generate(context);
            var value2 = generator2.Generate(context);
            var value3 = generator3.Generate(context);
            var value4 = generator4.Generate(context);
            var value5 = generator5.Generate(context);
            var value6 = generator6.Generate(context);
            var value7 = generator7.Generate(context);

            return (value1, value2, value3, value4, value5, value6, value7);
        }

        public static (T1, T2, T3, T4, T5, T6, T7, T8) Generate<T1, T2, T3, T4, T5, T6, T7, T8>(
            this GenerationContext context,
            IGenerator<T1> generator1,
            IGenerator<T2> generator2,
            IGenerator<T3> generator3,
            IGenerator<T4> generator4,
            IGenerator<T5> generator5,
            IGenerator<T6> generator6,
            IGenerator<T7> generator7,
            IGenerator<T8> generator8
        )
        {
            var value1 = generator1.Generate(context);
            var value2 = generator2.Generate(context);
            var value3 = generator3.Generate(context);
            var value4 = generator4.Generate(context);
            var value5 = generator5.Generate(context);
            var value6 = generator6.Generate(context);
            var value7 = generator7.Generate(context);
            var value8 = generator8.Generate(context);

            return (value1, value2, value3, value4, value5, value6, value7, value8);
        }
    }
}
