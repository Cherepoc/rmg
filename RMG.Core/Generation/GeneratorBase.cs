namespace RMG.Core.Generation
{
    public abstract class GeneratorBase<T> : IGenerator<T>
    {
        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public abstract T Generate(GenerationContext context);
    }
}
