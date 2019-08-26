using System.Collections.Generic;

namespace RMG.Core.Generation
{
    public sealed class CollectionGenerator<T> : IGenerator
    {
        public IGenerator ItemCountGenerator { get; set; }

        public IGenerator ItemGenerator { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public IList<T> Generate(GenerationContext context)
        {
            var itemCount = ItemCountGenerator.Generate<int>(context);
            var result = new List<T>(itemCount);
            for (var index = 0; index < itemCount; index++)
            {
                var collectionItemContext = new CollectionItemGenerationContext(context, result, index);
                result.Add(ItemGenerator.Generate<T>(collectionItemContext));
            }

            return result;
        }
    }
}
