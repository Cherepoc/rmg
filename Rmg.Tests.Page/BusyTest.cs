using System.Diagnostics;

namespace Rmg.Tests.Page;

/// <summary>
///     A song the server is too busy for is asked for again, after as long as the server says and a little more at
///     random, a few times, and given up as the songs on their way are.
/// </summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class BusyTest(Site site)
{
    private static double Seconds(long from, long to) => Stopwatch.GetElapsedTime(from, to).TotalSeconds;

    [Test]
    public async Task ASongTurnedAwayTwice_IsAskedForAgain_AndArrives()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        var before = await page.Seed();
        var asked = page.Requests.Count;

        page.Busy = 2;
        await page.Page.Locator("#generate").ClickAsync();
        await page.Until(async () => (await page.Status()).Contains("Trying again"), "the page saying it tries again");
        await page.Until(async () => await page.Seed() != before && await page.IsGenerateEnabled(), "the song arriving", 15);

        var since = page.Since(asked);
        await Assert.That(since.Select(x => x.Ended)).IsEquivalentTo(new[] { "503", "503", "200" }, CollectionOrdering.Matching);

        // after the Retry-After of 1 s, and up to half as long again
        foreach (var (earlier, later) in since.Zip(since.Skip(1)))
            await Assert.That(Seconds(earlier.SentAt, later.SentAt)).IsBetween(0.95, 1.75);
    }

    [Test]
    public async Task ASongTurnedAwayEveryTime_FailsOnce_AfterThreeMoreAsks()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        var before = await page.Seed();
        var asked = page.Requests.Count;

        page.Busy = 10;
        await page.Page.Locator("#generate").ClickAsync();
        await page.Until(async () => (await page.Status()).Contains("Could not generate"), "the page giving up", 15);
        // as long as a fifth ask would come after the fourth at the latest, the Retry-After and half as long again
        await Task.Delay(1750);

        await Assert.That(page.Since(asked).Select(x => x.Ended)).IsEquivalentTo(new[] { "503", "503", "503", "503" }, CollectionOrdering.Matching);
        await Assert.That(await page.Status()).Contains("busy");
        await Assert.That(await page.Seed()).IsEqualTo(before);
        await Assert.That(await page.IsGenerateEnabled()).IsTrue();
    }

    [Test]
    public async Task ANewerChange_WhileARemakeWaitsToAskAgain_TakesItsPlace()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        var asked = page.Requests.Count;

        page.Busy = 1;
        await page.SetSetting("feel", 10);
        await page.Until(async () => (await page.Status()).Contains("Trying again"), "the remake waiting to ask again");
        await page.SetSetting("feel", 20);
        await page.Until(() => page.Requests.Count == asked + 2, "the newer change asked for");
        await page.Settled();
        await Task.Delay(2000);

        var since = page.Since(asked);
        await Assert.That(since.Count).IsEqualTo(2);
        await Assert.That(since[0].Facet("feel")).IsEqualTo(10);
        await Assert.That(since[1].Facet("feel")).IsEqualTo(20);
        await Assert.That(since[1].Ended).IsEqualTo("200");
    }
}
