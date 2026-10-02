using System.Text.Json;

namespace Rmg.Tests.Page;

/// <summary>A link's settings, as the server writes them (SongSettingsTest's), read by the page as the server reads them.</summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class SettingsLinkTest(Site site)
{
    // settings with every field set apart from its neighbours, as the server writes them (Rmg.Tests'
    // SongSettingsTest.MixedText), and as the oldest format wrote them, which is read no more
    private const string MixedText = "6_07ELSZgnu3o_0-9zIyRxawjvsu_eo09IRajshQNX0aI4Iej0fg5Od-xWJIII";
    private const string MixedTextTwo = "2_07ELSZgnu3B2o_0-9zIyRxawjvsu_309IRajs002I0a2s183Q1i3-hQPh8f8hA";

    // what of them names the song, as the server works it out (SongSettingsTest.MixedIdentity)
    private const string MixedIdentity = "hQ1_0ESgu3201201201119999";

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
        // every third drum piece, from the first, in the server's order, which the page keeps the same
        await Assert.That(asked.Body.GetProperty("drumPieces").EnumerateArray().Select(x => x.GetString())).IsEquivalentTo(
            ["kick", "clap", "tambourine", "tom", "bongo", "agogo", "woodBlock", "cuica"]);
        // the counter-melody left out of the file, which the page no longer does, every part in it
        await Assert.That(Part(asked, "counterMelody").TryGetProperty("isOn", out _)).IsFalse();
        // and the solo part's, given in on its instrument
        await Assert.That(Part(asked, "solo").GetProperty("instrument").GetInt32()).IsEqualTo(65);
        await Assert.That(Part(asked, "solo").GetProperty("plays").GetBoolean()).IsTrue();
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
    public async Task ALinkOfAFormatBefore_PlaysItsSong_WithTheSettingsThePageHad_AndSaysSo()
    {
        await using var page = await SongPage.Open(site, $"?song=1&settings={MixedTextTwo}");
        await page.FirstSong();
        var asked = page.Requests[0];

        // asked for with nothing given, as a page opened for the first time has
        await Assert.That(asked.Seed).IsEqualTo("1");
        await Assert.That(asked.Facet("fills")).IsNull();
        await page.Until(async () => (await page.Status()).Contains("settings could not be read"), "the page saying so");
        await Assert.That(page.Errors).IsEmpty();
    }
}
