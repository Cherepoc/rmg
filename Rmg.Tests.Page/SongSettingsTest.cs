namespace Rmg.Tests.Page;

/// <summary>The song's settings as they show: every slider's ends beside it, none over it.</summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class SongSettingsTest(Site site)
{
    [Test]
    public async Task EverySlidersEnds_FitBesideIt()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        await page.Page.Locator("#composition-panel summary").ClickAsync();

        await Assert.That(await page.Page.Locator(".setting .end").CountAsync()).IsGreaterThan(0);
        var over = await page.Page.EvaluateAsync<string[]>(
            "() => [...document.querySelectorAll('.setting .end')].filter(x => x.scrollWidth > x.clientWidth + 1).map(x => x.textContent)");
        await Assert.That(over).IsEmpty();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task EveryRowsName_AndItsControl_LineUp_TheGenresToo()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        await page.Page.Locator("#composition-panel summary").ClickAsync();
        await page.Until(async () => await page.Page.Locator("select[aria-label='Genre']").IsEnabledAsync(), "the genres listed");

        // every row's name and control as far in as every other's, the genre's, which has no checkbox, among them
        var lefts = await page.Page.EvaluateAsync<double[][]>(
            "() => [...document.querySelectorAll('.setting')].filter(x => x.offsetParent).map(x => [x.querySelector('.setting-name').getBoundingClientRect().left, x.querySelector('.setting-slider').getBoundingClientRect().left])");
        await Assert.That(lefts.Select(x => Math.Round(x[0])).Distinct().Count()).IsEqualTo(1);
        await Assert.That(lefts.Select(x => Math.Round(x[1])).Distinct().Count()).IsEqualTo(1);
        // and every list as wide as a slider: from where it starts to where a slider ends
        var spans = await page.Page.EvaluateAsync<double[][]>(
            "() => [...document.querySelectorAll('.setting')].filter(x => x.offsetParent).map(x => x.querySelector('.setting-slider select, .setting-slider input[type=range]')).map(x => [x.getBoundingClientRect().left, x.getBoundingClientRect().right])");
        await Assert.That(spans.Select(x => Math.Round(x[0])).Distinct().Count()).IsEqualTo(1);
        await Assert.That(spans.Select(x => Math.Round(x[1])).Distinct().Count()).IsEqualTo(1);
        await Assert.That(page.Errors).IsEmpty();
    }

    /// <summary>
    ///     A setting changed while a new song is on its way names the song as it was asked for, listed without the setting,
    ///     and makes it again with the setting once it is here.
    /// </summary>
    [Test]
    public async Task ASettingChangedWhileASongComes_NamesTheSongAsAsked_ThenMakesItAgain()
    {
        await using var page = await SongPage.Open(site);
        await page.FirstSong();
        await page.Settled();
        page.Slow = TimeSpan.FromSeconds(2);
        var count = page.Requests.Count;
        await page.Page.Locator("#generate").ClickAsync();
        await page.Until(() => page.Requests.Count > count, "the song asked for");
        await Assert.That(page.Requests[^1].Body.TryGetProperty("tempo", out var asked) && asked.ValueKind == System.Text.Json.JsonValueKind.Number).IsFalse();
        await page.SetSetting("tempo", 40);
        page.Slow = TimeSpan.Zero;
        // made again with the tempo once the song is here, the song as it came listed as it was asked for
        await page.Until(() => page.Requests.Count > count + 1 && page.Requests[^1].Body.TryGetProperty("tempo", out var tempo) && tempo.ValueKind == System.Text.Json.JsonValueKind.Number && tempo.GetInt32() == 40,
            "the song made again with the tempo", 20);
        var arrived = await page.Page.Locator("#history button[data-entry]").First.GetAttributeAsync("data-song");
        await page.Settled(20);
        await page.Until(async () => await page.IsGenerateEnabled(), "the song made again");
        // named as it came, without the tempo, and again with it once made again
        await Assert.That(await page.Page.Locator("#history button[data-entry]").First.GetAttributeAsync("data-song")).IsNotEqualTo(arrived);
        await Assert.That(page.Errors).IsEmpty();
    }
}
