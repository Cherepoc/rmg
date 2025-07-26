namespace Rmg.Core.Probabilities;

public sealed class InputMapGenerator<TArg, TMap, T>
{
    private readonly Func<TArg, T> _generateDelegate;
    private readonly Func<TMap, T> _innerGenerator;
    private readonly Func<TArg, TMap> _mapFunc;

    public InputMapGenerator(Func<TMap, T> innerGenerator, Func<TArg, TMap> mapFunc)
    {
        _innerGenerator = innerGenerator;
        _mapFunc = mapFunc;
        _generateDelegate = Generate;
    }

    public T Generate(TArg input)
    {
        var mappedInput = _mapFunc(input);
        return _innerGenerator(mappedInput);
    }

    public static implicit operator Func<TArg, T>(InputMapGenerator<TArg, TMap, T> inputMapGenerator)
    {
        return inputMapGenerator._generateDelegate;
    }
}

public static class InputMapGeneratorExtensions
{
    public static Func<TArg, T> MapInput<TArg, TMap, T>(
        this Func<TMap, T> innerGenerator,
        Func<TArg, TMap> mapFunc
    )
    {
        return new InputMapGenerator<TArg, TMap, T>(innerGenerator, mapFunc);
    }
}
