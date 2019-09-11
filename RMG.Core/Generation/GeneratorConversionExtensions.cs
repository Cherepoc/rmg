using System.Collections.Generic;
using RMG.Core.Generation.ConversionGenerators;

namespace RMG.Core.Generation
{
    public static class GeneratorConversionExtensions
    {
        public static IGenerator<List<T>> ToList<T>(this IGenerator<IEnumerable<T>> generator)
        {
            return new CollectionConverterGenerator<List<T>, T>
            {
                ValueGenerator = generator
            };
        }
        
        public static IGenerator<double> ToDouble(this IGenerator<int> generator)
        {
            return new IntToDoubleConverterGenerator
            {
                ValueGenerator = generator
            };
        }
        
        public static IGenerator<int> ToInt(this IGenerator<double> generator)
        {
            return new DoubleToIntConverterGenerator
            {
                ValueGenerator = generator
            };
        }
    }
}
