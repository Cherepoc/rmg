namespace Rmg.Tests.Page;

/// <summary>
///     Going back to a song heard before: Previous walks down the list, each song gone back to keeping its place, and a seed
///     typed is asked for as it is.
/// </summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class GoingBackTest(Site site)
{
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
            await Assert.That(await page.History()).IsEquivalentTo(heard, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }

        // the oldest has nothing before it
        await Assert.That(await previous.IsEnabledAsync()).IsFalse();
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
