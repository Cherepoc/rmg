namespace Rmg.Tests.Page;

/// <summary>The song's link handed over by Share: copied on a desktop, the song and its settings in it.</summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class ShareTest(Site site)
{
    [Test]
    public async Task OnADesktop_Share_CopiesTheLink_EvenWhereTheBrowserOffersAShareSheetWithNothingBehindIt()
    {
        await using var page = await SongPage.Open(site);
        await page.Page.Context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"]);
        // as a browser on Linux has it: a share sheet that fails as if it were closed
        await page.Page.AddInitScriptAsync("navigator.share = () => Promise.reject(new DOMException('no share sheet', 'AbortError'));");
        await page.Page.ReloadAsync();
        await page.FirstSong();

        await page.Page.Locator("#share").ClickAsync();
        await page.Until(async () => await page.Page.Locator("#share-label").TextContentAsync() == "Copied", "the link copied");
        var copied = await page.Page.EvaluateAsync<string>("() => navigator.clipboard.readText()");
        await Assert.That(copied).Contains($"song={await page.Seed()}");
        await Assert.That(copied).Contains("settings=");
        await Assert.That(page.Errors).IsEmpty();
    }
}
