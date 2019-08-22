using System;
using System.Collections.Generic;
using System.Linq;

namespace RMG.Core.Generation
{
    public sealed class LinkedEntityGenerator<TEntity, TProperty> : IGenerator
    {
        public Func<TEntity, IEnumerable<TProperty>> LinkedCollectionAccessor { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public TProperty Generate(GenerationContext context)
        {
            var entityContext = context.FindParent(x => x.Value is TEntity);
            var entity = (TEntity) entityContext.Value;
            var linkedCollection = LinkedCollectionAccessor(entity).ToList();

            var linkedItemIndex = context.Random.Next(0, linkedCollection.Count);
            return linkedCollection[linkedItemIndex];
        }
    }
}
