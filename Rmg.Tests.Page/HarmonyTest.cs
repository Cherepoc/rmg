namespace Rmg.Tests.Page;

/// <summary>The key, its mode and the chord playing, beside the times, and the seek bar on a line of its own.</summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class HarmonyTest(Site site)
{
    private const string Shown = @"^[A-G][♯♭]? [A-Z][a-z]+( [a-z]+)* · [A-G][♯♭]?\S*$";

    [Test]
    public async Task TheKeyAndChord_AreShownAsTheSongPlays_AndFollowASeek()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        var harmony = page.Page.Locator("#harmony");

        // where the song starts, as its file marks it
        await page.Until(async () => System.Text.RegularExpressions.Regex.IsMatch(await harmony.TextContentAsync() ?? "", Shown), "the key and chord shown");

        // and where a seek puts it: the readout of every place the bar is dragged to, as the file marks them, changing
        // along the song
        var seen = new HashSet<string>();
        foreach (var at in new[] { 0.2, 0.5, 0.8 })
        {
            await page.Page.Locator("#seek").EvaluateAsync("(bar, at) => { bar.value = String(at); bar.dispatchEvent(new Event('input')); }", at);
            await page.Until(async () => System.Text.RegularExpressions.Regex.IsMatch(await harmony.TextContentAsync() ?? "", Shown), $"the key and chord at {at}");
            seen.Add((await harmony.TextContentAsync())!);
        }

        await Assert.That(seen.Count).IsGreaterThan(1);
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task TheSeekBar_IsOnALineOfItsOwn_AsWideAsThePanel(bool phone)
    {
        await using var page = await SongPage.Open(site, phone: phone);
        await page.FirstSong();

        var measures = await page.Page.EvaluateAsync<double[]>("""
            () => {
                const bar = document.getElementById("seek").getBoundingClientRect();
                const panel = document.querySelector(".song-panel");
                const style = getComputedStyle(panel);
                const inner = panel.getBoundingClientRect().width - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight);
                return [bar.width, inner, document.getElementById("play").getBoundingClientRect().bottom - bar.top];
            }
            """);
        var (bar, panel, transport) = (measures[0], measures[1], measures[2]);
        await Assert.That(bar).IsGreaterThanOrEqualTo(panel - 2);
        // under the playing buttons, not beside them
        await Assert.That(transport).IsLessThanOrEqualTo(0);
        await Assert.That(page.Errors).IsEmpty();
    }
}
