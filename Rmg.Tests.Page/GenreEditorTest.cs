using Microsoft.Playwright;

namespace Rmg.Tests.Page;

/// <summary>A genre made in the editor's popup: its sections, its checklists and lists, its parts' tabs, played, kept among one's own.</summary>
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

    /// <summary>A section of the editor, opened.</summary>
    private static async Task<ILocator> Section(ILocator dialog, string title)
    {
        var section = dialog.Locator($"details[data-section='{title}']");
        if (await section.GetAttributeAsync("open") is null)
            await section.Locator("summary").First.ClickAsync();
        return section;
    }

    private static ILocator Row(ILocator within, string option) => within.Locator($".genre-row[data-option='{option}']:visible");

    private static async Task<System.Text.Json.JsonElement> Played(SongPage page, ILocator dialog)
    {
        var count = page.Requests.Count;
        await dialog.Locator("footer button", new() { HasTextString = "Play" }).ClickAsync();
        await page.Until(() => page.Requests.Count > count, "the song asked for in the genre made");
        return page.Requests[^1].Body;
    }

    [Test]
    public async Task AMeterChecked_IsTheGenres_SavedAmongOnesOwn_AndListedOnTheNextVisit()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);

        var meter = await Section(dialog, "Meter");
        await Row(meter, "3/4").Locator("input[type=checkbox]").CheckAsync();
        await dialog.Locator("input[aria-label='Genre name']").FillAsync("Waltzes");
        var count = page.Requests.Count;
        await dialog.Locator("footer button", new() { HasTextString = "Save" }).ClickAsync();
        await page.Until(() => page.Requests.Count > count, "the song asked for in the genre made");
        await Assert.That(page.Requests[^1].Body.GetProperty("genre").GetProperty("meter").EnumerateObject().Select(x => x.Name)).IsEquivalentTo(["3/4"]);
        await page.Settled();

        await page.Page.GotoAsync("/");
        var genre = page.Page.Locator("select[aria-label='Genre']");
        await page.Until(async () => await genre.Locator("optgroup[label='Yours'] option").CountAsync() == 1, "the genre made listed");
        await Assert.That(await genre.Locator("optgroup[label='Yours'] option").TextContentAsync()).IsEqualTo("Waltzes");
        await Assert.That(await genre.InputValueAsync()).IsEqualTo("own:0");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task NoneChecked_IsAsTheSongsHaveIt()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var scale = await Section(dialog, "Scale");
        var dorian = Row(scale, "dorian").Locator("input[type=checkbox]");
        await dorian.CheckAsync();
        await dorian.UncheckAsync();
        await Section(dialog, "Meter").ContinueWith(async x => await Row(await x, "4/4").Locator("input[type=checkbox]").CheckAsync()).Unwrap();

        var genre = (await Played(page, dialog)).GetProperty("genre");
        await Assert.That(genre.TryGetProperty("scale", out _)).IsFalse();
        await Assert.That(genre.TryGetProperty("meter", out _)).IsTrue();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task APartsTab_ItsInstrumentsSwappedInTheirLists_AndTheOtherPartsAsTheSongsHaveThem()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var parts = await Section(dialog, "Parts");

        await parts.GetByRole(AriaRole.Tab, new() { Name = "Bass", Exact = true }).ClickAsync();
        await parts.Locator("button.genre-add:visible").ClickAsync();
        var rows = await parts.Locator(".genre-rows .genre-row:visible").CountAsync();
        await Assert.That(rows).IsGreaterThan(0);
        await parts.Locator(".genre-rows .genre-row:visible select").First.SelectOptionAsync("tuba");

        await parts.GetByRole(AriaRole.Tab, new() { Name = "Melody", Exact = true }).ClickAsync();
        await Assert.That(await parts.Locator(".genre-rows .genre-row:visible").CountAsync()).IsEqualTo(0);
        await Assert.That(await parts.Locator("[role=tab].named").First.TextContentAsync()).IsEqualTo("Bass");

        var genre = (await Played(page, dialog)).GetProperty("genre");
        await Assert.That(genre.GetProperty("instrument.bass").TryGetProperty("tuba", out _)).IsTrue();
        await Assert.That(genre.TryGetProperty("instrument.melody", out _)).IsFalse();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task TheDrumsSetupsAndRoles_KeepToThePiecesChosen()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var parts = await Section(dialog, "Parts");
        await parts.GetByRole(AriaRole.Tab, new() { Name = "Drums", Exact = true }).ClickAsync();

        // congas and bongos alone, percussion: no kit to set up, and only they to take a role
        var pieces = parts.Locator(".genre-sliders[data-choice='drumPieces']");
        await Row(pieces, "conga").Locator("input[type=checkbox]").CheckAsync();
        await Row(pieces, "bongo").Locator("input[type=checkbox]").CheckAsync();
        var setup = parts.Locator(".genre-sliders[data-choice='drumSetup']");
        await Assert.That(await setup.Locator(".genre-row").EvaluateAllAsync<string[]>("rows => rows.map(x => x.dataset.option)")).IsEquivalentTo(["percussion"]);
        var ground = parts.Locator(".genre-sliders[data-choice='drumRole.ground']");
        await Assert.That(await ground.Locator(".genre-row").EvaluateAllAsync<string[]>("rows => rows.map(x => x.dataset.option)")).IsEquivalentTo(["bongo", "conga"]);
        await Assert.That(page.Errors).IsEmpty();
    }

    /// <summary>The editor's parts as they look, a preset's, its melody's tab and its drums', where RMG_SCREENSHOTS names a folder.</summary>
    [Test]
    [Explicit]
    public async Task Screenshots()
    {
        var directory = Environment.GetEnvironmentVariable("RMG_SCREENSHOTS") ?? Path.GetTempPath();
        await using var page = await SongPage.Open(site, "?song=1");
        await page.FirstSong();
        await page.Page.SetViewportSizeAsync(1280, 1000);
        await page.Page.Locator("#settings-panel summary").ClickAsync();
        var genre = page.Page.Locator("select[aria-label='Genre']");
        await page.Until(() => genre.IsEnabledAsync(), "the genres listed");
        await genre.EvaluateAsync("s => { s.value = 'preset:latin'; s.dispatchEvent(new Event('change')); }");
        await page.Page.Locator("button.genre-edit").EvaluateAsync("b => b.click()");
        var dialog = page.Page.Locator("dialog.genre-dialog");
        await page.Until(() => dialog.IsVisibleAsync(), "the editor open");
        var parts = await Section(dialog, "Parts");
        await parts.ScrollIntoViewIfNeededAsync();
        await page.Page.ScreenshotAsync(new() { Path = Path.Combine(directory, "genre-editor-melody.png") });
        await parts.GetByRole(AriaRole.Tab, new() { Name = "Drums", Exact = true }).ClickAsync();
        await parts.Locator(".genre-sliders[data-choice='drumSetup']").ScrollIntoViewIfNeededAsync();
        await page.Page.ScreenshotAsync(new() { Path = Path.Combine(directory, "genre-editor-drums.png") });
    }
}
