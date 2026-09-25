using System.Collections.Immutable;

namespace Rmg.Core.Events;

internal static class OrderedTimelineItemArrayExtensions
{
    public static int GetIndexAtFloor<T>(this ImmutableArray<TimelineItem<T>> items, double position)
        where T : notnull
    {
        if (items.Length == 0)
            return -1;

        if (position < 0)
            return -1;
        if (position >= items[^1].Position)
            return items.Length - 1;

        return items.BinarySearchFloor(new TimelineItem<T>(position, default!));
    }

    public static int GetIndexAtCeiling<T>(this ImmutableArray<TimelineItem<T>> items, double position)
        where T : notnull
    {
        if (items.Length == 0)
            return -1;

        if (position < 0)
            return 0;
        if (position > items[^1].Position)
            return -1;

        return items.BinarySearchCeiling(new TimelineItem<T>(position, default!));
    }

    public static ImmutableArray<TimelineItem<T>> Trim<T>(this ImmutableArray<TimelineItem<T>> items, double duration)
        where T : notnull
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration);

        if (items.Length == 0 || duration <= 0)
            return [];

        var lastItem = items[^1];
        if (lastItem.Position < duration)
            return items;

        var index = items.GetIndexAtCeiling(duration);
        return index > 0 ? items[..index] : [];
    }

    public static ImmutableArray<TimelineItem<T>> TrimState<T>(
        this ImmutableArray<TimelineItem<T>> items,
        StateKind<T> stateKind,
        double oldDuration,
        double newDuration
    )
        where T : notnull
    {
        ArgumentOutOfRangeException.ThrowIfNegative(oldDuration);
        ArgumentOutOfRangeException.ThrowIfNegative(newDuration);

        if (items.Length == 0 || newDuration == 0)
            return [];

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (oldDuration == newDuration)
            return items;

        if (oldDuration > newDuration)
            return Trim(items, newDuration);

        var index = items.GetIndexAtCeiling(newDuration);
        if (index == 0)
            return [];

        if (index == -1)
            index = items.Length;

        var addClosingStateItem = !stateKind.CheckValueIsDefault(items[index - 1].Value);
        var newItems = new TimelineItem<T>[index + (addClosingStateItem ? 1 : 0)];
        items.CopyTo(newItems);
        if (addClosingStateItem)
            newItems[^1] = new TimelineItem<T>(oldDuration, stateKind.DefaultValue);
        return [..newItems];
    }

    public static ImmutableArray<TimelineItem<T>> Shift<T>(this ImmutableArray<TimelineItem<T>> items, double offset)
        where T : notnull
    {
        var breakOffset = -offset;
        if (items.Length == 0 || breakOffset > items[^1].Position)
            return [];

        if (offset == 0)
            return items;

        var breakIndex = 0;
        if (breakOffset > items[0].Position)
            breakIndex = items.GetIndexAtCeiling(breakOffset);

        var newItems = new TimelineItem<T>[items.Length - breakIndex];
        for (var i = 0; i < newItems.Length; i++)
            newItems[i] = items[i + breakIndex].Shift(offset);

        return [..newItems];
    }

    public static ImmutableArray<TimelineItem<T>> ShiftState<T>(
        this ImmutableArray<TimelineItem<T>> items,
        StateKind<T> stateKind,
        double offset
    )
        where T : notnull
    {
        var breakOffset = -offset;
        if (items.Length == 0)
            return [];

        var lastItem = items[^1];
        if (breakOffset > lastItem.Position && stateKind.CheckValueIsDefault(lastItem.Value))
            return [];

        if (offset == 0)
            return items;

        if (breakOffset <= items[0].Position)
            return Shift(items, offset);

        var breakIndex = items.GetIndexAtFloor(breakOffset);
        var skipFirstValue = stateKind.CheckValueIsDefault(items[breakIndex].Value);
        breakIndex += skipFirstValue ? 1 : 0;

        if (breakIndex == items.Length)
            return [];

        var newItems = new TimelineItem<T>[items.Length - breakIndex];
        for (var i = 0; i < newItems.Length; i++)
            newItems[i] = items[i + breakIndex].ShiftMinZero(offset);

        var firstItem = newItems[0];
        if (firstItem.Position < 0)
            newItems[0] = new TimelineItem<T>(0, firstItem.Value);

        return [..newItems];
    }

    public static ImmutableArray<TimelineItem<T>> Stretch<T>(this ImmutableArray<TimelineItem<T>> items, double factor)
        where T : notnull
    {
        ArgumentOutOfRangeException.ThrowIfNegative(factor);

        if (items.Length == 0 || factor == 0)
            return [];

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (factor == 1)
            return items;

        var newItems = new TimelineItem<T>[items.Length];
        for (var i = 0; i < items.Length; i++)
            newItems[i] = items[i].Stretch(factor);

        return [..newItems];
    }

    public static ImmutableArray<TimelineItem<T>> PhaseShift<T>(
        this ImmutableArray<TimelineItem<T>> items,
        double phase,
        double duration
    )
        where T : notnull
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duration);

        if (items.Length == 0)
            return [];

        if (phase == 0)
            return items;

        var offset = phase.Mod(duration);
        var breakDuration = duration - offset;
        var breakIndex = items.GetIndexAtCeiling(breakDuration);
        if (breakIndex < 0)
            return Shift(items, offset);

        var newItems = new TimelineItem<T>[items.Length];

        var beforeBreakIndexOffset = items.Length - breakIndex;
        for (var i = 0; i < breakIndex; i++)
            newItems[i + beforeBreakIndexOffset] = items[i].Shift(offset);

        var afterBreakPositionOffset = duration - offset;
        for (var i = breakIndex; i < items.Length; i++)
            newItems[i - breakIndex] = items[i].Shift(-afterBreakPositionOffset);

        return [..newItems];
    }

    public static ImmutableArray<TimelineItem<TDest>> MapValues<TSource, TDest>(
        this ImmutableArray<TimelineItem<TSource>> items,
        Func<TSource, TDest> mapFunc
    )
        where TSource : notnull
        where TDest : notnull
    {
        if (items.Length == 0)
            return [];

        var newItems = ImmutableArray.CreateBuilder<TimelineItem<TDest>>(items.Length);
        foreach (var item in items)
            newItems.Add(item.MapValue(mapFunc));

        return newItems.ToImmutable();
    }

    public static ImmutableArray<TimelineItem<TDest>> MapValues<TSource, TDest>(
        this ImmutableArray<TimelineItem<TSource>> items,
        Func<double, TSource, TDest> mapFunc
    )
        where TSource : notnull
        where TDest : notnull
    {
        if (items.Length == 0)
            return [];

        var newItems = ImmutableArray.CreateBuilder<TimelineItem<TDest>>(items.Length);
        foreach (var item in items)
            newItems.Add(item.MapValue(mapFunc));

        return newItems.ToImmutable();
    }

    public static ImmutableArray<TimelineItem<T>> FilterValues<T>(
        this ImmutableArray<TimelineItem<T>> items,
        Func<T, bool> filterFunc
    )
        where T : notnull
    {
        if (items.Length == 0)
            return [];

        var newItems = ImmutableArray.CreateBuilder<TimelineItem<T>>(items.Length);
        foreach (var item in items)
        {
            if (filterFunc(item.Value))
                newItems.Add(item);
        }

        return [..newItems];
    }
}
