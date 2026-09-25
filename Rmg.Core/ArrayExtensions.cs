using System.Collections.Immutable;

namespace Rmg.Core;

public static class ArrayExtensions
{
    public static int BinarySearchFloor<T>(this ImmutableArray<T> array, T value)
    {
        var index = array.BinarySearch(value);
        if (index < 0)
            index = ~index - 1;
        return index;
    }

    public static int BinarySearchCeiling<T>(this ImmutableArray<T> array, T value)
    {
        var index = array.BinarySearch(value);
        if (index < 0)
            index = ~index;
        return index < array.Length ? index : -1;
    }
}
