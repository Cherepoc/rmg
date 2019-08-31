using System;

namespace RMG.Core.Generation
{
    public static class GeneratorExtensions
    {
        public static T RunGeneration<T>(this IGenerator generator, GenerationContext context)
        {
            return (T) RunGeneration(generator, context, typeof(T));
        }

        public static object RunGeneration(this IGenerator generator, GenerationContext context)
        {
            object generatedObject = generator;
            while (generatedObject is IGenerator recursiveGenerator)
            {
                generatedObject = recursiveGenerator.Generate(context);
            }

            return generatedObject;
        }

        public static object RunGeneration(this IGenerator generator, GenerationContext context, Type type)
        {
            var generatedObject = RunGeneration(generator, context);

            if (type.IsInstanceOfType(generatedObject))
            {
                return generatedObject;
            }

            try
            {
                return Convert.ChangeType(generatedObject, type);
            }
            catch (Exception ex)
            {
                throw new InvalidCastException(
                    $"Cannot convert generated object of type {generatedObject.GetType()} to {type}",
                    ex);
            }
        }

        public static CacheGenerator<TEntity, TValue> Cache<TEntity, TValue>(this IGenerator generator)
        {
            return new CacheGenerator<TEntity, TValue>
            {
                ValueGenerator = generator
            };
        }
    }
}
