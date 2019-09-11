using System.Collections.Generic;

namespace RMG.Core.Generation.CollectionGenerators
{
    public sealed class RandomCollectionGenerator<T> : GeneratorBase<IEnumerable<T>>
    {
        public IGenerator<int> ItemCountGenerator { get; set; }

        public IGenerator<T> ItemGenerator { get; set; }

        public override IEnumerable<T> Generate(GenerationContext context)
        {
            var itemCount = ItemCountGenerator.Generate(context);
            var result = new List<T>(itemCount);
            for (var index = 0; index < itemCount; index++)
            {
                var collectionItemContext = new CollectionItemGenerationContext(context, result, index);
                result.Add(ItemGenerator.Generate(collectionItemContext));
            }

            return result;
        }
    }
}
