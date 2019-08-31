using System.Collections.Generic;
using System.Linq;

namespace RMG.Core.Generation
{
    public sealed class ReverseCollectionGenerator<T> : IGenerator
    {
        public IGenerator CollectionGenerator { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public IList<T> Generate(GenerationContext context)
        {
            var collection = CollectionGenerator.RunGeneration<IEnumerable<T>>(context);
            return collection
                .Reverse()
                .ToList();
        }
    }
}
