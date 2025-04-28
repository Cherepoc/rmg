namespace Rmg.Core.Events;

public interface ITimelineLike<T>
{
    bool IsEmpty { get; }
    
    double Duration { get; }
    
    T Trim(double duration);
    
    T Shift(double offset);
    
    static abstract T Empty { get; }
    
    static abstract T Merge(IEnumerable<T> timelines);
}
