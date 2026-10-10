namespace Rmg.Tests.Page;

/// <summary>The song's settings as they show: every slider's ends beside it, none over it.</summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class SongSettingsTest(Site site)
{
    [Test]
    public async Task EverySlidersEnds_FitBesideIt()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        await page.Page.Locator("#settings-panel summary").ClickAsync();

        await Assert.That(await page.Page.Locator(".setting .end").CountAsync()).IsGreaterThan(0);
        var over = await page.Page.EvaluateAsync<string[]>(
            "() => [...document.querySelectorAll('.setting .end')].filter(x => x.scrollWidth > x.clientWidth + 1).map(x => x.textContent)");
        await Assert.That(over).IsEmpty();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task EveryRowsName_AndItsControl_LineUp_TheGenresToo()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        await page.Page.Locator("#settings-panel summary").ClickAsync();
        await page.Until(async () => await page.Page.Locator("select[aria-label='Genre']").IsEnabledAsync(), "the genres listed");

        // every row's name and control as far in as every other's, the genre's, which has no checkbox, among them
        var lefts = await page.Page.EvaluateAsync<double[][]>(
            "() => [...document.querySelectorAll('.setting')].filter(x => x.offsetParent).map(x => [x.querySelector('.setting-name').getBoundingClientRect().left, x.querySelector('.setting-slider').getBoundingClientRect().left])");
        await Assert.That(lefts.Select(x => Math.Round(x[0])).Distinct().Count()).IsEqualTo(1);
        await Assert.That(lefts.Select(x => Math.Round(x[1])).Distinct().Count()).IsEqualTo(1);
        // and every list as wide as a slider: from where it starts to where a slider ends
        var spans = await page.Page.EvaluateAsync<double[][]>(
            "() => [...document.querySelectorAll('.setting')].filter(x => x.offsetParent).map(x => x.querySelector('.setting-slider select, .setting-slider input[type=range]')).map(x => [x.getBoundingClientRect().left, x.getBoundingClientRect().right])");
        await Assert.That(spans.Select(x => Math.Round(x[0])).Distinct().Count()).IsEqualTo(1);
        await Assert.That(spans.Select(x => Math.Round(x[1])).Distinct().Count()).IsEqualTo(1);
        await Assert.That(page.Errors).IsEmpty();
    }
}
