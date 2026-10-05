using Rmg.WebApi.Songs;

namespace Rmg.Tests.WebApi;

public sealed class StepScaleTest
{
    [Test]
    [Arguments(0, 0.0)]
    [Arguments(46, 0.75)]
    [Arguments(StepScale.Middle, 0.5)]
    [Arguments(StepScale.Last, 1.0)]
    public async Task AStep_IsItsAmount_TheMiddleStepExactlyAHalf(int step, double amount)
    {
        await Assert.That(StepScale.ToAmount(step)).IsEqualTo(amount);
        await Assert.That(StepScale.ToStep(amount)).IsEqualTo(step);
    }

    [Test]
    public async Task EveryStep_IsMoreThanTheOneBefore_AndIsItsAmountsNearestStep()
    {
        foreach (var step in Enumerable.Range(0, StepScale.Steps))
        {
            await Assert.That(StepScale.ToStep(StepScale.ToAmount(step))).IsEqualTo(step);
            if (step > 0) await Assert.That(StepScale.ToAmount(step)).IsGreaterThan(StepScale.ToAmount(step - 1));
        }
    }
}
