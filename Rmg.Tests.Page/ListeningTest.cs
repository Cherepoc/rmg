using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Rmg.Tests.Page;

/// <summary>
///     The listening mode (<c>?listen=</c> a question): a song heard with a device and without, and a verdict on it kept.
///     The questions are the tests' own (<c>listening.json</c> here), each test that gives verdicts its own question, so
///     that the verdicts kept are its.
/// </summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class ListeningTest(Site site)
{
    /// <summary>A question's songs leave the page's own settings, genre and list as they were for the next ordinary visit.</summary>
    [Test]
    public async Task AQuestionsSongs_LeaveThePagesSettingsAndList_AsTheyWere()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        await page.Settled();
        await page.SetSetting("tempo", 40);
        await page.Settled();
        var (heard, given) = (await page.History(), await page.Given());

        await page.Page.GotoAsync("/?listen=quieter-form");
        var strip = page.Page.Locator("section.listening");
        await page.Until(() => strip.IsVisibleAsync(), "the question asked");
        var count = page.Requests.Count;
        await strip.GetByRole(AriaRole.Button, new() { Name = "Play A" }).ClickAsync();
        await page.Until(() => page.Requests.Count > count, "A asked for");
        await page.Settled();

        await page.Page.GotoAsync("/");
        await page.FirstSong();
        await page.Settled();
        await Assert.That(await page.Given()).IsEqualTo(given);
        await Assert.That((await page.History()).Skip(1)).IsEquivalentTo(heard, CollectionOrdering.Matching);
        await Assert.That(page.Errors).IsEmpty();
    }

    /// <summary>A question's song with its device lays the patch its side gives over the genre, a device the genre lacks added, and its song without none.</summary>
    [Test]
    public async Task AQuestionsSongWith_AddsWhatItsPatchGives()
    {
        await using var page = await SongPage.Open(site, "?listen=rock-louder");
        var strip = page.Page.Locator("section.listening");
        await page.Until(() => strip.IsVisibleAsync(), "the question asked");
        var count = page.Requests.Count;
        await strip.GetByRole(AriaRole.Button, new() { Name = "Play A" }).ClickAsync();
        await page.Until(() => page.Requests.Count > count, "A asked for");
        await Assert.That(page.Requests[^1].Body.GetProperty("genre").GetProperty("louderForms").GetProperty("quieter").GetInt32()).IsEqualTo(100);
        await page.Settled();

        count = page.Requests.Count;
        await strip.GetByRole(AriaRole.Button, new() { Name = "Play B" }).ClickAsync();
        await page.Until(() => page.Requests.Count > count, "B asked for");
        var without = page.Requests[^1].Body.GetProperty("genre");
        // the genre's own, Rock's quieter form at its three ends
        await Assert.That(without.GetProperty("louderForms").GetProperty("quieter").ValueKind).IsEqualTo(JsonValueKind.Array);
        await page.Settled();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task AQuestion_PlaysASongWithAndWithout_AndKeepsTheVerdict()
    {
        await using var page = await SongPage.Open(site, "?listen=quieter-form");
        var strip = page.Page.Locator("section.listening");
        await page.Until(() => strip.IsVisibleAsync(), "the question asked");
        await Assert.That(await strip.Locator(".listening-question").TextContentAsync()).Contains("acoustic");
        // where to listen, the time its device first plays
        await Assert.That(await strip.Locator(".listening-where").TextContentAsync()).StartsWith("Song 1 of 6 · listen at ");
        // and the parts to listen to, those that differ the most there
        await Assert.That(await strip.Locator(".listening-focus").TextContentAsync()).StartsWith("Listen to: ");
        // no song rolled as the page opens, the question asking for its own; a tempo given here none of them
        await Task.Delay(SongPage.PastRemake);
        await Assert.That(page.Requests).IsEmpty();
        await page.SetSetting("tempo", 40);

        // the first song's A with the quieter form, its B without it, both in Black metal
        var count = page.Requests.Count;
        await strip.GetByRole(AriaRole.Button, new() { Name = "Play A" }).ClickAsync();
        await page.Until(() => page.Requests.Count > count, "A asked for");
        var with = page.Requests[^1].Body;
        await Assert.That(with.TryGetProperty("skip", out _)).IsFalse();
        await Assert.That(with.TryGetProperty("tempo", out var tempo) && tempo.ValueKind != JsonValueKind.Null).IsFalse();
        await Assert.That(with.GetProperty("genreName").GetString()).IsEqualTo("Black metal");
        await page.Settled();
        // the song started just before where to listen rather than at its start
        var listenAt = (await strip.Locator(".listening-where").TextContentAsync())!.Split("listen at ")[1].Split(",")[0].Split(":").Select(int.Parse).ToArray();
        var seconds = listenAt[0] * 60 + listenAt[1];
        await page.Until(async () => (await page.Page.Locator("#elapsed").TextContentAsync())!.Split(":").Select(int.Parse).ToArray() is var shown
                                     && shown[0] * 60 + shown[1] >= Math.Max(0, seconds - 3), "the song started where to listen");
        // and not past it, as it would be had it started anywhere else
        await Assert.That((await page.Page.Locator("#elapsed").TextContentAsync())!.Split(":").Select(int.Parse).ToArray() is var at ? at[0] * 60 + at[1] : 0).IsLessThanOrEqualTo(seconds + 2);

        count = page.Requests.Count;
        await strip.GetByRole(AriaRole.Button, new() { Name = "Play B" }).ClickAsync();
        await page.Until(() => page.Requests.Count > count, "B asked for");
        var without = page.Requests[^1].Body;
        await Assert.That(without.GetProperty("genre").GetProperty("louderForms").GetProperty("quieter").GetInt32()).IsEqualTo(0);
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
        await Assert.That(page.Requests[^1].Body.GetProperty("genre").GetProperty("louderForms").GetProperty("quieter").GetInt32()).IsEqualTo(0);

        using var client = new HttpClient { BaseAddress = site.Address };
        client.DefaultRequestHeaders.Authorization = new("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"admin:{Site.Token}")));
        JsonElement question = default;
        await page.Until(async () =>
        {
            var summary = await client.GetFromJsonAsync<JsonElement>("admin/api/summary");
            question = summary.GetProperty("verdicts").EnumerateArray().FirstOrDefault(x => x.GetProperty("question").GetString() == "quieter-form");
            return question.ValueKind == JsonValueKind.Object;
        }, "the verdict kept");
        await Assert.That(question.GetProperty("with").GetInt32()).IsGreaterThanOrEqualTo(1);
        await Assert.That(page.Errors).IsEmpty();
    }

    /// <summary>Back goes to the song before, its answer pressed, and an answer changed counts in place of the first.</summary>
    [Test]
    public async Task Back_GoesToTheSongBefore_AndAVerdictGivenAgainReplacesTheFirst()
    {
        await using var page = await SongPage.Open(site, "?listen=verdict-replaced");
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
        JsonElement asked = default;
        await page.Until(async () =>
        {
            var verdicts = await client.GetFromJsonAsync<JsonElement>("api/listening/verdicts");
            asked = verdicts.GetProperty("verdicts").EnumerateArray().FirstOrDefault(x => x.GetProperty("question").GetString() == "verdict-replaced");
            return asked.ValueKind == JsonValueKind.Object && asked.GetProperty("unsure").GetInt32() >= 1;
        }, "the verdict given again kept");
        await Assert.That(asked.GetProperty("same").GetInt32()).IsEqualTo(0);
        await Assert.That(page.Errors).IsEmpty();
    }

    /// <summary>A question's song without its device keeps the genre's other options of the choice it patches, differing by the device alone.</summary>
    [Test]
    public async Task AQuestionsSongWithout_KeepsTheGenresOtherOptions()
    {
        // Rock's, whose other option of the choice its question patches is not 0, as a patch zeroing the choice would make it
        await using var page = await SongPage.Open(site, "?listen=rock-quieter");
        var strip = page.Page.Locator("section.listening");
        await page.Until(() => strip.IsVisibleAsync(), "the question asked");
        // the first song's B the one without
        var count = page.Requests.Count;
        await strip.GetByRole(AriaRole.Button, new() { Name = "Play B" }).ClickAsync();
        await page.Until(() => page.Requests.Count > count, "B asked for");
        // its patch laid over the genre's choice, the choice's other options as the genre has them
        var forms = page.Requests[^1].Body.GetProperty("genre").GetProperty("louderForms");
        await Assert.That(forms.GetProperty("quieter").GetInt32()).IsEqualTo(0);
        await Assert.That(forms.GetProperty("guitars").EnumerateArray().Select(x => x.GetInt32()).ToArray()).IsEquivalentTo([20, 50, 60], CollectionOrdering.Matching);
        await Assert.That(page.Errors).IsEmpty();
    }

    /// <summary>A question's songs keep the mutes and solos pressed, from A to B and on to the next song.</summary>
    [Test]
    public async Task AQuestionsSongs_KeepTheMutesAndSolos()
    {
        await using var page = await SongPage.Open(site, "?listen=mutes-kept");
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

    /// <summary>A question's song without its device leaves out the stages it names, and its song with plays them all.</summary>
    [Test]
    public async Task AQuestionsSongWithout_LeavesOutItsStages()
    {
        await using var page = await SongPage.Open(site, "?listen=ghosts-left-out");
        // the events the page tells, which a song no name names tells without its seed
        var told = new System.Collections.Concurrent.ConcurrentQueue<System.Text.Json.JsonElement>();
        await page.Page.RouteAsync("**/api/tally", async route =>
        {
            told.Enqueue(System.Text.Json.JsonDocument.Parse(route.Request.PostData ?? "{}").RootElement.Clone());
            await route.ContinueAsync();
        });
        var strip = page.Page.Locator("section.listening");
        await page.Until(() => strip.IsVisibleAsync(), "the question asked");
        foreach (var (letter, skips) in new[] { ("A", false), ("B", true) })
        {
            var count = page.Requests.Count;
            await strip.GetByRole(AriaRole.Button, new() { Name = $"Play {letter}" }).ClickAsync();
            await page.Until(() => page.Requests.Count > count, $"{letter} asked for");
            var body = page.Requests[^1].Body;
            await Assert.That(body.GetProperty("genreName").GetString()).IsEqualTo("Funk");
            await Assert.That(body.TryGetProperty("skip", out var skip) ? skip.EnumerateArray().Select(x => x.GetString()).ToArray() : [])
                .IsEquivalentTo(skips ? ["Ghosts"] : Array.Empty<string?>(), CollectionOrdering.Matching);
            await page.Settled();
            // the song without, which its seed and settings do not name, not rated; a question's songs, A's too, neither
            // linked nor listed whatever their name
            if (skips)
                await Assert.That(await page.Page.Locator("#like").IsDisabledAsync()).IsTrue();
            // and its arrival told without its seed, as A's with it
            await page.Until(() => told.Count(x => x.GetProperty("name").GetString() == "song_generated") >= (skips ? 2 : 1), $"{letter}'s arrival told");
            var generated = told.Last(x => x.GetProperty("name").GetString() == "song_generated");
            await Assert.That(generated.TryGetProperty("seed", out var seed) && seed.ValueKind == System.Text.Json.JsonValueKind.String).IsEqualTo(!skips).Because(letter);
        }

        await Assert.That(page.Requests.All(x => x.Ended == "200")).IsTrue();
        await Assert.That(page.Errors).IsEmpty();
    }

    /// <summary>A question's sides give their songs the settings they name, a mood each, the rest as by default.</summary>
    [Test]
    public async Task AQuestionsSides_GiveTheirSongsTheirSettings()
    {
        await using var page = await SongPage.Open(site, "?listen=mood-given");
        var strip = page.Page.Locator("section.listening");
        await page.Until(() => strip.IsVisibleAsync(), "the question asked");
        // the first song's A the one with, its B the one without
        foreach (var (letter, darkness) in new[] { ("A", 0), ("B", 61) })
        {
            var count = page.Requests.Count;
            await strip.GetByRole(AriaRole.Button, new() { Name = $"Play {letter}" }).ClickAsync();
            await page.Until(() => page.Requests.Count > count, $"{letter} asked for");
            var body = page.Requests[^1].Body;
            await Assert.That(body.GetProperty("darkness").GetInt32()).IsEqualTo(darkness);
            await Assert.That(body.TryGetProperty("intensity", out var intensity) && intensity.ValueKind != JsonValueKind.Null).IsFalse();
            await page.Settled();
        }

        await Assert.That(page.Requests.All(x => x.Ended == "200")).IsTrue();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task NoQuestion_ListsEveryQuestion_WithItsLink_AndAsksForNoSong()
    {
        await using var page = await SongPage.Open(site, "?listen");
        var index = page.Page.Locator("section.listening-index");
        await page.Until(() => index.IsVisibleAsync(), "the questions listed");
        var questions = index.Locator("li[data-question]");
        await Assert.That(await questions.CountAsync()).IsGreaterThanOrEqualTo(1);
        var question = index.Locator("li[data-question='quieter-form']");
        await Assert.That(await question.Locator("a").GetAttributeAsync("href")).IsEqualTo("?listen=quieter-form");
        await Assert.That(await question.Locator(".listening-where").TextContentAsync()).StartsWith("Black metal · ");
        await Task.Delay(SongPage.PastRemake);
        await Assert.That(page.Requests).IsEmpty();

        // a link asks its question
        await question.Locator("a").ClickAsync();
        await page.Until(() => page.Page.Locator("section.listening:not(.listening-index)").IsVisibleAsync(), "the question asked");
        await Assert.That(page.Errors).IsEmpty();
    }
}
