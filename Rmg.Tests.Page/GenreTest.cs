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
        await Assert.That(asked.GetProperty("kit").GetProperty("jazz").GetInt32()).IsEqualTo(63);
        await Assert.That(asked.GetProperty("drumRole.time").GetProperty("ride").GetInt32()).IsEqualTo(63);
        await page.Settled();
        await Assert.That(await page.Given()).IsEqualTo("1 given");
        // the link carries it, and the list shows it chosen
        await Assert.That(page.Page.Url).Contains("settings=8");
        await Assert.That(await genre.InputValueAsync()).IsEqualTo("preset:jazz");

        count = page.Requests.Count;
        await genre.EvaluateAsync("select => { select.value = 'none'; select.dispatchEvent(new Event('change')); }");
        await page.Until(() => page.Requests.Count > count, "the song asked for in no genre");
        await Assert.That(page.Requests[^1].Body.TryGetProperty("genre", out _)).IsFalse();
        await Assert.That(page.Errors).IsEmpty();
    }
}
