namespace Rmg.Tests.Page;

/// <summary>A genre made in the editor: played, kept among one's own over a visit, and let go of.</summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class GenreEditorTest(Site site)
{
    [Test]
    public async Task AGenreMade_FromNone_IsPlayed_KeptAmongOnesOwn_AndListedOnTheNextVisit()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        await page.FirstSong();
        await page.Settled();
        var edit = page.Page.Locator("button.genre-edit");
        await page.Until(() => edit.IsEnabledAsync(), "the editor ready");

        // as a hand would, wherever the settings' panel is folded away: opened on none, a meter's choice added, 3/4 kept
        // alone, named, and saved
        await edit.EvaluateAsync("b => b.click()");
        var editor = page.Page.Locator(".genre-editor");
        await Change(editor.Locator("select[aria-label='Add a choice of tempo and feel']"), "meter");
        foreach (var meter in await editor.Locator(".genre-choice[data-choice='meter'] input[type=range]").AllAsync())
            if (await meter.GetAttributeAsync("data-option") != "3/4")
                await meter.EvaluateAsync("r => { r.value = '0'; r.dispatchEvent(new Event('input')); }");
        await editor.Locator(".genre-choice[data-choice='meter'] input[data-option='3/4']").EvaluateAsync("r => { r.value = '63'; r.dispatchEvent(new Event('input')); }");
        await editor.Locator("input[aria-label='Genre name']").EvaluateAsync("i => { i.value = 'Waltzes'; i.dispatchEvent(new Event('input')); }");
        var count = page.Requests.Count;
        await editor.Locator("button", new() { HasText = "Save" }).EvaluateAsync("b => b.click()");

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
        await page.FirstSong();
        var edit = page.Page.Locator("button.genre-edit");
        await page.Until(() => edit.IsEnabledAsync(), "the editor ready");
        await edit.EvaluateAsync("b => b.click()");
        var editor = page.Page.Locator(".genre-editor");
        await Change(editor.Locator("select[aria-label='Add a choice of harmony']"), "scale");
        foreach (var scale in await editor.Locator(".genre-choice[data-choice='scale'] input[type=range]").AllAsync())
            await scale.EvaluateAsync("r => { r.value = '0'; r.dispatchEvent(new Event('input')); }");

        await Assert.That(await editor.Locator("button", new() { HasText = "Play" }).IsDisabledAsync()).IsTrue();
        await Assert.That(await editor.Locator("p.warning").TextContentAsync()).Contains("keeps none");
        await Assert.That(page.Errors).IsEmpty();
    }

    private static Task Change(Microsoft.Playwright.ILocator select, string value) =>
        select.EvaluateAsync("(s, v) => { s.value = v; s.dispatchEvent(new Event('change')); }", value);
}
