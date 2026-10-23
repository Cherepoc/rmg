using Microsoft.Playwright;

namespace Rmg.Tests.Page;

/// <summary>
///     Walking the songs heard: Previous goes down the list and Next back up it, each song gone to keeping its place, Next
///     past the latest asking for a new one; and a seed typed is asked for as it is.
/// </summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class WalkingTheListTest(Site site)
{
    /// <summary>A song made in a preset is still on the list on the next visit, as one in no genre is.</summary>
    [Test]
    public async Task ASongInAPreset_IsStillListed_AfterAReload()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        await page.Settled();
        var genre = page.Page.Locator("select[aria-label='Genre']");
        await page.Until(() => genre.IsEnabledAsync(), "the genres listed");
        var count = page.Requests.Count;
        await genre.SelectOptionAsync(new SelectOptionValue { Label = "Rock" });
        await page.Until(() => page.Requests.Count > count, "the song asked for in Rock");
        await page.Settled();
        // its entries, which carry its genre's key
        var entries = page.Page.Locator("button[data-entry]");
        var heard = await entries.EvaluateAllAsync<string[]>("xs => xs.map(x => x.dataset.entry)");
        await Assert.That(heard[0]).EndsWith(":rock");

        // the page opened again rather than reloaded at the song's, which would open with that song and list it anew
        await page.Page.GotoAsync("/");
        await page.FirstSong();
        await page.Settled();
        var kept = await entries.EvaluateAllAsync<string[]>("xs => xs.map(x => x.dataset.entry)");
        await Assert.That(kept.Intersect(heard).Count()).IsEqualTo(heard.Length);
        await Assert.That(page.Errors).IsEmpty();
    }

    /// <summary>A listed song whose genre cannot be fetched is not played in no genre, which would be another song, but said so.</summary>
    [Test]
    public async Task AListedSongWhoseGenreCannotBeFetched_IsNotPlayed_ButSaidSo()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        await page.Generate();
        await page.Settled();
        // the older entry's genre a key the server keeps no genre by
        var entry = page.Page.Locator("#history button[data-entry]").Last;
        await entry.EvaluateAsync("x => x.dataset.entry = `${x.dataset.entry.split(':').slice(0, 2).join(':')}:0000000000Z`");
        // walked back to by Previous, which reads the entry as the list holds it
        var count = page.Requests.Count;
        await page.Page.Locator("#previous").ClickAsync();
        await page.Until(async () => (await page.Status()).Contains("could not be fetched"), "the refusal said");
        await Assert.That(page.Requests.Count).IsEqualTo(count);
        // but the browser's word of the genre not found, which the page asked for
        await Assert.That(page.Errors.Where(x => !x.Contains("api/genres/"))).IsEmpty();
    }

    [Test]
    public async Task Previous_WalksDownTheList_KeepingItsOrder()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        await page.Generate();
        await page.Generate();
        var heard = await page.History();
        var previous = page.Page.Locator("#previous");

        foreach (var seed in heard.Skip(1))
        {
            await previous.ClickAsync();
            await page.Until(async () => await page.Seed() == seed && await page.IsGenerateEnabled(), $"song {seed} gone back to");
            await Assert.That(await page.History()).IsEquivalentTo(heard, CollectionOrdering.Matching);
        }

        // the oldest has nothing before it
        await Assert.That(await previous.IsEnabledAsync()).IsFalse();
        await Assert.That(page.Errors).IsEmpty();
    }

    /// <summary>A song rated shows its rating on the list, as its buttons do, and loses it when the rating is taken back.</summary>
    [Test]
    public async Task ASongRated_ShowsItsRatingOnTheList()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        await page.Settled();
        var current = page.Page.Locator("#history [aria-current]");
        await page.Page.Locator("#like").ClickAsync();
        await page.Until(async () => (await current.GetAttributeAsync("aria-label"))!.EndsWith(", liked"), "the song liked on the list");
        await Assert.That(await current.TextContentAsync()).EndsWith("♥");
        await page.Page.Locator("#dislike").ClickAsync();
        await page.Until(async () => (await current.GetAttributeAsync("aria-label"))!.EndsWith(", disliked"), "the song disliked on the list");
        await page.Page.Locator("#dislike").ClickAsync();
        await page.Until(async () => !(await current.GetAttributeAsync("aria-label"))!.Contains("liked"), "the rating taken back on the list");
        await Assert.That(await current.TextContentAsync()).IsEqualTo(await page.Seed());
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task TheList_IsKept_ForThePageOpenedAgain()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        await page.Generate();
        var heard = await page.History();

        // opened again at its own address, rather than reloaded at the song's, which would open with that song
        await page.Page.GotoAsync("/");
        await page.Until(async () => await page.Seed() is var seed && seed != "" && seed != heard[0] && await page.IsGenerateEnabled(), "a new song");

        // the songs heard before under the one the page opens with
        await Assert.That((await page.History()).Skip(1)).IsEquivalentTo(heard, CollectionOrdering.Matching);
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task Next_WalksBackUpTheList_ThenAsksForANewSong()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        await page.Generate();
        var heard = await page.History();
        var latest = heard[0];
        await page.Page.Locator("#previous").ClickAsync();
        await page.Until(async () => await page.Seed() == heard[1] && await page.IsGenerateEnabled(), "the first song gone back to");
        var next = page.Page.Locator("#next");

        await next.ClickAsync();
        await page.Until(async () => await page.Seed() == latest && await page.IsGenerateEnabled(), "the latest song gone on to");
        await Assert.That(await page.History()).IsEquivalentTo(heard, CollectionOrdering.Matching);

        // past the latest, a new random song on top
        var asked = page.Requests.Count;
        await next.ClickAsync();
        await page.Until(async () => await page.Seed() != latest && await page.IsGenerateEnabled(), "a new song");
        await Assert.That(page.Since(asked).Single().Seed).IsNull();
        await Assert.That((await page.History())[0]).IsEqualTo(await page.Seed());
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task ASeedTyped_IsAskedFor_ItsButtonWaitingForOne()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        var first = await page.Seed();
        await page.Generate();
        var button = page.Page.Locator("#generate-seeded");
        await Assert.That(await button.IsEnabledAsync()).IsFalse();

        await page.Page.Locator("#seed-input").FillAsync(first);
        await button.ClickAsync();
        await page.Until(async () => await page.Seed() == first && await page.IsGenerateEnabled(), "the typed seed's song");

        await Assert.That(page.Requests[^1].Seed).IsEqualTo(first);
        await Assert.That(page.Errors).IsEmpty();
    }
}
