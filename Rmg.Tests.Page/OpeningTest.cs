using Microsoft.Playwright;

namespace Rmg.Tests.Page;

[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class OpeningTest(Site site)
{
    [Test]
    public async Task ThePage_OpensWithASong_AndNoErrors()
    {
        await using var page = await SongPage.Open(site);

        await page.FirstSong();

        await Assert.That(page.Requests.Count).IsEqualTo(1);
        await Assert.That(await page.Status()).Contains("Generated song");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task TheDashboard_ShowsTheServersSongs()
    {
        await using (var player = await SongPage.Open(site)) await player.FirstSong();

        await using var page = await SongPage.Open(site, "admin/dashboard.html", admin: true);

        var server = page.Page.Locator("#server-panel");
        await Assertions.Expect(server).ToBeVisibleAsync();
        await Assertions.Expect(server.Locator("#mk-p50")).Not.ToHaveTextAsync("—");
        await Assertions.Expect(server.Locator("#outcomes")).ToContainTextAsync("Made");
        await Assert.That(page.Errors).IsEmpty();
    }
}
