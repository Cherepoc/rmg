namespace Rmg.Core.Probabilities;

public sealed class GenerationContext : IGenerationContext
{
    private readonly Xoshiro256 _random;

    public GenerationContext(ulong seed)
    {
        _random = new Xoshiro256(seed);
    }

    public double GenerateDouble()
    {
        return _random.NextDouble();
    }

    /// <summary>A number from 0 to <see cref="int.MaxValue" />, as <see cref="System.Random.Next()" /> draws one.</summary>
    public int GenerateInt()
    {
        return (int)(_random.Next() >> 33);
    }

    public int GenerateInt(int min, int max)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(min, max);
        return min + (int)_random.NextBelow((ulong)((long)max - min));
    }

    public ulong GenerateSeed()
    {
        return _random.Next();
    }

    public bool TestProbability(double probability)
    {
        if (probability is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(probability), "Probability must be between 0 and 1");

        if (probability.IsEqualToByEpsilon(1))
            return true;

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (probability.IsEqualToByEpsilon(0))
            return false;

        return GenerateDouble() < probability;
    }

    public IGenerationContext CreateContext(ulong seed)
    {
        return new GenerationContext(seed);
    }
}

public interface IGenerationContext
{
    double GenerateDouble();

    int GenerateInt();

    int GenerateInt(int min, int max);

    /// <summary>64 random bits, for the seed of another sequence.</summary>
    ulong GenerateSeed();

    bool TestProbability(double probability);

    IGenerationContext CreateContext(ulong seed);
}
