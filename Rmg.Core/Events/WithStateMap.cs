namespace Rmg.Core.Events;

public readonly record struct WithStateMap<T>(T Value, StateMap StateMap)
    where T : notnull;

public static class WithStateMapExtensions
{
    public static WithStateMap<T> WithStateMap<T>(this T value, StateMap stateMap)
        where T : notnull
    {
        return new WithStateMap<T>(value, stateMap);
    }
}