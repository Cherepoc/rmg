using System.Collections.Generic;

namespace RMG.Core.Generation.CollectionGenerators
{
    public sealed class DictionaryGenerator<TKey, TValue> : GeneratorBase<IDictionary<TKey, TValue>>
    {
        public IGenerator<IEnumerable<TKey>> KeyCollectionGenerator { get; set; }

        public IGenerator<TValue> ValueGenerator { get; set; }

        public override IDictionary<TKey, TValue> Generate(GenerationContext context)
        {
            var dictionary = new Dictionary<TKey, TValue>();
            var dictionaryContext = new GenerationContext(context, dictionary);
            var keys = KeyCollectionGenerator.Generate(dictionaryContext);
            foreach (var key in keys)
            {
                var value = ValueGenerator.Generate(dictionaryContext);
                dictionary.Add(key, value);
            }

            return dictionary;
        }
    }
}
