using System;

namespace RMG.Core.Generation
{
    public static class GeneratorExtensions
    {
        public static T Generate<T>(this IGenerator generator, GenerationContext context)
        {
            object generatedObject = generator;
            while (generatedObject is IGenerator recursiveGenerator)
            {
                generatedObject = recursiveGenerator.Generate(context);
            }

            if (generatedObject is T typedObject)
                return typedObject;
            
            return (T) Convert.ChangeType(generatedObject, typeof(T));
        }
    }
}
