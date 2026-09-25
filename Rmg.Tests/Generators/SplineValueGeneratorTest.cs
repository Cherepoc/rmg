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

    [Test]
    [Arguments(0.25, 0.5, 0)]
    [Arguments(0.0625, 0.5, -0.1339745962155614)]
    [Arguments(0.5625, 0.5, 0.1339745962155614)]
    [Arguments(0.75, 1, 0.1339745962155614)]
    public async Task SkewedSplineValue_RaisesDrawToSkewFirst(double generatedValue, double skew, double expectedValue)
    {
        // the draw raised to the skew is 0.5, 0.25 and 0.75, which the unskewed spline turns into 0 and -+0.134
        var generationContext = new FakeGenerationContext
        {
            NextDouble = generatedValue
        };

        var result = Core.Probabilities.Generators.SplineValue(1, skew)(generationContext);

        await Assert.That(result).IsEqualTo(expectedValue).Within(1e-12);
    }
}
