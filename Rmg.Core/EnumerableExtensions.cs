using System.Collections.Immutable;

namespace Rmg.Core;

public static class EnumerableExtensions
{
    public static ImmutableArray<T> AsImmutableArray<T>(this IEnumerable<T> source)
    {
        return source switch
        {
            ImmutableArray<T> immutableArray => immutableArray,
            _ => [..source]
        };
    }

    public static IReadOnlyDictionary<TKey, TValue> AsReadOnlyDictionary<TKey, TValue>(
        this IEnumerable<KeyValuePair<TKey, TValue>> source
    ) where TKey : notnull
    {
        return source switch
        {
            IReadOnlyDictionary<TKey, TValue> dictionary => dictionary,
            _ => new Dictionary<TKey, TValue>(source)
        };
    }
}