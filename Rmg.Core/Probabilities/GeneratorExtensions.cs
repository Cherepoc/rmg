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
}
