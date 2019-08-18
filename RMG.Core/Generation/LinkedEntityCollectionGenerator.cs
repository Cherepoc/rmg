using System;
using System.Collections.Generic;
using System.Linq;

namespace RMG.Core.Generation
{
    public sealed class LinkedEntityCollectionGenerator<TEntity, TProperty> : IGenerator
    {
        public IGenerator ItemCountGenerator { get; set; }

        public Func<TEntity, IEnumerable<TProperty>> LinkedCollectionAccessor { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public IReadOnlyList<TProperty> Generate(GenerationContext context)
        {
            var result = new List<TProperty>();
            var resultContext = new GenerationContext(context, result);
            var itemCount = Convert.ToInt32(ItemCountGenerator.Generate(resultContext));
            result.Capacity = itemCount;

            var entityContext = context.FindParent(x => x.Value is TEntity);
            var entity = (TEntity) entityContext.Value;
            var linkedCollection = LinkedCollectionAccessor(entity).ToList();

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
