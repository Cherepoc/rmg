using System;
using System.Collections.Generic;
using RMG.Core.Generation.CollectionGenerators;

namespace RMG.Core.Generation.ContextGenerators
{
    public sealed class ContextCollectionItemIndexGenerator<T> : GeneratorBase<int>
    {
        public override int Generate(GenerationContext context)
        {
            var collectionItemContext =
                (CollectionItemGenerationContext) context.FindParent(
                    x => x is CollectionItemGenerationContext collectionItemGenerationContext
                         && collectionItemGenerationContext.Value is IEnumerable<T>);
            if (collectionItemContext == null)
            {
                throw new ApplicationException(
                    $"Failed to find collection item generation context for type {typeof(IEnumerable<T>)}");
            }

            return collectionItemContext.Index;
        }
    }
}
