namespace RMG.Core.Generation.Building
{
    public interface IGeneratorTransformationBuilder<out T>
    {
        IGenerator<T> Build();
    }
}
