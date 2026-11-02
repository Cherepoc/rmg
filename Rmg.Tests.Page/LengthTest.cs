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
        await page.Page.EvaluateAsync("() => document.getElementById('composition-panel').open = true");

        // drawn, the length the song played, in minutes and seconds
        await page.Until(async () => System.Text.RegularExpressions.Regex.IsMatch(await Output(page).TextContentAsync() ?? "", @"^\d+:\d\d$"), "the length shown");
        // the song's as it played, its length as the server made it, at the nearest step of the lengths a song may be given;
        // not the transport's total, which the player's own length takes over once it has it, its last notes' release in it
        static int Seconds(string? time) => time!.Split(':') is [var minutes, var seconds] ? int.Parse(minutes) * 60 + int.Parse(seconds) : -1;
        var lengths = await page.Page.EvaluateAsync<double[]>("async () => (await (await fetch('api/songs/options')).json()).lengths");
        await page.Until(() => page.Requests[^1].Answered is not null, "the song's settings answered");
        var total = page.Requests[^1].Answered!.Value.GetProperty("seconds").GetDouble();
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
