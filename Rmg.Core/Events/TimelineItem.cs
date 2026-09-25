using System.Diagnostics;

namespace Rmg.Core.Events;

[DebuggerDisplay("[{Position},{Value}]")]
public readonly struct TimelineItem<T>
    : IComparable<TimelineItem<T>>, IComparable
    where T : notnull
{
    public TimelineItem(double position, T value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);

        Position = position;
        Value = value;
    }

    public double Position { get; }

    public T Value { get; }

    public TimelineItem<T> Shift(double offset)
    {
        var newPosition = Position + offset;
        if (newPosition < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Cannot shift to negative position");

        return new TimelineItem<T>(newPosition, Value);
    }

    public TimelineItem<T> ShiftMinZero(double offset)
    {
        return new TimelineItem<T>(Math.Max(Position + offset, 0), Value);
    }

    public TimelineItem<T> Stretch(double factor)
    {
        return new TimelineItem<T>(Position * factor, Value);
    }

    public TimelineItem<TDest> MapValue<TDest>(Func<T, TDest> map)
        where TDest : notnull
    {
        return new TimelineItem<TDest>(Position, map(Value));
    }

    public TimelineItem<TDest> MapValue<TDest>(Func<double, T, TDest> map)
        where TDest : notnull
    {
        return new TimelineItem<TDest>(Position, map(Position, Value));
    }

    public int CompareTo(TimelineItem<T> other)
    {
        return Position.CompareTo(other.Position);
    }

    public int CompareTo(object? obj)
    {
        if (obj is TimelineItem<T> item)
            return CompareTo(item);

        throw new ArgumentException($"Object must be of type {nameof(TimelineItem<T>)}", nameof(obj));
    }
}

public static class TimelineItem
{
    public static TimelineItem<T> ToTimelineItem<T>(this T value, double position)
        where T : notnull
    {
        return new TimelineItem<T>(position, value);
    }
}
