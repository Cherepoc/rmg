using Microsoft.Playwright;

namespace Rmg.Tests.Page;

/// <summary>The operator's pages and what they ask for, served only with the admin area's credentials.</summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class AdminTest(Site site)
{
    [Test]
    [Arguments("admin/")]
    [Arguments("admin/dashboard.html")]
    [Arguments("admin/api/summary")]
    public async Task TheAdminArea_AsksForItsCredentials_AndRefusesOthers(string path)
    {
        using var client = new HttpClient { BaseAddress = site.Address };
        var refused = await client.GetAsync(path);
        await Assert.That((int)refused.StatusCode).IsEqualTo(401);
        await Assert.That(refused.Headers.WwwAuthenticate.ToString()).StartsWith("Basic");

        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new("Basic", Convert.ToBase64String("admin:wrong"u8.ToArray()));
        await Assert.That((int)(await client.SendAsync(request)).StatusCode).IsEqualTo(401);

        request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"admin:{Site.Token}")));
        await Assert.That((int)(await client.SendAsync(request)).StatusCode).IsEqualTo(200);
    }

    [Test]
    public async Task TheAdminPage_LinksToTheDashboard()
    {
        await using var page = await SongPage.Open(site, "admin/", admin: true);
        await page.Page.Locator("a[href='dashboard.html']").ClickAsync();
        await Assertions.Expect(page.Page.Locator("h1")).ToHaveTextAsync("Analytics");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task TheDashboard_TellsTheRatingsByGenre_AndARatedSongsGenre()
    {
        await using var page = await SongPage.Open(site, "admin/", admin: true);
        // the server's summary with songs rated in genres
        await page.Page.RouteAsync("**/api/summary**", async route =>
        {
            var summary = System.Text.Json.Nodes.JsonNode.Parse(await (await route.FetchAsync()).TextAsync())!;
            summary["ratedVersion"] = "0.7.000";
            summary["rated"] = System.Text.Json.Nodes.JsonNode.Parse("""[{"seed":"1","identity":"","genre":"rock","likes":0,"dislikes":1}]""");
            summary["ratedGenres"] = System.Text.Json.Nodes.JsonNode.Parse(
                """[{"genre":"own","songs":2,"likes":2,"dislikes":0},{"genre":"rock","songs":1,"likes":0,"dislikes":1},{"genre":"","songs":1,"likes":1,"dislikes":0}]""");
            await route.FulfillAsync(new RouteFulfillOptions { ContentType = "application/json", Body = summary.ToJsonString() });
        });
        await page.Page.GotoAsync(new Uri(site.Address, "admin/dashboard.html").ToString());

        var genres = page.Page.Locator("#genres-panel");
        await Assertions.Expect(genres).ToBeVisibleAsync();
        await Assert.That(await genres.Locator("tbody tr td:first-child").AllTextContentsAsync()).IsEquivalentTo(["made on the page", "rock", "none"], CollectionOrdering.Matching);
        await Assert.That(await page.Page.Locator("#rated tbody tr td").AllTextContentsAsync()).IsEquivalentTo(["1", "drawn", "rock", "0", "1"], CollectionOrdering.Matching);
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task ASongsLink_IsDescribed_AsTextToCopy()
    {
        await using var page = await SongPage.Open(site, "admin/", admin: true);
        await page.Page.Context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"]);
        await page.Page.Locator("#song-link").FillAsync($"{site.Address}?song=1A");
        await page.Page.Locator("#describe").ClickAsync();

        var description = page.Page.Locator("#description");
        await Assertions.Expect(description).ToBeVisibleAsync(new() { Timeout = 30_000 });
        var text = await description.InputValueAsync();
        await Assert.That(text).StartsWith("# Song 1A (songs ");
        await Assert.That(text).Contains("## Form:");

        await page.Page.Locator("#copy").ClickAsync();
        await Assertions.Expect(page.Page.Locator("#describe-status")).ToHaveTextAsync("Copied.");
        await Assert.That(await page.Page.EvaluateAsync<string>("() => navigator.clipboard.readText()")).IsEqualTo(text);
        await Assert.That(page.Errors).IsEmpty();
    }

    /// <summary>A link to a song of a genre, a preset's, described as that genre's song.</summary>
    [Test]
    public async Task AGenreSongsLink_IsDescribed_InItsGenre()
    {
        await using var page = await SongPage.Open(site, "admin/", admin: true);
        await page.Page.Locator("#song-link").FillAsync($"{site.Address}?song=1A&genre=rock");
        await page.Page.Locator("#describe").ClickAsync();

        var description = page.Page.Locator("#description");
        await Assertions.Expect(description).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Assert.That(await description.InputValueAsync()).Contains("Genre: Rock");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task ALinkWithNoSong_IsSaidSo()
    {
        await using var page = await SongPage.Open(site, "admin/", admin: true);
        await page.Page.Locator("#song-link").FillAsync("not a seed!");
        await page.Page.Locator("#describe").ClickAsync();

        await Assertions.Expect(page.Page.Locator("#describe-status")).ToContainTextAsync("is not a seed");
        await Assert.That(await page.Page.Locator("#description").IsHiddenAsync()).IsTrue();
    }

    /// <summary>The admin area asks for its credentials however its path is written, a run of slashes in front or its case changed.</summary>
    [Test]
    [Arguments("admin/dashboard.html")]
    [Arguments("/admin/dashboard.html")]
    [Arguments("ADMIN/dashboard.html")]
    public async Task TheAdminArea_AsksForItsCredentials_HoweverItsPathIsWritten(string path)
    {
        using var client = new HttpClient();
        var response = await client.GetAsync(new Uri(site.Address.GetLeftPart(UriPartial.Authority) + "/" + path));
        await Assert.That((int)response.StatusCode).IsEqualTo(401);
    }

    [Test]
    public async Task TheOldDashboard_IsGone()
    {
        using var client = new HttpClient { BaseAddress = site.Address };
        await Assert.That((int)(await client.GetAsync("dashboard.html")).StatusCode).IsEqualTo(404);
    }
}
