namespace Rmg.Tests.Page;

/// <summary>A song's length among the settings: drawn, shown in minutes as the song played, or given and asked for.</summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class LengthTest(Site site)
{
    private static Microsoft.Playwright.ILocator Output(SongPage page) => page.Page.Locator(".setting:has(input[data-setting='length']) output");

    [Test]
    public async Task ALength_ShowsHowLongTheSongPlayed_AndGiven_IsAskedFor()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        await page.Page.EvaluateAsync("() => document.getElementById('settings-panel').open = true");

        // drawn, the length the song played, in minutes and seconds
        await page.Until(async () => System.Text.RegularExpressions.Regex.IsMatch(await Output(page).TextContentAsync() ?? "", @"^\d+:\d\d$"), "the length shown");
        await Assert.That(page.Requests[^1].Body.TryGetProperty("length", out _)).IsFalse();

        // given at its first step, half a minute, and asked for
        await page.SetSetting("length", 0);
        await page.Until(() => page.Requests[^1].Body.TryGetProperty("length", out var length) && length.GetInt32() == 0, "the song asked for with the length given");
        await Assert.That(await Output(page).TextContentAsync()).IsEqualTo("0:30");
        await Assert.That(page.Errors).IsEmpty();
    }
}
