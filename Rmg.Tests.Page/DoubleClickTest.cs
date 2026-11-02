using Microsoft.Playwright;

namespace Rmg.Tests.Page;

/// <summary>A slider double-clicked back to its default: a value to its middle, a pan to the middle and a volume to 100%.</summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class DoubleClickTest(Site site)
{
    // near the slider's left end, where a click of its own would leave it, so that only the double-click brings it back
    private static readonly LocatorDblClickOptions NearItsStart = new() { Position = new Position { X = 3, Y = 5 } };

    private static async Task<SongPage> Opened(Site site)
    {
        var page = await SongPage.Open(site);
        await page.FirstSong();
        await page.Page.EvaluateAsync("() => { document.getElementById('composition-panel').open = true; document.getElementById('parts-panel').open = true; }");
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

        await Assert.That(await Setting(page, "feel").InputValueAsync()).IsEqualTo("31");
        await Assert.That(await ValueOf(page, "feel").TextContentAsync()).IsEqualTo("50%");
        await page.Until(() => page.Requests[^1].Facet("feel") == 31, "the song asked for with the feel at its middle");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task TheTempoGiven_GoesBackTo120()
    {
        await using var page = await Opened(site);
        await page.SetSetting("tempo", 5);

        await Setting(page, "tempo").DblClickAsync(NearItsStart);

        await Assert.That(await ValueOf(page, "tempo").TextContentAsync()).IsEqualTo("120 bpm");
        await page.Until(() => page.Requests[^1].Body.TryGetProperty("tempo", out var tempo) && tempo.ValueKind == System.Text.Json.JsonValueKind.Number && tempo.GetInt32() == 31,
            "the song asked for at 120 beats a minute");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task OnAPhone_ADoubleTap_PutsAFacetBackToItsMiddle_AndOneTapDoesNot()
    {
        await using var page = await SongPage.Open(site, phone: true);
        await page.FirstSong();
        await page.Page.EvaluateAsync("() => { document.getElementById('composition-panel').open = true; document.getElementById('parts-panel').open = true; }");
        await page.SetSetting("feel", 10);
        var slider = Setting(page, "feel");

        // where the slider is once the song the change asks for is here, as the page's lines below the song can move it
        async Task<(float X, float Y)> Place()
        {
            await page.Settled();
            await slider.ScrollIntoViewIfNeededAsync();
            var box = (await slider.BoundingBoxAsync())!;
            return (box.X + 4, box.Y + box.Height / 2);
        }

        var (x, y) = await Place();
        await page.Page.Touchscreen.TapAsync(x, y);
        await page.Page.WaitForTimeoutAsync(500);
        await Assert.That(await slider.InputValueAsync()).IsNotEqualTo("31");

        // two taps soon after each other, again where a busy machine let more than a double-tap's time pass between them
        for (var attempt = 0; attempt < 5 && await slider.InputValueAsync() != "31"; attempt++)
        {
            (x, y) = await Place();
            await page.Page.Touchscreen.TapAsync(x, y);
            await page.Page.Touchscreen.TapAsync(x, y);
            await page.Page.WaitForTimeoutAsync(400);
        }

        await page.Until(async () => await slider.InputValueAsync() == "31", "the feel back to its middle");
        await Assert.That(await ValueOf(page, "feel").TextContentAsync()).IsEqualTo("50%");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task AFacetTheSongDrew_IsLeftAsItIs()
    {
        await using var page = await Opened(site);
        var drawn = await Setting(page, "form").InputValueAsync();
        var asked = page.Requests.Count;

        // dispatched to the disabled slider, as a browser may deliver no double click to a disabled control at all, so that
        // the page's own guard is what keeps it
        await Setting(page, "form").DispatchEventAsync("dblclick");

        await Assert.That(await Setting(page, "form").InputValueAsync()).IsEqualTo(drawn);
        await Assert.That(await Setting(page, "form").IsDisabledAsync()).IsTrue();
        // past the page's wait before it remakes a song, which would come after it
        await Task.Delay(SongPage.PastRemake);
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

        await Assert.That(await volume.InputValueAsync()).IsEqualTo("61");
        await Assert.That(await melody.Locator(".volume output").TextContentAsync()).IsEqualTo("100%");
        await Assert.That(await pan.InputValueAsync()).IsEqualTo("31");
        await Assert.That(await melody.Locator(".pan output").TextContentAsync()).IsEqualTo("C");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task TheSongsAndThePlaybacksVolumes_GoBackTo100Percent()
    {
        await using var page = await Opened(site);

        await page.Page.Locator("#song-volume").DblClickAsync(NearItsStart);
        await page.Page.Locator("#volume").DblClickAsync(NearItsStart);

        await Assert.That(await page.Page.Locator("#song-volume").InputValueAsync()).IsEqualTo("61");
        await Assert.That(await page.Page.Locator("#song-volume-value").TextContentAsync()).IsEqualTo("100%");
        await Assert.That(await page.Page.Locator("#volume").InputValueAsync()).IsEqualTo("1");
        await Assert.That(await page.Page.Locator("#volume-value").TextContentAsync()).IsEqualTo("100%");
        await Assert.That(page.Errors).IsEmpty();
    }
}
