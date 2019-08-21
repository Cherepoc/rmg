using System;
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
            var itemCountObject = ItemCountGenerator.Generate(context);
            var itemCount = Convert.ToInt32(itemCountObject);
            var result = new T[itemCount];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = (T) ItemGenerator.Generate(context);
            }

            return result;
        }
    }
}
