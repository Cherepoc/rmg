namespace RMG.Core.Generation
{
    public static class GeneratorExtensions
    {
        public static object GenerateRecursive(this IGenerator generator, GenerationContext context)
        {
            object generatedObject = generator;
            while (generatedObject is IGenerator recursiveGenerator)
            {
                generatedObject = recursiveGenerator.Generate(context);
            }

            return generatedObject;
        }
    }
}
