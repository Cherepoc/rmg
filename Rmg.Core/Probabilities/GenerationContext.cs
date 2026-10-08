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

    /// <summary>A number from min up to max, max not among them; min where they are the same, a draw spent all the same, so that the draws are as many.</summary>
    public int GenerateInt(int min, int max)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(min, max);
        return min + (int)_random.NextBelow((ulong)((long)max - min));
    }

    public ulong GenerateSeed()
    {
        return _random.Next();
    }

    /// <summary>
    ///     Whether a thing of the chance happens: a draw under it, made whatever the chance, so that a chance at an end, as a
    ///     facet supplied at 0 or 1 makes it, leaves the draws after it as they were; never within a thousandth of 0 and
    ///     always within one of 1, as a chance a curve or a lean brings next to an end is meant to be there.
    /// </summary>
    public bool TestProbability(double probability)
    {
        if (probability is < 0 or > 1 or double.NaN)
            throw new ArgumentOutOfRangeException(nameof(probability), "Probability must be between 0 and 1");

        var draw = GenerateDouble();
        return probability.IsEqualToByEpsilon(1) || !probability.IsEqualToByEpsilon(0) && draw < probability;
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
