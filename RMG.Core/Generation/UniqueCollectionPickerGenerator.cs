using System;
using System.Collections.Generic;
using System.Linq;

namespace RMG.Core.Generation
{
    public sealed class UniqueCollectionPickerGenerator<T> : IGenerator
    {
        public IGenerator CollectionGenerator { get; set; }
        public IGenerator ItemCountGenerator { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public IList<T> Generate(GenerationContext context)
        {
            var itemCount = ItemCountGenerator.RunGeneration<int>(context);
            var result = new List<T>(itemCount);

            var linkedCollection = CollectionGenerator.RunGeneration<IList<T>>(context).ToList();

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
