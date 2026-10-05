using System.Text.Json;

namespace Rmg.Tests.Page;

/// <summary>
///     A part's row given as one: whether it plays, its instrument and its pan given as the song drew them, then set; and
///     let go of, the song's own again.
/// </summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class GivingAPartTest(Site site)
{
    private const string Row = "#mixer tr:first-child";

    // the drums' row, and their pieces' switches in the rows of their groups under it
    private const string Drums = "#mixer tr:has(+ tr.drum-group)";
    private const string Chips = "#mixer tr.drum-group .chip";

    private static JsonElement Melody(SongRequest request) => request.Body.GetProperty("parts").GetProperty("melody");

    private static bool Has(JsonElement part, string property) => part.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null;

    [Test]
    public async Task ASettingChanged_MakesTheSongAgain_WithoutMovingWhatIsOnTheScreen()
    {
        await using var page = await SongPage.Open(site, phone: true);
        await page.FirstSong();
        await page.Generate();
        await page.Page.EvaluateAsync("() => document.getElementById('settings-panel').open = true");
        var slider = page.Page.Locator("input[data-setting='fills']");
        await slider.ScrollIntoViewIfNeededAsync();
        var before = (await slider.BoundingBoxAsync())!.Y;

        var asked = page.Requests.Count;
        await page.SetSetting("fills", 10);
        await page.Until(() => page.Requests.Count > asked && page.Requests[^1].Ended == "200", "the song made again");
        await page.Until(async () => await page.IsGenerateEnabled(), "the song in place");

        // the slider where it was on the screen, rather than the page scrolled up to the songs so far
        await Assert.That(await page.Page.EvaluateAsync<double>("() => scrollY")).IsGreaterThan(300);
        await Assert.That(Math.Abs((await slider.BoundingBoxAsync())!.Y - before)).IsLessThan(1);
    }

    [Test]
    public async Task EverySwitchThatGivesAValueOrAPart_SaysWhatItDoes()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();

        var titles = await page.Page.EvaluateAsync<string[]>("() => [...document.querySelectorAll('.setting > input[type=checkbox], #mixer .given input')].map(x => x.title)");
        // every song setting's and every part's
        await Assert.That(titles.Length).IsGreaterThan(20);
        await Assert.That(titles.Distinct()).IsEquivalentTo(["Check to override, uncheck to randomize"]);
    }

    /// <summary>Clicks a control of the melody's row, wherever its panel is folded away, and waits for the song made again.</summary>
    private static async Task<SongRequest> Click(SongPage page, string control)
    {
        var asked = page.Requests.Count;
        await page.Page.EvaluateAsync("selector => document.querySelector(selector).click()", $"{Row} {control}");
        await page.Until(() => page.Requests.Count > asked && page.Requests[^1].Ended == "200", "the song made again");
        return page.Requests[^1];
    }

    private static Task<bool> IsDisabled(SongPage page, string control) =>
        page.Page.EvaluateAsync<bool>("selector => document.querySelector(selector).disabled", $"{Row} {control}");

    [Test]
    public async Task TheDrumsGiven_AreAskedForWithTheirPieces_APieceAddedOrTakenOut()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        await page.Until(() => page.Page.Locator(Chips).CountAsync().ContinueWith(x => x.Result > 0), "the drum pieces listed");
        await Assert.That(await page.Page.EvaluateAsync<bool>("selector => document.querySelector(selector).disabled", Chips)).IsTrue();

        var asked = page.Requests.Count;
        await page.Page.EvaluateAsync("selector => document.querySelector(selector).click()", $"{Drums} td:first-child input");
        await page.Until(() => page.Requests.Count > asked && page.Requests[^1].Ended == "200", "the song made again");
        var given = page.Requests[^1].Body.GetProperty("drumPieces").EnumerateArray().Select(x => x.GetString()!).ToArray();
        await Assert.That(given).IsNotEmpty();

        // a piece the song does not play put in
        var outside = await page.Page.EvaluateAsync<string>("selector => [...document.querySelectorAll(selector)].find(x => x.getAttribute('aria-pressed') === 'false').dataset.piece", Chips);
        asked = page.Requests.Count;
        await page.Page.EvaluateAsync("([selector, piece]) => document.querySelector(`${selector}[data-piece='${piece}']`).click()", new object[] { Chips, outside });
        await page.Until(() => page.Requests.Count > asked && page.Requests[^1].Ended == "200", "the song made again");
        await Assert.That(page.Requests[^1].Body.GetProperty("drumPieces").EnumerateArray().Select(x => x.GetString())).Contains(outside);
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task MuteAndSolo_ArePressedHere_AndAskTheServerForNothing()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        await page.Settled();
        var asked = page.Requests.Count;

        foreach (var control in new[] { $"{Row} button.listen", "#mixer tr.drum-group button.listen[aria-label^='Solo']" })
        {
            await page.Page.EvaluateAsync("selector => document.querySelector(selector).click()", control);
            await Assert.That(await page.Page.EvaluateAsync<string>("selector => document.querySelector(selector).getAttribute('aria-pressed')", control)).IsEqualTo("true");
            await page.Page.EvaluateAsync("selector => document.querySelector(selector).click()", control);
            await Assert.That(await page.Page.EvaluateAsync<string>("selector => document.querySelector(selector).getAttribute('aria-pressed')", control)).IsEqualTo("false");
        }

        await Task.Delay(800);
        await Assert.That(page.Requests.Count).IsEqualTo(asked);
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task APartGiven_IsAskedForAsDrawn_ThenAsSet_AndLetGoOf_AsTheSongs()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        await Assert.That(await IsDisabled(page, "select")).IsTrue();

        var given = Melody(await Click(page, "td:first-child input"));
        await Assert.That(Has(given, "plays") && Has(given, "instrument") && Has(given, "pan")).IsTrue();
        await Assert.That(await IsDisabled(page, "select")).IsFalse();

        // the part switched to the other of in and out
        var switched = Melody(await Click(page, "td:nth-child(3) input"));
        await Assert.That(switched.GetProperty("plays").GetBoolean()).IsEqualTo(!given.GetProperty("plays").GetBoolean());

        var released = Melody(await Click(page, "td:first-child input"));
        await Assert.That(Has(released, "plays") || Has(released, "instrument") || Has(released, "pan")).IsFalse();
        await Assert.That(await IsDisabled(page, "select")).IsTrue();
        // and its volume the song's own, the fullest, not to be moved
        await Assert.That(await IsDisabled(page, "input.fader")).IsTrue();
        await Assert.That(released.GetProperty("volume").GetInt32()).IsEqualTo(61);
        await Assert.That(page.Errors).IsEmpty();
    }
}
