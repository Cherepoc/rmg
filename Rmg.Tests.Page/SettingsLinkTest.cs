using System.Text.Json;

namespace Rmg.Tests.Page;

/// <summary>A link's settings, as the server writes them (SongSettingsTest's), read by the page as the server reads them.</summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class SettingsLinkTest(Site site)
{
    // settings with every field set apart from its neighbours, as the server writes them now and wrote them before the
    // solo part was named and its tempo a step (Rmg.Tests' SongSettingsTest.MixedText and MixedTextTwo)
    private const string MixedText = "4_07ELSZgnu3B2o_0-9zIyRxawjvsu_eo309IRajs002I0a2s183Q1i3-31hQPh8f8ph2";
    private const string MixedTextTwo = "2_07ELSZgnu3B2o_0-9zIyRxawjvsu_309IRajs002I0a2s183Q1i3-hQPh8f8hA";

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
        // and the solo part's, given in on its instrument
        await Assert.That(Part(asked, "solo").GetProperty("instrument").GetInt32()).IsEqualTo(65);
        await Assert.That(Part(asked, "solo").GetProperty("plays").GetBoolean()).IsTrue();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task ALinkOfAFormatBefore_IsReadAsItWas_ItsTempoAndSoloPartAsTheSongDrawsThem_AndWrittenInTheFormatNow()
    {
        await using var page = await SongPage.Open(site, $"?song=1&settings={MixedTextTwo}");
        await page.FirstSong();
        var asked = page.Requests[0];

        await Assert.That(asked.Facet("fills")).IsEqualTo(14);
        await Assert.That(Part(asked, "chords").GetProperty("instrument").GetInt32()).IsEqualTo(18);
        await Assert.That(Part(asked, "solo").TryGetProperty("plays", out _)).IsFalse();
        await Assert.That(asked.Body.TryGetProperty("tempo", out var tempo) && tempo.ValueKind != JsonValueKind.Null).IsFalse();
        // the address now in the format the page writes
        var settings = new Uri(page.Page.Url).Query.Split('&').Single(x => x.TrimStart('?').StartsWith("settings=")).Split('=')[1];
        await Assert.That(settings[0]).IsEqualTo('4');
        await Assert.That(settings.Length).IsEqualTo(MixedText.Length);
        await Assert.That(page.Errors).IsEmpty();
    }
}
