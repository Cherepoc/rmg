using System.Text.Json;

namespace Rmg.Tests.Page;

/// <summary>A link's settings, as the server writes them (SongSettingsTest's), read by the page as the server reads them.</summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class SettingsLinkTest(Site site)
{
    // settings with every field set apart from its neighbours, as the server writes them (Rmg.Tests'
    // SongSettingsTest.MixedText)
    private const string MixedText = "Bz07ELSZgnu3jKoz0y8xGwOvWuetmsueo09IRajsBLDN261GK42IGM5GKAB0RPVR1EI49I1";

    // what of them names the song, as the server works it out (SongSettingsTest.MixedIdentity)
    private const string MixedIdentity = "BLD1z0ESgu32CK01201201119I496";

    private static JsonElement Part(SongRequest request, string part) => request.Body.GetProperty("parts").GetProperty(part);

    [Test]
    public async Task ALinksSettings_AreAskedForAsTheServerWroteThem_TheSoloPartAmongThem()
    {
        await using var page = await SongPage.Open(site, $"?song=1&settings={MixedText}");
        await page.Until(() => Task.FromResult(page.Requests.Count > 0), "the linked song asked for");
        var asked = page.Requests[0];

        // the facets given, every other from the first, and a part's instrument given, every other from the second
        await Assert.That(asked.Facet("feel")).IsEqualTo(0);
        await Assert.That(asked.Facet("groove")).IsNull();
        await Assert.That(asked.Facet("fills")).IsEqualTo(14);
        await Assert.That(Part(asked, "chords").GetProperty("instrument").GetInt32()).IsEqualTo(18);
        await Assert.That(asked.Body.GetProperty("tempo").GetInt32()).IsEqualTo(3);
        // a link carries no genre
        await Assert.That(asked.Body.TryGetProperty("genre", out _)).IsFalse();
        // the mode given, the last scale, and the swing drawn
        await Assert.That(asked.Body.GetProperty("mode").GetInt32()).IsEqualTo(12);
        await Assert.That(asked.Body.TryGetProperty("swing", out _)).IsFalse();
        // every third drum piece, from the first, and the last, in the server's order, which the page keeps the same
        await Assert.That(asked.Body.GetProperty("drumPieces").EnumerateArray().Select(x => x.GetString())).IsEquivalentTo(
            ["kick", "clap", "tambourine", "tom", "bongo", "agogo", "woodBlock", "cuica", "whistle"], CollectionOrdering.Matching);
        // the counter-melody left out of the file, which the page no longer does, every part in it
        await Assert.That(Part(asked, "counterMelody").TryGetProperty("isOn", out _)).IsFalse();
        // and the solo part's, given in on its instrument
        await Assert.That(Part(asked, "solo").GetProperty("instrument").GetInt32()).IsEqualTo(65);
        await Assert.That(Part(asked, "solo").GetProperty("plays").GetBoolean()).IsTrue();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task ALinkMade_IsLettersAndDigitsAlone_ForAChatToLeaveAsTheyAre_AndIsReadAsItWas()
    {
        await using var page = await SongPage.Open(site, $"?song=1&settings={MixedText}");
        await page.FirstSong();
        await page.Settled();

        // the address, as Share hands it on: no "_" or "-", which a chat's markdown may take for its own
        var link = await page.Page.EvaluateAsync<string>("() => location.href");
        await Assert.That(link[link.IndexOf('?')..]).Matches("^\\?[A-Za-z0-9=&]+$");

        // opened elsewhere, the song asked for as it was, with nothing said of its settings
        await using var other = await SongPage.Open(site, link[link.IndexOf('?')..]);
        await other.FirstSong();
        await other.Settled();
        await Assert.That(other.Requests[0].Body.GetRawText()).IsEqualTo(page.Requests[0].Body.GetRawText());
        await Assert.That(await other.Status()).DoesNotContain("could not be read");
        await Assert.That(page.Errors).IsEmpty();
        await Assert.That(other.Errors).IsEmpty();
    }

    [Test]
    public async Task ALinkOfAnOlderFormat_PlaysItsOwnSong_ItsSeedInTheDigitsOfThen()
    {
        // a seed of 127, as the 64 digits wrote it then, "1_", and as the 62 write it now, "23"
        await using var page = await SongPage.Open(site, "?song=1_&settings=6VXcaCWOe6YTT_j_W_l_d_W_0_I_Y_10I0I2mWHGM8700y0QWWgv08");
        await page.FirstSong();
        await page.Settled();

        await Assert.That(page.Requests[0].Body.GetProperty("seed").GetString()).IsEqualTo("23");
        await Assert.That(await page.Status()).Contains("could not be read");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task AModeChosen_AndASwingSet_AreAskedFor_AndKeptInTheLink()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        await page.FirstSong();

        // the scales listed as the server lists them, the song's own shown until one is chosen
        var mode = page.Page.Locator("select[aria-label='Mode']");
        await page.Until(async () => await mode.Locator("option").CountAsync() == 13, "the scales listed");
        // as a hand would, wherever the settings' panel is folded away: given, then chosen
        await page.Page.EvaluateAsync("() => { const given = document.querySelector(\"input[aria-label='Give the mode']\"); given.checked = true; given.dispatchEvent(new Event('change')); }");
        await page.Settled();
        var count = page.Requests.Count;
        await mode.EvaluateAsync("select => { select.value = '2'; select.dispatchEvent(new Event('change')); }");
        await page.Until(() => page.Requests.Count > count, "the song asked for in Dorian");
        await Assert.That(page.Requests[^1].Body.GetProperty("mode").GetInt32()).IsEqualTo(2);
        await page.Settled();

        count = page.Requests.Count;
        await page.SetSetting("swing", 40);
        await page.Until(() => page.Requests.Count > count, "the song asked for swung");
        await page.Until(() => page.Requests[^1].Body.TryGetProperty("swing", out _), "the swing given");
        await Assert.That(page.Requests[^1].Body.GetProperty("swing").GetInt32()).IsEqualTo(40);
        await Assert.That(page.Requests[^1].Body.GetProperty("mode").GetInt32()).IsEqualTo(2);
        await page.Settled();
        await Assert.That(await page.Given()).IsEqualTo("2 given");

        // and the link, opened where nothing is kept, asks for both
        await page.Until(() => Task.FromResult(page.Page.Url.Contains("settings=")), "the link written");
        var link = page.Page.Url;
        await page.Page.EvaluateAsync("() => localStorage.clear()");
        count = page.Requests.Count;
        await page.Page.GotoAsync(link);
        await page.Until(() => page.Requests.Count > count, "the linked song asked for");
        await Assert.That(page.Requests[^1].Body.GetProperty("mode").GetInt32()).IsEqualTo(2);
        await Assert.That(page.Requests[^1].Body.GetProperty("swing").GetInt32()).IsEqualTo(40);
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task ASongWithTheLinksSettings_IsNamedAsTheServerNamesIt()
    {
        await using var page = await SongPage.Open(site, $"?song=1&settings={MixedText}");
        await page.FirstSong();

        await Assert.That(await page.HistoryItem("1").GetAttributeAsync("data-song")).IsEqualTo($"1.{MixedIdentity}");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task ALinkOfAnotherFormat_PlaysItsSong_WithTheSettingsThePageHad_AndSaysSo()
    {
        await using var page = await SongPage.Open(site, $"?song=1&settings=5{MixedText[1..]}");
        await page.FirstSong();
        var asked = page.Requests[0];

        // asked for with nothing given, as a page opened for the first time has
        await Assert.That(asked.Seed).IsEqualTo("1");
        await Assert.That(asked.Facet("fills")).IsNull();
        await page.Until(async () => (await page.Status()).Contains("settings could not be read"), "the page saying so");
        await Assert.That(page.Errors).IsEmpty();
    }
}
