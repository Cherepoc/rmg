namespace Rmg.Tests.Page;

/// <summary>
///     The latest ask for a song is the one that plays: a seed asked for lets go of a song on its way, the same seed asked
///     for again does not, Stop lets go of it and keeps the page as it was, and a change of the settings is asked for once
///     the settings have stopped changing, of the song that is on the page by then.
/// </summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class AskingTest(Site site)
{
    private static readonly TimeSpan Held = TimeSpan.FromMilliseconds(1500);

    /// <summary>A page with two songs on its list, the second on the page.</summary>
    private async Task<SongPage> WithTwoSongs()
    {
        var page = await SongPage.Open(site);
        await page.FirstSong();
        await page.Generate();
        return page;
    }

    [Test]
    public async Task ASongOnItsWay_NewOrMadeAgain_IsShownUnderTheSeed_UntilItIsHere()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        var making = page.Page.Locator(".now-playing [role=progressbar]");
        await Assert.That(await making.CountAsync()).IsEqualTo(0);

        page.Slow = Held;
        await page.Page.Locator("#generate").ClickAsync();
        await page.OnItsWay();
        await Assert.That(await making.GetAttributeAsync("aria-label")).IsEqualTo("Making the song");
        await page.Settled();
        await page.Until(async () => await making.CountAsync() == 0, "the bar gone with the song here");

        // and a song made again for a setting changed, which says nothing else of it
        await page.SetSetting("fills", 10);
        await page.OnItsWay();
        await Assert.That(await making.IsVisibleAsync()).IsTrue();
        await page.Settled();
        await page.Until(async () => await making.CountAsync() == 0, "the bar gone with the song made again");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task ASeedFromTheList_WhileASongIsOnItsWay_TakesItsPlace()
    {
        await using var page = await WithTwoSongs();
        var current = await page.Seed();
        var picked = (await page.History()).First(x => x != current);
        var asked = page.Requests.Count;

        page.Slow = Held;
        await page.Page.Locator("#generate").ClickAsync();
        await page.OnItsWay();
        await page.HistoryItem(picked).ClickAsync();
        await page.Settled();

        var since = page.Since(asked);
        await Assert.That(since.Count).IsEqualTo(2);
        await Assert.That(since[0].Ended).IsEqualTo("aborted");
        await Assert.That(since[1].Seed).IsEqualTo(picked);
        await Assert.That(since[1].Ended).IsEqualTo("200");
        await page.Until(async () => await page.Seed() == picked && await page.IsGenerateEnabled(), "the picked song on the page");
    }

    [Test]
    public async Task Stop_WhileASongIsOnItsWay_LetsGoOfIt_AndThePageKeepsItsSong()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        await page.Playable();
        var before = await page.Seed();
        var asked = page.Requests.Count;

        page.Slow = Held;
        await page.Page.Locator("#generate").ClickAsync();
        await page.OnItsWay();
        await page.Page.Locator("#stop").ClickAsync();
        await page.Settled();

        await Assert.That(page.Since(asked).Single().Ended).IsEqualTo("aborted");
        await Assert.That(await page.Seed()).IsEqualTo(before);
        await Assert.That(await page.Status()).IsEqualTo("");
        await Assert.That(await page.IsGenerateEnabled()).IsTrue();
    }

    [Test]
    public async Task Stop_AfterASeedFromTheList_PutsBackTheSettingsOfTheSongOnThePage()
    {
        // the first song's entry has every setting drawn, and the second, the one on the page, has its feel given
        await using var page = await WithTwoSongs();
        var current = await page.Seed();
        var first = (await page.History()).First(x => x != current);
        await page.Playable();
        await page.SetSetting("feel", 10);
        await page.Until(async () => page.Requests.Count == 3 && page.Requests[^1].Ended == "200", "the song remade with its feel given");
        await Assert.That(await page.Given()).IsEqualTo("1 given");

        page.Slow = Held;
        await page.HistoryItem(first).ClickAsync();
        await page.OnItsWay();
        var during = await page.Given();
        await page.Page.Locator("#stop").ClickAsync();
        await page.Settled();

        await Assert.That(during).IsEqualTo("All drawn");
        await Assert.That(await page.Given()).IsEqualTo("1 given");
    }

    [Test]
    public async Task TheSameSeedAskedForTwice_WhileItIsOnItsWay_IsAskedForOnce()
    {
        await using var page = await WithTwoSongs();
        var current = await page.Seed();
        var other = (await page.History()).First(x => x != current);
        var asked = page.Requests.Count;

        page.Slow = Held;
        await page.HistoryItem(other).ClickAsync();
        await page.OnItsWay();
        await page.HistoryItem(other).ClickAsync();
        await page.Settled();

        var since = page.Since(asked);
        await Assert.That(since.Count).IsEqualTo(1);
        await Assert.That(since[0].Ended).IsEqualTo("200");
    }

    [Test]
    public async Task SettingsChangedOneAfterAnother_AreAskedForOnce_AsTheyStand()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        var asked = page.Requests.Count;

        // one after another within the page's wait before it remakes, however slow the test's round trips
        await page.Page.EvaluateAsync("""
            () => {
                const range = document.querySelector("input[data-setting='feel']");
                for (const value of [10, 20, 30]) {
                    range.value = String(value);
                    range.dispatchEvent(new Event("input"));
                    range.dispatchEvent(new Event("change"));
                }
            }
            """);

        await page.Until(() => page.Requests.Count > asked, "the song asked for again");
        await page.Settled();
        await Task.Delay(SongPage.PastRemake);

        var remade = page.Since(asked).Single();
        await Assert.That(remade.Seed).IsEqualTo(await page.Seed());
        await Assert.That(remade.Facet("feel")).IsEqualTo(30);
    }

    [Test]
    public async Task ASettingChanged_WhileANewSongIsOnItsWay_RemakesTheNewSong()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        var old = await page.Seed();
        var asked = page.Requests.Count;

        page.Slow = Held;
        await page.Page.Locator("#generate").ClickAsync();
        await page.OnItsWay();
        await page.SetSetting("feel", 10);
        await page.Until(() => page.Requests.Count == asked + 2, "the new song remade", 15);
        await page.Settled();

        var since = page.Since(asked);
        var arrived = await page.Seed();
        await Assert.That(arrived).IsNotEqualTo(old);
        await Assert.That(since.Any(x => x.Seed == old)).IsFalse();
        await Assert.That(since[1].Seed).IsEqualTo(arrived);
        await Assert.That(since[1].Facet("feel")).IsEqualTo(10);
    }

    [Test]
    public async Task TheMixChanged_WhileARemakeIsOnItsWay_IsFetchedForTheDownloadOnceTheRemakeIsHere()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        var asked = page.Requests.Count;

        page.Slow = Held;
        await page.SetSetting("feel", 10);
        await page.OnItsWay();
        await page.SetSongVolume(30);
        await page.Until(() => page.Requests.Count == asked + 2, "the download fetched with the mix", 15);
        await page.Settled();

        var (remake, download) = (page.Since(asked)[0], page.Since(asked)[1]);
        await Assert.That(remake.Ended).IsEqualTo("200");
        await Assert.That(download.Volume).IsEqualTo(30);
        await Assert.That(download.SentAt).IsGreaterThanOrEqualTo(remake.EndedAt!.Value);
    }
}
