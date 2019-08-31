using System;
using System.Collections.Generic;

namespace RMG.Core.Generation
{
    public sealed class GeneratedCollectionItemIndexGenerator<T> : IGenerator
    {
        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public int Generate(GenerationContext context)
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
