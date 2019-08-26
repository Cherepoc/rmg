using System;
using System.Collections.Generic;
using System.Linq;

namespace RMG.Core.Generation
{
    public sealed class LinkedEntityCollectionGenerator<T> : IGenerator
    {
        private ValueProvider<IEnumerable<T>> _linkedCollectionProvider;
        private IGenerator _itemCountGenerator;

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public IList<T> Generate(GenerationContext context)
        {
            var itemCount = _itemCountGenerator.Generate<int>(context);
            var result = new List<T>(itemCount);

            var linkedCollection = _linkedCollectionProvider.GetValue(context).ToList();

            var maxItemCount = Math.Min(itemCount, linkedCollection.Count);
            for (var index = 0; index < maxItemCount; index++)
            {
                var linkedItemIndex = context.Random.Next(0, linkedCollection.Count);
                result.Add(linkedCollection[linkedItemIndex]);
                linkedCollection.RemoveAt(linkedItemIndex);
            }

            return result;
        }

        public LinkedEntityCollectionGenerator<T> LinkEntityCollection(Action<ValueProviderBuilder<IEnumerable<T>>> valueProviderSetup)
        {
            var valueProviderBuilder = new ValueProviderBuilder<IEnumerable<T>>();
            valueProviderSetup(valueProviderBuilder);
            _linkedCollectionProvider = valueProviderBuilder.Build();

            return this;
        }

        public LinkedEntityCollectionGenerator<T> WithItemCountGenerator(IGenerator generator)
        {
            _itemCountGenerator = generator;

            return this;
        }
    }
}
