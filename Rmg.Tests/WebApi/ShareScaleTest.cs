using Rmg.Core.Composition;
using Rmg.WebApi.Songs;

namespace Rmg.Tests.WebApi;

public sealed class ShareScaleTest
{
    [Test]
    public async Task ItsSteps_RiseFrom0To1_TheLeastAbove0AHundredth_AndEveryChanceTheSongsHaveAmongThem()
    {
        await Assert.That(ShareScale.Amounts[0]).IsEqualTo(0);
        await Assert.That(ShareScale.Amounts[1]).IsEqualTo(0.01);
        await Assert.That(ShareScale.Amounts[^1]).IsEqualTo(1);
        await Assert.That(ShareScale.Amounts.Zip(ShareScale.Amounts.Skip(1)).All(x => x.Second > x.First)).IsTrue();
        await Assert.That(ShareScale.Amounts.Length).IsLessThanOrEqualTo(64);
        foreach (var choice in GenreChoices.All.Where(x => x.Kind == GenreChoiceKind.Chances))
        foreach (var option in choice.Options)
            await Assert.That(ShareScale.Amounts[ShareScale.ToStep(option.Default)]).IsEqualTo(option.Default).Within(1e-9).Because($"{choice.Name} {option.Name}");
    }
}
