namespace Rmg.Tests.Page;

/// <summary>The key, its mode and the chord playing, the tempo and the meter, beside the times, and the seek bar on a line of its own.</summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class HarmonyTest(Site site)
{
    private const string Shown = @"^[A-G][♯♭]? [A-Z][a-z]+( [a-z]+)* · ([A-G][♯♭]?\S*|N\.C\.) · \d+ BPM · \d+/\d+$";

    [Test]
    public async Task TheKeyChordTempoAndMeter_AreShownAsTheSongPlays_AndFollowASeek()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        var harmony = page.Page.Locator("#harmony");

        // and where a seek puts it: the readout of every place the bar is dragged to, as the file marks them, changing
        // along the song
        var seen = new HashSet<string>();
        foreach (var at in new[] { 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9 })
        {
            await page.Page.Locator("#seek").EvaluateAsync("(bar, at) => { bar.value = String(at); bar.dispatchEvent(new Event('input')); }", at);
            await page.Until(async () => System.Text.RegularExpressions.Regex.IsMatch(await harmony.TextContentAsync() ?? "", Shown), $"the key and chord at {at}");
            seen.Add((await harmony.TextContentAsync())!);
        }

        await Assert.That(seen.Count).IsGreaterThan(1);
        await Assert.That(page.Errors).IsEmpty();
    }

    /// <summary>The seek bar on a line of its own, the time played and the song's length either side of it, the three as wide as the panel.</summary>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task TheSeekBar_IsOnALineOfItsOwn_BetweenItsTimes_AsWideAsThePanel(bool phone)
    {
        await using var page = await SongPage.Open(site, phone: phone);
        await page.FirstSong();

        var measures = await page.Page.EvaluateAsync<double[]>("""
            () => {
                const box = (id) => document.getElementById(id).getBoundingClientRect();
                const panel = document.querySelector(".song-panel");
                const style = getComputedStyle(panel);
                const inner = panel.getBoundingClientRect().width - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight);
                const middle = (r) => r.top + r.height / 2;
                const [elapsed, seek, total] = [box("elapsed"), box("seek"), box("total")];
                return [total.right - elapsed.left, inner, Math.max(Math.abs(middle(seek) - middle(elapsed)), Math.abs(middle(seek) - middle(total))),
                    elapsed.right <= seek.left && seek.right <= total.left ? 1 : 0, box("play").bottom - seek.top];
            }
            """);
        // the row the panel's width, the times either side of the bar on its line, and the line under the playing buttons
        await Assert.That(measures[0]).IsGreaterThanOrEqualTo(measures[1] - 2);
        await Assert.That(measures[2]).IsLessThan(8);
        await Assert.That(measures[3]).IsEqualTo(1);
        await Assert.That(measures[4]).IsLessThanOrEqualTo(0);
        await Assert.That(page.Errors).IsEmpty();
    }

    /// <summary>The key and chord follow the bar as a hand drags it, playing or not, and stay where it is let go.</summary>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task TheKeyAndChord_FollowTheBar_AsAHandDragsIt(bool playing)
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        var harmony = page.Page.Locator("#harmony");
        // a song that opens on its drums alone shows none where it starts, so that none is waited for here
        if (playing)
        {
            await page.Page.Locator("#play").ClickAsync();
            await page.Until(async () => await page.Page.Locator("#play").GetAttributeAsync("aria-label") == "Pause", "the song playing");
        }

        var bar = (await page.Page.Locator("#seek").BoundingBoxAsync())!;
        double X(double share) => bar.X + 8 + (bar.Width - 16) * share;
        var y = bar.Y + bar.Height / 2;
        var seen = new List<string>();
        await page.Page.Mouse.MoveAsync((float)X(0.1), (float)y);
        await page.Page.Mouse.DownAsync();
        foreach (var share in new[] { 0.3, 0.6, 0.9 })
        {
            await page.Page.Mouse.MoveAsync((float)X(share), (float)y, new() { Steps = 4 });
            seen.Add((await harmony.TextContentAsync())!);
        }

        await page.Page.Mouse.UpAsync();
        var after = (await harmony.TextContentAsync())!;
        Console.WriteLine($"seen while dragging: {string.Join(" | ", seen)}; let go of: {after}");
        // it changed as the bar moved, and shows the place it is let go of, its key and chord, as a playing song's tempo may
        // step on between the last move and the let-go; a playing song's may cross a change of chord there too, so it shows
        // a key and chord, where it is let go the seek bar's place below
        static string KeyAndChord(string shown) => string.Join(" · ", shown.Split(" · ").Take(2));
        await Assert.That(seen.Distinct().Count()).IsGreaterThan(1);
        if (playing)
            await Assert.That(System.Text.RegularExpressions.Regex.IsMatch(after, Shown)).IsTrue().Because(after);
        else
            await Assert.That(KeyAndChord(after)).IsEqualTo(KeyAndChord(seen[^1]));
        // and the song goes on from there, not from its start, whether or not the player had it yet
        await Task.Delay(SongPage.PastRemake);
        await Assert.That(double.Parse(await page.Page.Locator("#seek").InputValueAsync(), System.Globalization.CultureInfo.InvariantCulture)).IsGreaterThan(0.8);
        await Assert.That(page.Errors).IsEmpty();
    }
}
