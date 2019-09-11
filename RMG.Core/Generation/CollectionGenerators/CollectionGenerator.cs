using System.Collections.Generic;
using System.Linq;

namespace RMG.Core.Generation.CollectionGenerators
{
    public sealed class CollectionGenerator<T> : GeneratorBase<IEnumerable<T>>
    {
        public CollectionGenerator()
        {
        }

        public CollectionGenerator(params IGenerator<T>[] itemGenerators)
        {
            ItemGenerators = itemGenerators;
        }

        public IEnumerable<IGenerator<T>> ItemGenerators { get; set; }

        public override IEnumerable<T> Generate(GenerationContext context)
        {
            return ItemGenerators
                .Select(x => x.Generate(context))
                .ToList();
        }
    }
}
