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
}
