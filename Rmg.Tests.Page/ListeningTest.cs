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
        // where to listen, the time its device first plays
        await Assert.That(await strip.Locator(".listening-where").TextContentAsync()).StartsWith("Song 1 of 6 · listen at ");
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
        // the song started just before where to listen rather than at its start
        var listenAt = (await strip.Locator(".listening-where").TextContentAsync())!.Split("listen at ")[1].Split(",")[0].Split(":").Select(int.Parse).ToArray();
        var seconds = listenAt[0] * 60 + listenAt[1];
        await page.Until(async () => (await page.Page.Locator("#elapsed").TextContentAsync())!.Split(":").Select(int.Parse).ToArray() is var shown
                                     && shown[0] * 60 + shown[1] >= Math.Max(0, seconds - 3), "the song started where to listen");

        count = page.Requests.Count;
        await strip.GetByRole(AriaRole.Button, new() { Name = "Play B" }).ClickAsync();
        await page.Until(() => page.Requests.Count > count, "B asked for");
        var without = page.Requests[^1].Body;
        await Assert.That(without.GetProperty("skip").EnumerateArray().Select(x => x.GetString())).IsEquivalentTo(["Pushes"]);
        await Assert.That(without.GetProperty("seed").GetString()).IsEqualTo(with.GetProperty("seed").GetString());
        await page.Settled();
        await Assert.That(page.Requests.All(x => x.Ended == "200")).IsTrue();

        // A better on the first song, the one with: pressed, the song staying until Next, which tells it and goes on to the
        // next song, whose A is the one without
        var next = strip.GetByRole(AriaRole.Button, new() { Name = "Next" });
        await Assert.That(await next.IsDisabledAsync()).IsTrue();
        var better = strip.GetByRole(AriaRole.Button, new() { Name = "A is better" });
        await better.ClickAsync();
        await Assert.That(await better.GetAttributeAsync("aria-pressed")).IsEqualTo("true");
        await Assert.That(await strip.Locator(".listening-where").TextContentAsync()).StartsWith("Song 1 of 6");
        await next.ClickAsync();
        await Assert.That(await strip.Locator(".listening-where").TextContentAsync()).StartsWith("Song 2 of 6 · listen at ");
        await Assert.That(await better.GetAttributeAsync("aria-pressed")).IsEqualTo("false");
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

    /// <summary>Back goes to the song before, its answer pressed, and an answer changed counts in place of the first.</summary>
    [Test]
    public async Task Back_GoesToTheSongBefore_AndAVerdictGivenAgainReplacesTheFirst()
    {
        await using var page = await SongPage.Open(site, "?listen=echo");
        var strip = page.Page.Locator("section.listening");
        await page.Until(() => strip.IsVisibleAsync(), "the question asked");
        var back = strip.GetByRole(AriaRole.Button, new() { Name = "Back" });
        await Assert.That(await back.IsDisabledAsync()).IsTrue();
        // a song heard, whose version the verdicts are kept by
        var count = page.Requests.Count;
        await strip.GetByRole(AriaRole.Button, new() { Name = "Play A" }).ClickAsync();
        await page.Until(() => page.Requests.Count > count, "A asked for");
        await page.Settled();

        var (same, unsure, next) = (strip.GetByRole(AriaRole.Button, new() { Name = "The same" }), strip.GetByRole(AriaRole.Button, new() { Name = "Can't tell" }),
            strip.GetByRole(AriaRole.Button, new() { Name = "Next" }));
        await same.ClickAsync();
        await next.ClickAsync();
        await Assert.That(await strip.Locator(".listening-where").TextContentAsync()).StartsWith("Song 2 of 6");
        await back.ClickAsync();
        await Assert.That(await strip.Locator(".listening-where").TextContentAsync()).StartsWith("Song 1 of 6");
        await Assert.That(await same.GetAttributeAsync("aria-pressed")).IsEqualTo("true");
        await Assert.That(await back.IsDisabledAsync()).IsTrue();
        await unsure.ClickAsync();
        await Assert.That(await same.GetAttributeAsync("aria-pressed")).IsEqualTo("false");
        await next.ClickAsync();
        await Assert.That(await strip.Locator(".listening-where").TextContentAsync()).StartsWith("Song 2 of 6");

        using var client = new HttpClient { BaseAddress = site.Address };
        JsonElement echo = default;
        await page.Until(async () =>
        {
            var verdicts = await client.GetFromJsonAsync<JsonElement>("api/listening/verdicts");
            echo = verdicts.GetProperty("verdicts").EnumerateArray().FirstOrDefault(x => x.GetProperty("question").GetString() == "echo");
            return echo.ValueKind == JsonValueKind.Object && echo.GetProperty("unsure").GetInt32() >= 1;
        }, "the verdict given again kept");
        await Assert.That(echo.GetProperty("same").GetInt32()).IsEqualTo(0);
        await Assert.That(page.Errors).IsEmpty();
    }

    /// <summary>A question's song without its device keeps the genre's other options of the choice it patches, differing by the device alone.</summary>
    [Test]
    public async Task AQuestionsSongWithout_KeepsTheGenresOtherOptions()
    {
        await using var page = await SongPage.Open(site, "?listen=tone");
        var strip = page.Page.Locator("section.listening");
        await page.Until(() => strip.IsVisibleAsync(), "the question asked");
        // the first song's B the one without
        var count = page.Requests.Count;
        await strip.GetByRole(AriaRole.Button, new() { Name = "Play B" }).ClickAsync();
        await page.Until(() => page.Requests.Count > count, "B asked for");
        var effects = page.Requests[^1].Body.GetProperty("genre").GetProperty("effects");
        await Assert.That(effects.GetProperty("tone").GetInt32()).IsEqualTo(0);
        await Assert.That(effects.TryGetProperty("vibrato", out _)).IsTrue();
        await page.Settled();
        await Assert.That(page.Requests.All(x => x.Ended == "200")).IsTrue();
    }

    /// <summary>A question's songs keep the mutes and solos pressed, from A to B and on to the next song.</summary>
    [Test]
    public async Task AQuestionsSongs_KeepTheMutesAndSolos()
    {
        await using var page = await SongPage.Open(site, "?listen=pushes");
        var strip = page.Page.Locator("section.listening");
        await page.Until(() => strip.IsVisibleAsync(), "the question asked");
        var mute = page.Page.Locator("#mixer button.listen[aria-label='Mute the bass']");
        var solo = page.Page.Locator("#mixer button.listen[aria-label='Solo the melody']");

        async Task Play(string letter)
        {
            var count = page.Requests.Count;
            await strip.GetByRole(AriaRole.Button, new() { Name = $"Play {letter}" }).ClickAsync();
            await page.Until(() => page.Requests.Count > count, $"{letter} asked for");
            await page.Settled();
            await page.Until(() => page.IsGenerateEnabled(), $"{letter} arrived");
        }

        await Play("A");
        await page.Page.EvaluateAsync("() => { document.querySelector(\"#mixer button.listen[aria-label='Mute the bass']\").click(); document.querySelector(\"#mixer button.listen[aria-label='Solo the melody']\").click(); }");
        await Assert.That(await mute.GetAttributeAsync("aria-pressed")).IsEqualTo("true");

        await Play("B");
        await Assert.That(await mute.GetAttributeAsync("aria-pressed")).IsEqualTo("true");
        await Assert.That(await solo.GetAttributeAsync("aria-pressed")).IsEqualTo("true");

        await strip.GetByRole(AriaRole.Button, new() { Name = "The same" }).ClickAsync();
        await strip.GetByRole(AriaRole.Button, new() { Name = "Next" }).ClickAsync();
        await Play("A");
        await Assert.That(await mute.GetAttributeAsync("aria-pressed")).IsEqualTo("true");
        await Assert.That(await solo.GetAttributeAsync("aria-pressed")).IsEqualTo("true");
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
