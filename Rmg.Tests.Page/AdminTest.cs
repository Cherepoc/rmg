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
    public async Task TheOldDashboard_IsGone()
    {
        using var client = new HttpClient { BaseAddress = site.Address };
        await Assert.That((int)(await client.GetAsync("dashboard.html")).StatusCode).IsEqualTo(404);
    }
}
