using System.Collections.Generic;

namespace RMG.Core.Generation
{
    public abstract class ReduceOperatorGeneratorBase<T> : GeneratorBase<T>
    {
        public IGenerator<IEnumerable<T>> CollectionGenerator { get; set; }

        protected abstract T Reduce(IEnumerable<T> collection);
        
        public override T Generate(GenerationContext context)
        {
            var collection = CollectionGenerator.Generate(context);

            return Reduce(collection);
        }
    }
}
