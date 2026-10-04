namespace Rmg.Tests.Page;

/// <summary>A genre chosen on the page: asked for by its choices, kept in the link, and let go of.</summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class GenreTest(Site site)
{
    [Test]
    public async Task APresetChosen_IsAskedFor_ByItsChoices_AndNoneLetsItGo()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        await page.FirstSong();
        await page.Settled();

        var genre = page.Page.Locator("select[aria-label='Genre']");
        await page.Until(async () => await genre.IsEnabledAsync() && await genre.Locator("option").CountAsync() > 10, "the genres listed");
        var count = page.Requests.Count;
        // as a hand would, wherever the settings' panel is folded away
        await genre.EvaluateAsync("select => { select.value = 'preset:jazz'; select.dispatchEvent(new Event('change')); }");
        await page.Until(() => page.Requests.Count > count, "the song asked for in the genre");
        var asked = page.Requests[^1].Body.GetProperty("genre");
        await Assert.That(asked.GetProperty("kit").GetProperty("jazz").GetInt32()).IsEqualTo(100);
        await Assert.That(asked.GetProperty("drumRole.time").GetProperty("ride").GetInt32()).IsEqualTo(100);
        await page.Settled();
        await Assert.That(await page.Given()).IsEqualTo("1 given");
        // the list shows it chosen, and the next visit asks for it again, kept in this browser, not the link
        await Assert.That(page.Page.Url).Contains("settings=A");
        await Assert.That(await genre.InputValueAsync()).IsEqualTo("preset:jazz");
        await page.Page.GotoAsync("/");
        await page.Until(() => page.Requests.Count > count + 1 && page.Requests[^1].Body.TryGetProperty("genre", out var kept) && kept.TryGetProperty("kit", out _),
            "the song of the next visit asked for in the genre kept");
        await page.Settled();
        genre = page.Page.Locator("select[aria-label='Genre']");
        await page.Until(async () => await genre.InputValueAsync() == "preset:jazz", "the genre kept shown chosen");

        count = page.Requests.Count;
        await genre.EvaluateAsync("select => { select.value = 'none'; select.dispatchEvent(new Event('change')); }");
        await page.Until(() => page.Requests.Count > count, "the song asked for in no genre");
        await Assert.That(page.Requests[^1].Body.TryGetProperty("genre", out _)).IsFalse();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task ASongsLink_CarriesItsGenresKey_AndOpensInIt_WhereTheBrowserNeverHadIt()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        await page.FirstSong();
        await page.Settled();
        var genre = page.Page.Locator("select[aria-label='Genre']");
        await page.Until(async () => await genre.IsEnabledAsync() && await genre.Locator("option").CountAsync() > 10, "the genres listed");
        await genre.EvaluateAsync("select => { select.value = 'preset:jazz'; select.dispatchEvent(new Event('change')); }");
        await page.Until(() => Task.FromResult(page.Page.Url.Contains("genre=")), "the link carrying the genre's key");
        var link = page.Page.Url;
        await page.Settled();

        // a browser that never had it: nothing kept, the genre read by its key
        await page.Page.EvaluateAsync("() => localStorage.clear()");
        var count = page.Requests.Count;
        await page.Page.GotoAsync(link);
        await page.Until(() => page.Requests.Count > count, "the linked song asked for");
        var asked = page.Requests[^1].Body;
        await Assert.That(asked.GetProperty("genre").GetProperty("kit").GetProperty("jazz").GetInt32()).IsEqualTo(100);
        await Assert.That(asked.GetProperty("genreName").GetString()).IsEqualTo("Jazz");
        await Assert.That(page.Errors).IsEmpty();
    }
}
