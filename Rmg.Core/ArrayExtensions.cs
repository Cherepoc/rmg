using System.Collections.Immutable;

namespace Rmg.Core;

public static class ArrayExtensions
{
    public static int BinarySearchFloor<T>(this ImmutableArray<T> array, T value)
    {
        int index = array.BinarySearch(value);
        if (index < 0)
            index = ~index - 1;
        return index;
    }
    
    public static int BinarySearchCeiling<T>(this ImmutableArray<T> array, T value)
    {
        int index = array.BinarySearch(value);
        if (index < 0)
            index = ~index;
        return index < array.Length ? index : -1;
    }
    
    public static T GetValueAtModIndex<T>(this ImmutableArray<T> array, int index)
    {
        if (array.IsEmpty)
            throw new ArgumentException("Array is empty", nameof(array));
        return array[index.Mod(array.Length)];
    }
}