using System.Collections.Generic;

namespace RMG.Core.Generation
{
    public sealed class DictionaryGenerator<TKey, TValue> : IGenerator
    {
        public IGenerator KeyCollectionGenerator { get; set; }

        public IGenerator ValueGenerator { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public IDictionary<TKey, TValue> Generate(GenerationContext context)
        {
            var dictionary = new Dictionary<TKey, TValue>();
            var dictionaryContext = new GenerationContext(context, dictionary);
            var keys = KeyCollectionGenerator.Generate<IEnumerable<TKey>>(dictionaryContext);
            foreach (var key in keys)
            {
                var value = ValueGenerator.Generate<TValue>(dictionaryContext);
                dictionary.Add(key, value);
            }

            return dictionary;
        }
    }
}
