namespace RMG.Core.Generation.CollectionGenerators
{
    public sealed class CollectionItemGenerationContext : GenerationContext
    {
        public CollectionItemGenerationContext(GenerationContext parentContext, object value, int index)
            : base(parentContext, value)
        {
            Index = index;
        }

        public int Index { get; }
    }
}
