namespace Rmg.Core.Probabilities;

public sealed class GenerationContext : IGenerationContext
{
    private readonly Random _random;
    
    public GenerationContext()
    {
        _random = new Random();
    }
    
    public GenerationContext(int seed)
    {
        _random = new Random(seed);
    }
    
    public double GenerateDouble()
    {
        return _random.NextDouble();
    }
    
    public int GenerateInt()
    {
        return _random.Next();
    }
    
    public int GenerateInt(int min, int max)
    {
        return _random.Next(min, max);
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
    
    public IGenerationContext CreateContext(int seed)
    {
        return new GenerationContext(seed);
    }
}

public interface IGenerationContext
{
    double GenerateDouble();

    int GenerateInt();
    
    int GenerateInt(int min, int max);

    bool TestProbability(double probability);

    IGenerationContext CreateContext(int seed);
}