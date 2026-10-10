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
        // the song's as it played, its total on the transport, at the nearest step of the lengths a song may be given
        static int Seconds(string? time) => time!.Split(':') is [var minutes, var seconds] ? int.Parse(minutes) * 60 + int.Parse(seconds) : -1;
        var lengths = await page.Page.EvaluateAsync<double[]>("async () => (await (await fetch('api/songs/options')).json()).lengths");
        await page.Until(async () => await page.Page.Locator("#total").TextContentAsync() != "0:00", "the song's total shown");
        var total = Seconds(await page.Page.Locator("#total").TextContentAsync());
        var nearest = lengths.MinBy(x => Math.Abs(x - total));
        await Assert.That(Seconds(await Output(page).TextContentAsync())).IsEqualTo((int)Math.Round(nearest)).Because($"total {total}, steps {string.Join(", ", lengths)}");
        await Assert.That(page.Requests[^1].Body.TryGetProperty("length", out _)).IsFalse();

        // given at its first step, half a minute, and asked for
        await page.SetSetting("length", 0);
        await page.Until(() => page.Requests[^1].Body.TryGetProperty("length", out var length) && length.GetInt32() == 0, "the song asked for with the length given");
        await Assert.That(await Output(page).TextContentAsync()).IsEqualTo("0:30");
        await Assert.That(page.Errors).IsEmpty();
    }
}
