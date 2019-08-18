namespace RMG.Core.Generation
{
    public interface IGenerator
    {
        object Generate(GenerationContext context);
    }
}
