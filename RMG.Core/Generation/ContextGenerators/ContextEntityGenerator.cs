namespace RMG.Core.Generation.ContextGenerators
{
    public sealed class ContextEntityGenerator<T> : GeneratorBase<T>
    {
        public override T Generate(GenerationContext context)
        {
            var entityContext = context.FindParent(x => x.Value is T);
            return (T) entityContext.Value;
        }
    }
}
