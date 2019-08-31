using System.Collections.Generic;
using System.Linq;

namespace RMG.Core.Generation
{
    public sealed class CollectionItemPickerGenerator<T> : IGenerator
    {
        public IGenerator CollectionGenerator { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public T Generate(GenerationContext context)
        {
            var collection = CollectionGenerator.RunGeneration<IEnumerable<T>>(context).ToList();
            if (collection.Count == 0)
            {
                return default;
            }

            var index = context.Random.Next(collection.Count);
            return collection[index];
        }
    }
}
