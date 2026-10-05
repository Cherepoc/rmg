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

        var over = await page.Page.EvaluateAsync<string[]>(
            "() => [...document.querySelectorAll('.setting .end')].filter(x => x.scrollWidth > x.clientWidth + 1).map(x => x.textContent)");
        await Assert.That(over).IsEmpty();
        await Assert.That(page.Errors).IsEmpty();
    }
}
