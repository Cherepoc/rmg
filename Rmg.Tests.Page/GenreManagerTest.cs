using Microsoft.Playwright;

namespace Rmg.Tests.Page;

/// <summary>
///     The genre on the song's panel, under the transport and out of the settings, and the window of every genre its
///     Manage button opens: a preset duplicated, one's own edited and removed, one made new.
/// </summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class GenreManagerTest(Site site)
{
    private static async Task<ILocator> OpenManager(SongPage page)
    {
        await page.FirstSong();
        await page.Settled();
        var manage = page.Page.Locator("button.genre-manage");
        await page.Until(() => manage.IsEnabledAsync(), "the genres listed");
        await manage.ClickAsync();
        var manager = page.Page.Locator("dialog.genre-manager");
        await page.Until(() => manager.IsVisibleAsync(), "the window open");
        return manager;
    }

    [Test]
    public async Task TheGenre_IsOnTheSongsPanel_UnderTheTransport_AndCountsAmongNoSettings()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        await page.FirstSong();
        await page.Settled();

        var genre = page.Page.Locator(".song-panel #genre-row select[aria-label='Genre']");
        await page.Until(() => genre.IsEnabledAsync(), "the genres listed");
        // the list and the window's button alone, seen with the settings folded away
        await Assert.That(await page.Page.Locator("#genre-row button").CountAsync()).IsEqualTo(1);
        await Assert.That(await genre.IsVisibleAsync()).IsTrue();
        await Assert.That(await page.Page.Locator("#settings-panel select[aria-label='Genre']").CountAsync()).IsEqualTo(0);
        var under = await page.Page.EvaluateAsync<bool>("() => document.querySelector('.transport').getBoundingClientRect().bottom <= document.getElementById('genre-row').getBoundingClientRect().top");
        await Assert.That(under).IsTrue();

        var count = page.Requests.Count;
        await genre.SelectOptionAsync("preset:rock");
        await page.Until(() => page.Requests.Count > count, "the song asked for in the genre");
        await page.Settled();
        await Assert.That(await page.Given()).IsEqualTo("All drawn");
        await Assert.That(await page.Page.Locator(".genre-about").TextContentAsync()).Contains("guitars");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task APreset_Duplicated_IsYours_ThenEdited_AndRemoved()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var manager = await OpenManager(page);
        var yours = page.Page.Locator("dialog.genre-manager ul[aria-label='Yours'] li");
        await Assert.That(await yours.CountAsync()).IsEqualTo(0);
        await Assert.That(await manager.Locator("ul[aria-label='Presets'] li").CountAsync()).IsGreaterThan(10);

        // duplicated, it opens in the editor in place of the window, kept among one's own under its name and a number
        await manager.Locator("ul[aria-label='Presets'] li", new() { HasText = "Jazz" }).First.Locator("button", new() { HasText = "Duplicate" }).ClickAsync();
        var editor = page.Page.Locator("dialog.genre-dialog");
        await page.Until(() => editor.IsVisibleAsync(), "the editor open");
        await Assert.That(await manager.IsVisibleAsync()).IsFalse();
        await Assert.That(await editor.Locator("input[aria-label='Genre name']").InputValueAsync()).IsEqualTo("Jazz 1");
        await editor.Locator(":scope > header > button.genre-close").ClickAsync();
        await page.Until(async () => !await editor.IsVisibleAsync(), "the editor closed");
        await Assert.That(await page.Page.Locator("select[aria-label='Genre'] optgroup[label='Yours'] option").TextContentAsync()).IsEqualTo("Jazz 1");

        // edited, the editor opens on it
        await page.FromManager("Yours", "Jazz 1", "Edit");
        await Assert.That(await editor.Locator("input[aria-label='Genre name']").InputValueAsync()).IsEqualTo("Jazz 1");
        await editor.Locator(":scope > header > button.genre-close").ClickAsync();
        manager = await page.Manager();
        await Assert.That(await yours.CountAsync()).IsEqualTo(1);

        // removed once that is confirmed, and gone from the list on the page too
        page.Page.Dialog += (_, prompt) => _ = prompt.AcceptAsync();
        await yours.First.Locator("button", new() { HasText = "Remove" }).ClickAsync();
        await page.Until(async () => await yours.CountAsync() == 0, "the genre removed");
        await Assert.That(await page.Page.Locator("select[aria-label='Genre'] optgroup[label='Yours'] option").CountAsync()).IsEqualTo(0);
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task ANewGenre_OpensTheEditor_OnNone()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var manager = await OpenManager(page);
        await manager.Locator("button", new() { HasText = "New genre" }).ClickAsync();
        var editor = page.Page.Locator("dialog.genre-dialog");
        await page.Until(() => editor.IsVisibleAsync(), "the editor open");
        await Assert.That(await editor.Locator("input[aria-label='Genre name']").InputValueAsync()).IsEqualTo("");
        await Assert.That(page.Errors).IsEmpty();
    }

    /// <summary>The genre's row and the window as they look, on a desktop and a phone, where RMG_SCREENSHOTS names a folder; nothing running off a phone's screen.</summary>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task TheRowAndTheWindow_AsTheyLook_NothingRunningOffAPhone(bool phone)
    {
        await using var page = await SongPage.Open(site, "?song=1", phone: phone);
        var directory = Environment.GetEnvironmentVariable("RMG_SCREENSHOTS");
        var manager = await OpenManager(page);
        var name = phone ? "phone" : "desktop";
        if (directory is not null)
            await page.Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"genre-manager-{name}.png") });
        var past = await page.Page.EvaluateAsync<string[]>(
            "() => [...document.querySelectorAll('dialog.genre-manager button, #genre-row select, #genre-row button')]" +
            ".filter(x => { const r = x.getBoundingClientRect(); return r.width > 0 && r.right > innerWidth; })" +
            ".map(x => x.getAttribute('aria-label') || x.textContent)");
        await Assert.That(past).IsEmpty();
        await manager.Locator(":scope > header > button.genre-close").ClickAsync();
        if (directory is not null)
            await page.Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"genre-row-{name}.png") });
        await Assert.That(page.Errors).IsEmpty();
    }
}
