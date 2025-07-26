namespace Rmg.Core.Events;

public readonly record struct WithDuration<T>(double Duration, T Value);

public static class WithDurationExtensions
{
    public static WithDuration<T> WithDuration<T>(this T value, double duration)
    {
        return new WithDuration<T>(duration, value);
    }
}
