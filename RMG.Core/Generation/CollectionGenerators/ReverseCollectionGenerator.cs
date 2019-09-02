using System.Collections.Generic;
using System.Linq;

namespace RMG.Core.Generation.CollectionGenerators
{
    public sealed class ReverseCollectionGenerator<T> : GeneratorBase<IEnumerable<T>>
    {
        public IGenerator<IEnumerable<T>> CollectionGenerator { get; set; }

        public override IEnumerable<T> Generate(GenerationContext context)
        {
            var collection = CollectionGenerator.Generate(context);
            return collection
                .Reverse()
                .ToList();
        }
    }
}
