using System;

namespace RMG.Core.Generation
{
    public sealed class CollectionItemGenerationContext : GenerationContext
    {
        public int Index { get; }

        public CollectionItemGenerationContext(GenerationContext parentContext, object value, int index)
            : base(parentContext, value)
        {
            Index = index;
        }
    }
}
