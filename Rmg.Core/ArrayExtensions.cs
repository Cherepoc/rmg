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

    /// <summary>The first item at or after a value, the first of several alike; -1 where every item is before it.</summary>
    public static int BinarySearchCeiling<T>(this ImmutableArray<T> array, T value)
    {
        var index = array.BinarySearch(value);
        if (index < 0)
            index = ~index;
        else
            while (index > 0 && Comparer<T>.Default.Compare(array[index - 1], value) == 0)
                index--;
        return index < array.Length ? index : -1;
    }
}
