namespace Rmg.Tests.Page;

/// <summary>The page on a phone's width: everything on it within the screen, the mixer's rows among it.</summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class PhoneTest(Site site)
{
    [Test]
    public async Task OnAPhone_WithEveryPanelOpen_NothingRunsOffTheScreen()
    {
        await using var page = await SongPage.Open(site, phone: true);
        await page.FirstSong();
        await page.Page.EvaluateAsync("() => document.querySelectorAll('details').forEach(x => x.open = true)");
        await page.Until(() => page.Page.Locator("#mixer tr").CountAsync().ContinueWith(x => x.Result > 0), "the mixer's rows");

        // the page as wide as the screen, and no control past its edge, the mixer's too, which no longer scrolls
        await Assert.That(await page.Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth")).IsEqualTo(360);
        var past = await page.Page.EvaluateAsync<string[]>(
            "() => [...document.querySelectorAll('button, input, select, canvas, output, .chip')]" +
            ".filter(x => { const r = x.getBoundingClientRect(); return r.width > 0 && r.right > innerWidth; })" +
            ".map(x => x.id || x.getAttribute('aria-label') || x.textContent)");
        await Assert.That(past).IsEmpty();
        // every song setting on a line of its own, its name beside its slider or list and its value
        var split = await page.Page.EvaluateAsync<string[]>(
            "() => [...document.querySelectorAll('.setting')].filter(row => { const name = row.querySelector('.setting-name').getBoundingClientRect();" +
            " return [...row.querySelectorAll('input[type=range], select, output')].some(x => { const r = x.getBoundingClientRect(); return Math.abs(r.top + r.height / 2 - name.top - name.height / 2) > 8; })" +
            " || name.height > 24; }).map(row => row.querySelector('.setting-name').textContent)");
        await Assert.That(split).IsEmpty();
        // a part's given switch, as wide as a finger gets it, clear of the part's name
        var crowded = await page.Page.EvaluateAsync<string[]>(
            "() => [...document.querySelectorAll('#mixer tr.part')].filter(row => row.querySelector('.given input').getBoundingClientRect().right > row.querySelector('th').getBoundingClientRect().left)" +
            ".map(row => row.querySelector('th').textContent)");
        await Assert.That(crowded).IsEmpty();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task OnAPhone_TheSongPanel_IsALineEach_ForTheSong_ItsButtonsRatingAndShare_WhereItIsWithItsVolume_AndItsDownloads()
    {
        await using var page = await SongPage.Open(site, phone: true);
        await page.FirstSong();

        // each line's controls side by side, and each line under the one before it
        string[][] lines = [["generate", "autoplay", "seed"], ["previous", "play", "stop", "next", "like", "dislike", "share"], ["elapsed", "seek", "total", "volume"],
            ["download", "export-mp3", "bitrate"]];
        var tops = await page.Page.EvaluateAsync<double[][]>(
            "lines => lines.map(ids => ids.map(id => { const r = document.getElementById(id).getBoundingClientRect(); return r.top + r.height / 2; }))", lines);
        for (var i = 0; i < lines.Length; i++)
        {
            foreach (var middle in tops[i]) await Assert.That(Math.Abs(middle - tops[i][0])).IsLessThan(8);
            if (i > 0) await Assert.That(tops[i].Min()).IsGreaterThan(tops[i - 1].Max() + 8);
        }
    }

    [Test]
    public async Task OnAPhone_Share_IsAnIcon_ThatSaysWhatItDidBesideItself()
    {
        await using var page = await SongPage.Open(site, phone: true);
        await page.Page.Context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"]);
        await page.FirstSong();

        var label = page.Page.Locator("#share-label");
        await Assert.That(await label.BoundingBoxAsync() is { Width: <= 1 }).IsTrue();
        await page.Page.Locator("#share").ClickAsync();
        await page.Until(async () => await label.BoundingBoxAsync() is { Width: > 20 }, "Share saying what it did");
        await Assert.That(await label.TextContentAsync()).IsEqualTo("Copied");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task OnAPhone_Share_HandsTheLinkToTheShareSheet()
    {
        await using var page = await SongPage.Open(site, phone: true);
        await page.Page.AddInitScriptAsync("navigator.share = (data) => { window.shared = data.url; return Promise.resolve(); };");
        await page.Page.ReloadAsync();
        await page.FirstSong();

        await page.Page.Locator("#share").ClickAsync();
        await page.Until(async () => await page.Page.EvaluateAsync<string?>("() => window.shared ?? null") is not null, "the link handed to the share sheet");
        await Assert.That(await page.Page.EvaluateAsync<string>("() => window.shared")).Contains("settings=");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task OnAPhone_TheGenreEditor_FillsTheScreen_AndNothingRunsOffIt()
    {
        await using var page = await SongPage.Open(site, phone: true);
        await page.FirstSong();
        var dialog = await page.FromManager("Presets", "Reggae", "Duplicate");

        var box = await dialog.BoundingBoxAsync();
        await Assert.That(box!.Width).IsEqualTo(360).Within(1);
        var past = await page.Page.EvaluateAsync<string[]>(
            "() => [...document.querySelectorAll('dialog.genre-dialog button, dialog.genre-dialog input, dialog.genre-dialog select')]" +
            ".filter(x => { const r = x.getBoundingClientRect(); return r.width > 0 && r.right > innerWidth; })" +
            ".map(x => x.getAttribute('aria-label') || x.textContent)");
        await Assert.That(past).IsEmpty();
        if (Environment.GetEnvironmentVariable("RMG_SCREENSHOTS") is { } directory)
            await page.Page.ScreenshotAsync(new() { Path = Path.Combine(directory, "genre-editor-phone.png") });
        await Assert.That(page.Errors).IsEmpty();
    }
}
