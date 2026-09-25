namespace Rmg.Core.Events;

public interface ITimelineLike<T>
{
    double Duration { get; }

    T Trim(double duration);

    T Shift(double offset);

    /// <summary>Merges the timelines; merging none results in a timeline of zero duration.</summary>
    abstract static T Merge(IEnumerable<T> timelines);
}
