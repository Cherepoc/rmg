using System;
using System.Collections.Generic;
using System.Linq;

namespace RMG.Core.Generation
{
    public sealed class CollectionItemGenerator<T> : IGenerator
    {
        public IGenerator CollectionGenerator { get; set; }

        public IGenerator IndexGenerator { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public T Generate(GenerationContext context)
        {
            var collection = CollectionGenerator.RunGeneration<IEnumerable<T>>(context);
            if (collection == null)
            {
                throw new ApplicationException($"Could not get collection of type {typeof(IEnumerable<T>)}");
            }

            var index = IndexGenerator.RunGeneration<int>(context);
            return collection.ToList()[index];
        }
    }
}
