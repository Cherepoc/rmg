using Rmg.Core;

namespace Rmg.Tests.MathExtensions;

public class BounceInBoundsTest
{
    [Test]
    [Arguments(-3.25, 0.75)]
    [Arguments(-2, 0)]
    [Arguments(-1.75, -0.25)]
    [Arguments(-1.5, -0.5)]
    [Arguments(-1.25, -0.75)]
    [Arguments(-1, -1)]
    [Arguments(-0.75, -0.75)]
    [Arguments(-0.5, -0.5)]
    [Arguments(-0.25, -0.25)]
    [Arguments(0, 0)]
    [Arguments(0.25, 0.25)]
    [Arguments(0.5, 0.5)]
    [Arguments(0.75, 0.75)]
    [Arguments(1, 1)]
    [Arguments(1.25, 0.75)]
    [Arguments(1.5, 0.5)]
    [Arguments(1.75, 0.25)]
    [Arguments(2, 0)]
    public async Task BounceDouble(double inputValue, double expectedValue)
    {
        var result = inputValue.BounceInBounds(-1, 1);

        await Assert.That(result).IsEqualTo(expectedValue);
    }
    
    [Test]
    [Arguments(-7, 1)]
    [Arguments(-6, 2)]
    [Arguments(-5, 2)]
    [Arguments(-4, 1)]
    [Arguments(-3, 0)]
    [Arguments(-2, -1)]
    [Arguments(-1, -1)]
    [Arguments(0, 0)]
    [Arguments(1, 1)]
    [Arguments(2, 2)]
    [Arguments(3, 2)]
    [Arguments(4, 1)]
    [Arguments(5, 0)]
    [Arguments(6, -1)]
    [Arguments(7, -1)]
    [Arguments(8, 0)]
    public async Task BounceInt(int inputValue, int expectedValue)
    {
        var result = inputValue.BounceInBounds(-1, 2);

        await Assert.That(result).IsEqualTo(expectedValue);
    }
}