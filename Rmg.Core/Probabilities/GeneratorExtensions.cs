namespace Rmg.Core.Probabilities;

public static class GeneratorExtensions
{
    public static Func<T> WithContext<T>(this Func<IGenerationContext, T> generator, IGenerationContext context)
    {
        return () => generator(context);
    }

    public static Func<IGenerationContext, TDest> Then<TSource, TDest>(
        this Func<IGenerationContext, TSource> generator,
        Func<TSource, TDest> thenFunc
    )
    {
        return context => thenFunc(generator(context));
    }

    /// <summary>
    ///     The generator, remembering what it made for every input so that the same input gets the same result without
    ///     generating it again.
    /// </summary>
    public static Func<TArg, T> CacheGeneratedValues<TArg, T>(this Func<TArg, T> generator)
        where TArg : notnull
    {
        var cache = new Dictionary<TArg, T>();
        return input => cache.TryGetValue(input, out var value) ? value : cache[input] = generator(input);
    }

    /// <summary>The generator, taking its input through <paramref name="map" /> first.</summary>
    public static Func<TArg, T> MapInput<TArg, TMap, T>(this Func<TMap, T> generator, Func<TArg, TMap> map)
    {
        return input => generator(map(input));
    }
}
