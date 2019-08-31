using System.Collections.Generic;
using System.Linq;

namespace RMG.Core.Generation
{
    public sealed class GeneratedCollectionGenerator<T> : IGenerator
    {
        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public IEnumerable<T> Generate(GenerationContext context)
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
