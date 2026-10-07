using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Rmg.Tests.Page;

/// <summary>The listening mode (<c>?listen=</c> a question): a song heard with a device and without, and a verdict on it kept.</summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class ListeningTest(Site site)
{
    [Test]
    public async Task AQuestion_PlaysASongWithAndWithout_AndKeepsTheVerdict()
    {
        await using var page = await SongPage.Open(site, "?listen=pushes");
        var strip = page.Page.Locator("section.listening");
        await page.Until(() => strip.IsVisibleAsync(), "the question asked");
        await Assert.That(await strip.Locator(".listening-question").TextContentAsync()).Contains("Pushes");
        await Assert.That(await strip.Locator(".listening-where").TextContentAsync()).IsEqualTo("Song 1 of 6");
        // no song rolled as the page opens, the question asking for its own; a tempo given here none of them
        await Task.Delay(500);
        await Assert.That(page.Requests).IsEmpty();
        await page.SetSetting("tempo", 40);

        // the first song's A with the pushes, its B without them, both in Rock
        var count = page.Requests.Count;
        await strip.GetByRole(AriaRole.Button, new() { Name = "Play A" }).ClickAsync();
        await page.Until(() => page.Requests.Count > count, "A asked for");
        var with = page.Requests[^1].Body;
        await Assert.That(with.TryGetProperty("skip", out _)).IsFalse();
        await Assert.That(with.TryGetProperty("tempo", out var tempo) && tempo.ValueKind != JsonValueKind.Null).IsFalse();
        await Assert.That(with.GetProperty("genreName").GetString()).IsEqualTo("Rock");
        await page.Settled();

        count = page.Requests.Count;
        await strip.GetByRole(AriaRole.Button, new() { Name = "Play B" }).ClickAsync();
        await page.Until(() => page.Requests.Count > count, "B asked for");
        var without = page.Requests[^1].Body;
        await Assert.That(without.GetProperty("skip").EnumerateArray().Select(x => x.GetString())).IsEquivalentTo(["Pushes"]);
        await Assert.That(without.GetProperty("seed").GetString()).IsEqualTo(with.GetProperty("seed").GetString());
        await page.Settled();
        await Assert.That(page.Requests.All(x => x.Ended == "200")).IsTrue();

        // A better on the first song, the one with: kept as such, and on to the next song, whose A is the one without
        await strip.GetByRole(AriaRole.Button, new() { Name = "A is better" }).ClickAsync();
        await Assert.That(await strip.Locator(".listening-where").TextContentAsync()).IsEqualTo("Song 2 of 6");
        count = page.Requests.Count;
        await strip.GetByRole(AriaRole.Button, new() { Name = "Play A" }).ClickAsync();
        await page.Until(() => page.Requests.Count > count, "the next A asked for");
        await Assert.That(page.Requests[^1].Body.TryGetProperty("skip", out _)).IsTrue();

        using var client = new HttpClient { BaseAddress = site.Address };
        client.DefaultRequestHeaders.Authorization = new("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"admin:{Site.Token}")));
        JsonElement pushes = default;
        await page.Until(async () =>
        {
            var summary = await client.GetFromJsonAsync<JsonElement>("admin/api/summary");
            pushes = summary.GetProperty("verdicts").EnumerateArray().FirstOrDefault(x => x.GetProperty("question").GetString() == "pushes");
            return pushes.ValueKind == JsonValueKind.Object;
        }, "the verdict kept");
        await Assert.That(pushes.GetProperty("with").GetInt32()).IsGreaterThanOrEqualTo(1);
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task NoQuestion_ListsEveryQuestion_WithItsLink_AndAsksForNoSong()
    {
        await using var page = await SongPage.Open(site, "?listen");
        var index = page.Page.Locator("section.listening-index");
        await page.Until(() => index.IsVisibleAsync(), "the questions listed");
        var questions = index.Locator("li[data-question]");
        await Assert.That(await questions.CountAsync()).IsGreaterThanOrEqualTo(10);
        var pushes = index.Locator("li[data-question='pushes']");
        await Assert.That(await pushes.Locator("a").GetAttributeAsync("href")).IsEqualTo("?listen=pushes");
        await Assert.That(await pushes.Locator(".listening-where").TextContentAsync()).StartsWith("Rock · ");
        await Task.Delay(500);
        await Assert.That(page.Requests).IsEmpty();

        // a link asks its question
        await pushes.Locator("a").ClickAsync();
        await page.Until(() => page.Page.Locator("section.listening:not(.listening-index)").IsVisibleAsync(), "the question asked");
        await Assert.That(page.Errors).IsEmpty();
    }
}
