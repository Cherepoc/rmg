using System;
using System.Collections.Generic;
using System.Linq;

namespace RMG.Core.Generation.ConversionGenerators
{
    public sealed class CollectionConverterGenerator
        <TCollection, TItem> : ConverterGeneratorBase<IEnumerable<TItem>, TCollection>
        where TCollection : IEnumerable<TItem>
    {
        protected override TCollection Convert(IEnumerable<TItem> value)
        {
            if (value == null)
            {
                return default;
            }

            if (value is TCollection collection)
            {
                return collection;
            }

            var targetType = typeof(TCollection);

            if (targetType == typeof(List<TItem>))
            {
                return (TCollection) (IEnumerable<TItem>) new List<TItem>(value);
            }

            if (targetType == typeof(TItem[])
                || typeof(ICollection<TItem>).IsAssignableFrom(targetType)
                || typeof(IReadOnlyCollection<TItem>).IsAssignableFrom(targetType))
            {
                return (TCollection) (IEnumerable<TItem>) value.ToArray();
            }

            throw new InvalidCastException(
                $"Collection of type {value.GetType()} cannot be converted to type {targetType}");
        }
    }
}
