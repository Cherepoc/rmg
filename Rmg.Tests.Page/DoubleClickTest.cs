using Microsoft.Playwright;

namespace Rmg.Tests.Page;

/// <summary>A slider double-clicked back to its default: a value to its middle, a pan to the middle and a volume to its loudest.</summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class DoubleClickTest(Site site)
{
    // near the slider's left end, where a click of its own would leave it, so that only the double-click brings it back
    private static readonly LocatorDblClickOptions NearItsStart = new() { Position = new Position { X = 3, Y = 5 } };

    private static async Task<SongPage> Opened(Site site)
    {
        var page = await SongPage.Open(site);
        await page.FirstSong();
        await page.Page.EvaluateAsync("() => document.getElementById('settings-panel').open = true");
        return page;
    }

    private static ILocator Setting(SongPage page, string key) => page.Page.Locator($"input[data-setting='{key}']");

    private static ILocator ValueOf(SongPage page, string key) => page.Page.Locator($".setting:has(input[data-setting='{key}']) output");

    private static ILocator PartRow(SongPage page, string name) => page.Page.Locator("#mixer tr.part").Filter(new() { Has = page.Page.GetByRole(AriaRole.Rowheader, new() { Name = name, Exact = true }) });

    [Test]
    public async Task AFacetGiven_GoesBackToItsMiddle_50Percent_AndTheSongIsAskedForWithIt()
    {
        await using var page = await Opened(site);
        await page.SetSetting("feel", 10);
        await page.Until(() => page.Requests[^1].Facet("feel") == 10, "the song asked for with the feel given");

        await Setting(page, "feel").DblClickAsync(NearItsStart);

        await Assert.That(await Setting(page, "feel").InputValueAsync()).IsEqualTo("32");
        await Assert.That(await ValueOf(page, "feel").TextContentAsync()).IsEqualTo("50%");
        await page.Until(() => page.Requests[^1].Facet("feel") == 32, "the song asked for with the feel at its middle");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task TheTempoGiven_GoesBackTo120()
    {
        await using var page = await Opened(site);
        await page.SetSetting("tempo", 5);

        await Setting(page, "tempo").DblClickAsync(NearItsStart);

        await Assert.That(await ValueOf(page, "tempo").TextContentAsync()).IsEqualTo("120 bpm");
        await page.Until(() => page.Requests[^1].Body.TryGetProperty("tempo", out var tempo) && tempo.ValueKind == System.Text.Json.JsonValueKind.Number && tempo.GetInt32() == 32,
            "the song asked for at 120 beats a minute");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task AFacetTheSongDrew_IsLeftAsItIs()
    {
        await using var page = await Opened(site);
        var drawn = await Setting(page, "form").InputValueAsync();
        var asked = page.Requests.Count;

        await Setting(page, "form").DblClickAsync(new LocatorDblClickOptions { Position = NearItsStart.Position, Force = true });

        await Assert.That(await Setting(page, "form").InputValueAsync()).IsEqualTo(drawn);
        await Assert.That(await Setting(page, "form").IsDisabledAsync()).IsTrue();
        await Assert.That(page.Requests.Count).IsEqualTo(asked);
    }

    [Test]
    public async Task APartsVolumeAndPan_GoBackTo100PercentAndTheMiddle()
    {
        await using var page = await Opened(site);
        var melody = PartRow(page, "Melody");
        await melody.Locator(".given input").CheckAsync();
        var volume = melody.Locator(".volume input");
        var pan = melody.Locator(".pan input");

        await volume.DblClickAsync(NearItsStart);
        await pan.DblClickAsync(NearItsStart);

        await Assert.That(await volume.InputValueAsync()).IsEqualTo("63");
        await Assert.That(await melody.Locator(".volume output").TextContentAsync()).IsEqualTo("100%");
        await Assert.That(await pan.InputValueAsync()).IsEqualTo("32");
        await Assert.That(await melody.Locator(".pan output").TextContentAsync()).IsEqualTo("C");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task TheSongsAndThePlaybacksVolumes_GoBackTo100Percent()
    {
        await using var page = await Opened(site);

        await page.Page.Locator("#song-volume").DblClickAsync(NearItsStart);
        await page.Page.Locator("#volume").DblClickAsync(NearItsStart);

        await Assert.That(await page.Page.Locator("#song-volume").InputValueAsync()).IsEqualTo("63");
        await Assert.That(await page.Page.Locator("#song-volume-value").TextContentAsync()).IsEqualTo("100%");
        await Assert.That(await page.Page.Locator("#volume").InputValueAsync()).IsEqualTo("1");
        await Assert.That(await page.Page.Locator("#volume-value").TextContentAsync()).IsEqualTo("100%");
        await Assert.That(page.Errors).IsEmpty();
    }
}
