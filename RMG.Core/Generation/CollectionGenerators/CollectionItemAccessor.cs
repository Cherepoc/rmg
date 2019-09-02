using System;
using System.Collections.Generic;
using System.Linq;

namespace RMG.Core.Generation.CollectionGenerators
{
    public sealed class CollectionItemAccessor<T> : GeneratorBase<T>
    {
        public IGenerator<IEnumerable<T>> CollectionGenerator { get; set; }

        public IGenerator<int> IndexGenerator { get; set; }

        public override T Generate(GenerationContext context)
        {
            var collection = CollectionGenerator.Generate(context);
            if (collection == null)
            {
                throw new ApplicationException($"Could not get collection of type {typeof(IEnumerable<T>)}");
            }

            var index = IndexGenerator.Generate(context);
            return collection.ToList()[index];
        }
    }
}
