namespace RMG.Core.Generation
{
    public interface IGenerator
    {
        object Generate(GenerationContext context);
    }

    public interface IGenerator<out T> : IGenerator
    {
        new T Generate(GenerationContext context);
    }
}
