namespace RMG.Core.Generation.Building
{
    public sealed class GeneratorBuilder
    {
        public GeneratorBuilder<T> OfType<T>()
        {
            return new GeneratorBuilder<T>();
        }
    }

    public class GeneratorBuilder<T> : IGeneratorBuilder<T>
    {
    }
}
