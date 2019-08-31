namespace RMG.Core.Generation
{
    public interface IGenerator
    {
        object Generate(GenerationContext context);
    }

    public interface IGenerator<out T>
    {
        T Generate(GenerationContext context);
    }
}
