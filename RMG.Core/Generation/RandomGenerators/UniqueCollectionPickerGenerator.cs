using System;
using System.Collections.Generic;
using System.Linq;

namespace RMG.Core.Generation.RandomGenerators
{
    public sealed class UniqueCollectionPickerGenerator<T> : GeneratorBase<IEnumerable<T>>
    {
        public IGenerator<IEnumerable<T>> CollectionGenerator { get; set; }
        public IGenerator<int> ItemCountGenerator { get; set; }

        public override IEnumerable<T> Generate(GenerationContext context)
        {
            var itemCount = ItemCountGenerator.Generate(context);
            var result = new List<T>(itemCount);

            var linkedCollection = CollectionGenerator.Generate(context).ToList();

            var maxItemCount = Math.Min(itemCount, linkedCollection.Count);
            for (var index = 0; index < maxItemCount; index++)
            {
                var linkedItemIndex = context.Random.Next(0, linkedCollection.Count);
                result.Add(linkedCollection[linkedItemIndex]);
                linkedCollection.RemoveAt(linkedItemIndex);
            }

            return result;
        }
    }
}
