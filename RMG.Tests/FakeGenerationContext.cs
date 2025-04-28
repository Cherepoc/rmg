using Rmg.Core.Probabilities;

namespace Rmg.Tests;

public sealed class FakeGenerationContext : IGenerationContext
{
    public double NextDouble { get; set; }
    
    public double GenerateDouble()
    {
        return NextDouble;
    }

    public int GenerateInt()
    {
        throw new NotImplementedException();
    }

    public int GenerateInt(int min, int max)
    {
        throw new NotImplementedException();
    }

    public bool TestProbability(double probability)
    {
        throw new NotImplementedException();
    }

    public IGenerationContext CreateContext(int seed)
    {
        throw new NotImplementedException();
    }
}