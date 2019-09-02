using System.Collections.Generic;
using System.Linq;

namespace RMG.Core.Generation.RandomGenerators
{
    public sealed class CollectionItemPickerGenerator<T> : GeneratorBase<T>
    {
        public IGenerator<IEnumerable<T>> CollectionGenerator { get; set; }

        public override T Generate(GenerationContext context)
        {
            var collection = CollectionGenerator.Generate(context).ToList();
            if (collection.Count == 0)
            {
                return default;
            }

            var index = context.Random.Next(collection.Count);
            return collection[index];
        }
    }
}
