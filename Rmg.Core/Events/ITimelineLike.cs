namespace Rmg.Core.Events;

public interface ITimelineLike<T>
{
    bool IsEmpty { get; }

    double Duration { get; }

    abstract static T Empty { get; }

    T Trim(double duration);

    T Shift(double offset);

    abstract static T Merge(IEnumerable<T> timelines);
}
