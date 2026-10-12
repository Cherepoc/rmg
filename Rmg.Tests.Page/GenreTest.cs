namespace Rmg.Tests.Page;

/// <summary>A genre chosen on the page: asked for by its choices, kept in the link, and let go of.</summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class GenreTest(Site site)
{
    [Test]
    public async Task ALinksPreset_IsTheOneOfItsId_AskedOfNoServer_AndEveryIdIsNeverAKey()
    {
        await using var page = await SongPage.Open(site, $"?song=1&settings=B{new string('0', 70)}&genre=reggae");
        await page.FirstSong();
        await page.Settled();

        await Assert.That(page.Requests[0].Body.GetProperty("genre").TryGetProperty("kit", out _)).IsTrue();
        // every request from the page's start, as it reads the link
        await Assert.That(page.Urls.Any(x => x.Contains("/api/genres/reggae"))).IsFalse();
        await Assert.That(await page.Page.Locator("select[aria-label='Genre']").InputValueAsync()).IsEqualTo("preset:reggae");
        // letters alone, and shorter than a key's 11, so that no key is ever an id
        var ids = await page.Page.EvaluateAsync<string[]>("() => [...document.querySelectorAll(\"select[aria-label='Genre'] option[value^='preset:']\")].map(x => x.value.slice(7))");
        // every preset of the page's genres
        var presets = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Site.Root().FullName, "Rmg.WebApi", "Client", "genres.json"))).RootElement;
        await Assert.That(ids).IsEquivalentTo(presets.EnumerateArray().Select(x => x.GetProperty("id").GetString()!).ToArray(), CollectionOrdering.Any);
        await Assert.That(ids.All(x => x.All(char.IsAsciiLetter) && x.Length < 11)).IsTrue();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task APresetChosen_IsAskedFor_ByItsChoices_AndNoneLetsItGo()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var tallies = new System.Collections.Concurrent.ConcurrentBag<string>();
        page.Page.Request += (_, request) =>
        {
            if (request.Url.Contains("/api/tally")) tallies.Add(request.PostData ?? "");
        };
        await page.FirstSong();
        await page.Settled();

        var genre = page.Page.Locator("select[aria-label='Genre']");
        await page.Until(async () => await genre.IsEnabledAsync() && await genre.Locator("option").CountAsync() > 10, "the genres listed");
        var count = page.Requests.Count;
        // as a hand would, the list on the song's panel
        await genre.SelectOptionAsync("preset:jazz");
        await page.Until(() => page.Requests.Count > count, "the song asked for in the genre");
        var asked = page.Requests[^1].Body.GetProperty("genre");
        await Assert.That(asked.GetProperty("kit").GetProperty("jazz").GetInt32()).IsEqualTo(100);
        await Assert.That(asked.GetProperty("drumRole.time").GetProperty("ride").GetInt32()).IsEqualTo(100);
        await page.Settled();
        await Assert.That(await page.Given()).IsEqualTo("All drawn");
        // the list shows it chosen, the link names it, and the next visit asks for it again, kept in this browser
        await Assert.That(page.Page.Url).Contains("settings=B");
        // a preset named in the link by its id, which reads as it is
        await Assert.That(page.Page.Url).Contains("genre=jazz");
        // and in every event about the song, as the ratings by genre read it
        await page.Page.Locator("#like").ClickAsync();
        await page.Until(() => Task.FromResult(tallies.Any(x => x.Contains("\"name\":\"rated\"") && x.Contains("\"genre\":\"jazz\""))), "the rating told with its genre");
        await Assert.That(await genre.InputValueAsync()).IsEqualTo("preset:jazz");
        await page.Page.GotoAsync("/");
        await page.Until(() => page.Requests.Count > count + 1 && page.Requests[^1].Body.TryGetProperty("genre", out var kept) && kept.TryGetProperty("kit", out _),
            "the song of the next visit asked for in the genre kept");
        await page.Settled();
        genre = page.Page.Locator("select[aria-label='Genre']");
        await page.Until(async () => await genre.InputValueAsync() == "preset:jazz", "the genre kept shown chosen");

        count = page.Requests.Count;
        await genre.EvaluateAsync("select => { select.value = 'none'; select.dispatchEvent(new Event('change')); }");
        await page.Until(() => page.Requests.Count > count, "the song asked for in no genre");
        await Assert.That(page.Requests[^1].Body.TryGetProperty("genre", out _)).IsFalse();
        await Assert.That(page.Errors).IsEmpty();
    }

    /// <summary>A preset chosen is kept by its id, so that the next visit asks in it as the page has it then, retuned or not.</summary>
    [Test]
    public async Task APresetChosen_IsKeptByItsId_AndTheNextVisitAsksInItAsItIsNow()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        await page.FirstSong();
        await page.Settled();
        var genre = page.Page.Locator("select[aria-label='Genre']");
        await page.Until(async () => await genre.IsEnabledAsync() && await genre.Locator("option").CountAsync() > 10, "the genres listed");
        await genre.EvaluateAsync("select => { select.value = 'preset:jazz'; select.dispatchEvent(new Event('change')); }");
        await page.Until(() => page.Page.EvaluateAsync<bool>("() => localStorage.getItem('rmg.genre') === JSON.stringify({ preset: 'jazz' })"), "the preset kept by its id");
        await page.Settled();

        // the next visit, with no link, which would name its own genre
        var count = page.Requests.Count;
        await page.Page.GotoAsync("/");
        await page.Until(() => page.Requests.Count > count && page.Requests[^1].Body.TryGetProperty("genre", out _), "the next visit's song asked for in the genre kept");
        var asked = page.Requests[^1].Body;
        await Assert.That(asked.GetProperty("genre").GetProperty("kit").GetProperty("jazz").GetInt32()).IsEqualTo(100);
        await Assert.That(asked.GetProperty("genreName").GetString()).IsEqualTo("Jazz");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task ASongsLink_CarriesItsGenresKey_AndOpensInIt_WhereTheBrowserNeverHadIt()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        await page.FirstSong();
        await page.Settled();
        var genre = page.Page.Locator("select[aria-label='Genre']");
        await page.Until(async () => await genre.IsEnabledAsync() && await genre.Locator("option").CountAsync() > 10, "the genres listed");
        await genre.EvaluateAsync("select => { select.value = 'preset:jazz'; select.dispatchEvent(new Event('change')); }");
        await page.Until(() => Task.FromResult(page.Page.Url.Contains("genre=")), "the link carrying the genre's key");
        var link = page.Page.Url;
        await page.Settled();

        // a browser that never had it: nothing kept, the genre read by its key
        await page.Page.EvaluateAsync("() => localStorage.clear()");
        var count = page.Requests.Count;
        await page.Page.GotoAsync(link);
        await page.Until(() => page.Requests.Count > count, "the linked song asked for");
        var asked = page.Requests[^1].Body;
        await Assert.That(asked.GetProperty("genre").GetProperty("kit").GetProperty("jazz").GetInt32()).IsEqualTo(100);
        await Assert.That(asked.GetProperty("genreName").GetString()).IsEqualTo("Jazz");
        await Assert.That(page.Errors).IsEmpty();
    }

    /// <summary>A genre kept here naming a choice since taken out is asked for without it, rather than every song refused.</summary>
    [Test]
    public async Task AKeptGenre_NamingAChoiceTakenOut_PlaysWithoutIt()
    {
        await using var page = await SongPage.Open(site, script: """
            if (!sessionStorage.getItem("seeded")) {
                sessionStorage.setItem("seeded", "1");
                localStorage.setItem("rmg.genre", JSON.stringify({ name: "Mine", choices: { kickLock: { sections: 50 }, push: { sections: 50, never: 10 } } }));
            }
            """);
        await page.FirstSong();
        await page.Settled();
        var asked = page.Requests[0].Body.GetProperty("genre");
        await Assert.That(asked.TryGetProperty("kickLock", out _)).IsFalse();
        await Assert.That(asked.GetProperty("push").TryGetProperty("never", out _)).IsFalse();
        await Assert.That(asked.GetProperty("push").GetProperty("sections").GetInt32()).IsEqualTo(50);
        await Assert.That(page.Requests.All(x => x.Ended == "200")).IsTrue();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task AnyGenre_DrawsAPresetForEveryNewSong_NamedInItsLink_AndIsKept()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        await page.FirstSong();
        await page.Settled();
        var genre = page.Page.Locator("select[aria-label='Genre']");
        await page.Until(() => genre.IsEnabledAsync(), "the genres listed");
        // every draw the first of those it may draw, so that a draw of the song's own preset again would come every time
        await page.Page.EvaluateAsync("() => { Math.random = () => 0; }");

        var count = page.Requests.Count;
        await genre.SelectOptionAsync("any");
        await page.Until(() => page.Requests.Count > count, "the song asked for in a preset drawn");
        await page.Settled();
        await Assert.That(page.Requests[^1].Body.TryGetProperty("genre", out _)).IsTrue();
        await Assert.That(await genre.InputValueAsync()).IsEqualTo("any");
        await Assert.That(await page.Page.Locator(".genre-about").TextContentAsync()).StartsWith("This song: ");
        await Assert.That(page.Page.Url).Contains("genre=");

        // every new song a preset of its own, the one before never drawn again at once, the first two presets in turn
        var names = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            count = page.Requests.Count;
            await page.Generate();
            await page.Until(() => page.Requests.Count > count, "the next song asked for");
            await page.Settled();
            names.Add(page.Requests[^1].Body.GetProperty("genreName").GetString()!);
        }
        await Assert.That(names.Zip(names.Skip(1)).All(x => x.First != x.Second)).IsTrue();

        // and kept for the next visit
        await page.Page.GotoAsync("/");
        genre = page.Page.Locator("select[aria-label='Genre']");
        await page.Until(async () => await genre.InputValueAsync() == "any", "Any genre kept");
        await Assert.That(page.Errors).IsEmpty();
    }
}
