namespace Rmg.Tests.Generators;

public sealed class SplineValueGeneratorTest
{
    [Test]
    [Arguments(0, -1)]
    [Arguments(0.125, -0.3385621722338523)]
    [Arguments(0.250, -0.1339745962155614)]
    [Arguments(0.375, -0.031754163448145745)]
    [Arguments(0.500, 0)]
    [Arguments(0.625, 0.031754163448145745)]
    [Arguments(0.750, 0.1339745962155614)]
    [Arguments(0.875, 0.3385621722338523)]
    [Arguments(1, 1)]
    public async Task GenerateSplineValues(double generatedValue, double expectedValue)
    {
        var generationContext = new FakeGenerationContext
        {
            NextDouble = generatedValue
        };

        var splineValueGenerator = Core.Probabilities.Generators.SplineValue();
        var result = splineValueGenerator(generationContext);

        await Assert.That(result).IsEqualTo(expectedValue);
    }
}