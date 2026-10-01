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

    private static JsonElement Melody(SongRequest request) => request.Body.GetProperty("parts").GetProperty("melody");

    private static bool Has(JsonElement part, string property) => part.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null;

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
        await Assert.That(page.Errors).IsEmpty();
    }
}
