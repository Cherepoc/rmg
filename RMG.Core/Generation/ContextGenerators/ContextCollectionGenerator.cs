using System.Collections.Generic;
using System.Linq;
using RMG.Core.Generation.CollectionGenerators;

namespace RMG.Core.Generation.ContextGenerators
{
    public sealed class ContextCollectionGenerator<T> : GeneratorBase<IEnumerable<T>>
    {
        public override IEnumerable<T> Generate(GenerationContext context)
        {
            var collectionItemContext =
                (CollectionItemGenerationContext) context.FindParent(
                    x => x is CollectionItemGenerationContext collectionItemGenerationContext
                         && collectionItemGenerationContext.Value is IEnumerable<T>);
            var collection = ((IEnumerable<T>) collectionItemContext.Value).ToList();
            return collection;
        }
    }
}
