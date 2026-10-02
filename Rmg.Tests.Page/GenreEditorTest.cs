using Microsoft.Playwright;

namespace Rmg.Tests.Page;

/// <summary>A genre made in the editor's popup: its pools swapped in their lists, its tabs, played, kept among one's own, and let go of.</summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class GenreEditorTest(Site site)
{
    private static async Task<ILocator> OpenEditor(SongPage page)
    {
        await page.FirstSong();
        await page.Settled();
        await page.Page.Locator("#settings-panel summary").ClickAsync();
        var edit = page.Page.Locator("button.genre-edit");
        await page.Until(() => edit.IsEnabledAsync(), "the editor ready");
        await edit.ClickAsync();
        var dialog = page.Page.Locator("dialog.genre-dialog");
        await page.Until(() => dialog.IsVisibleAsync(), "the editor open");
        return dialog;
    }

    private static ILocator Section(ILocator dialog, string title) => dialog.Locator(".genre-section", new() { Has = dialog.Page.Locator("h3", new() { HasTextString = title }) });

    [Test]
    public async Task AGenreMade_ItsMeterSwappedInItsList_IsPlayed_KeptAmongOnesOwn_AndListedOnTheNextVisit()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);

        // the meter kept to its commonest, 4/4 alone, then swapped for 3/4 in its list
        var meter = Section(dialog, "Meter");
        await meter.Locator(".genre-check input").CheckAsync();
        await Assert.That(await meter.Locator(".genre-row select").CountAsync()).IsEqualTo(1);
        await meter.Locator(".genre-row select").SelectOptionAsync("3/4");
        await dialog.Locator("input[aria-label='Genre name']").FillAsync("Waltzes");
        var count = page.Requests.Count;
        await dialog.Locator("footer button", new() { HasTextString = "Save" }).ClickAsync();

        await page.Until(() => page.Requests.Count > count, "the song asked for in the genre made");
        var meters = page.Requests[^1].Body.GetProperty("genre").GetProperty("meter");
        await Assert.That(meters.EnumerateObject().Select(x => x.Name)).IsEquivalentTo(["3/4"]);
        await page.Settled();

        // listed among one's own on the next visit, and chosen there
        await page.Page.GotoAsync("/");
        var genre = page.Page.Locator("select[aria-label='Genre']");
        await page.Until(async () => await genre.Locator("optgroup[label='Yours'] option").CountAsync() == 1, "the genre made listed");
        await Assert.That(await genre.Locator("optgroup[label='Yours'] option").TextContentAsync()).IsEqualTo("Waltzes");
        await Assert.That(await genre.InputValueAsync()).IsEqualTo("own:0");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task APoolKeepingNothing_IsNotPlayed()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var scale = Section(dialog, "Scale");
        await scale.Locator(".genre-check input").CheckAsync();
        while (await scale.Locator("button.genre-remove").CountAsync() > 0)
            await scale.Locator("button.genre-remove").First.ClickAsync();

        await Assert.That(await dialog.Locator("footer button", new() { HasTextString = "Play" }).IsDisabledAsync()).IsTrue();
        await Assert.That(await dialog.Locator("p.warning").TextContentAsync()).Contains("keeps none");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task ATabsFields_AreOneSet_BoundToTheTabsPart_AndATabTheGenreNamesIsMarked()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var instruments = Section(dialog, "Instruments");

        // the bass's instruments kept to its commonest, one more added
        await instruments.GetByRole(AriaRole.Tab, new() { Name = "Bass", Exact = true }).ClickAsync();
        await instruments.Locator(".genre-check input").CheckAsync();
        var rows = await instruments.Locator(".genre-row").CountAsync();
        await instruments.Locator("button.genre-add").ClickAsync();
        await Assert.That(await instruments.Locator(".genre-row").CountAsync()).IsEqualTo(rows + 1);

        // the melody's tab the same fields, as the songs have it, and the bass's marked
        await instruments.GetByRole(AriaRole.Tab, new() { Name = "Melody", Exact = true }).ClickAsync();
        await Assert.That(await instruments.Locator(".genre-check input").IsCheckedAsync()).IsFalse();
        await Assert.That(await instruments.Locator(".genre-row").CountAsync()).IsEqualTo(0);
        await Assert.That(await instruments.Locator("[role=tab].named").TextContentAsync()).IsEqualTo("Bass");

        var count = page.Requests.Count;
        await dialog.Locator("footer button", new() { HasTextString = "Play" }).ClickAsync();
        await page.Until(() => page.Requests.Count > count, "the song asked for in the genre made");
        var genre = page.Requests[^1].Body.GetProperty("genre");
        await Assert.That(genre.GetProperty("instrument.bass").EnumerateObject().Count()).IsEqualTo(rows + 1);
        await Assert.That(genre.TryGetProperty("instrument.melody", out _)).IsFalse();
        await Assert.That(page.Errors).IsEmpty();
    }
}
