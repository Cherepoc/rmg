namespace Rmg.Core.Probabilities;

public sealed class CachedGenerator<TArg, T>
    where TArg : notnull
{
    private readonly Func<TArg, T> _innerGenerator;
    private readonly Dictionary<TArg, T> _dictionary = new();
    private readonly Func<TArg, T> _generateDelegate;
    
    public CachedGenerator(Func<TArg, T> innerGenerator)
    {
        _innerGenerator = innerGenerator;
        _generateDelegate = Generate;
    }

    public T Generate(TArg input)
    {
        if (_dictionary.TryGetValue(input, out var cachedValue))
            return cachedValue;
        
        var generatedValue = _innerGenerator(input);
        _dictionary[input] = generatedValue;
        return generatedValue;
    }

    public static implicit operator Func<TArg, T>(CachedGenerator<TArg, T> generator) =>
        generator._generateDelegate;
}

public static class CachedGeneratorExtensions
{
    public static Func<TArg, T> CacheGeneratedValues<TArg, T>(this Func<TArg, T> innerGenerator)
        where TArg : notnull
    {
        return new CachedGenerator<TArg, T>(innerGenerator);
    }
}