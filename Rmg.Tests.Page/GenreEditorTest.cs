using Microsoft.Playwright;

namespace Rmg.Tests.Page;

/// <summary>
///     A genre made in the editor's popup: its sections of tiles, each a choice's graph and its override, the window a
///     graph opens, its options picked and removed and their amounts set, its parts' tabs, played, kept among one's own.
/// </summary>
[ClassDataSource<Site>(Shared = SharedType.PerTestSession)]
public sealed class GenreEditorTest(Site site)
{
    private static async Task<ILocator> OpenEditor(SongPage page)
    {
        await page.FirstSong();
        await page.Settled();
        return await page.NewGenre();
    }

    /// <summary>A section of the editor, opened.</summary>
    private static async Task<ILocator> Section(ILocator dialog, string title)
    {
        var section = dialog.Locator($"details[data-section='{title}']");
        if (await section.GetAttributeAsync("open") is null)
            await section.Locator("summary").First.ClickAsync();
        return section;
    }

    /// <summary>A choice's tile, as it shows.</summary>
    private static ILocator Tile(ILocator within, string choice) => within.Locator($".genre-tile[data-choice='{choice}']:visible");

    /// <summary>A choice's override, its tile's checkbox.</summary>
    private static ILocator Override(ILocator within, string choice) => Tile(within, choice).Locator(".genre-override input");

    /// <summary>The window changing a choice, opened by its tile's graph.</summary>
    private static async Task<ILocator> Change(ILocator within, string choice)
    {
        await Tile(within, choice).Locator(".genre-graph").ClickAsync();
        var tune = within.Page.Locator("dialog.genre-tune");
        await Assert.That(await tune.IsVisibleAsync()).IsTrue();
        return tune;
    }

    /// <summary>The options the window's choice keeps, by their tabs.</summary>
    private static Task<string[]> Kept(ILocator tune) => tune.Locator(".tune-row").EvaluateAllAsync<string[]>("rows => rows.map(x => x.dataset.option)");

    /// <summary>An option's checkbox in the list of those to pick, the list opened.</summary>
    private static async Task<ILocator> Pick(ILocator tune, string option)
    {
        if (await tune.Locator(".tune-popup").IsHiddenAsync())
            await tune.Locator(".tune-pick-open").ClickAsync();
        return tune.Locator($".tune-picks label[data-option='{option}'] input");
    }

    /// <summary>Every option the window's choice keeps left out by its tab's button, but those given.</summary>
    private static async Task LeaveOutAllBut(ILocator tune, params string[] kept)
    {
        // the list of those to pick closed, as a click elsewhere would, which it lies over
        if (await tune.Locator(".tune-popup").IsVisibleAsync())
            await tune.Locator(".tune-pick-open").ClickAsync();
        foreach (var option in await Kept(tune))
            if (!kept.Contains(option))
                await tune.Locator($".tune-row[data-option='{option}'] .tune-remove").ClickAsync();
    }

    private static Task Done(ILocator tune) => tune.GetByRole(AriaRole.Button, new() { Name = "Done" }).ClickAsync();

    private static async Task<System.Text.Json.JsonElement> Played(SongPage page, ILocator dialog)
    {
        var count = page.Requests.Count;
        await dialog.Locator("footer button", new() { HasTextString = "Play" }).ClickAsync();
        await page.Until(() => page.Requests.Count > count, "the song asked for in the genre made");
        return page.Requests[^1].Body;
    }

    private static int[] Ends(System.Text.Json.JsonElement amount) =>
        amount.ValueKind == System.Text.Json.JsonValueKind.Array ? [..amount.EnumerateArray().Select(x => x.GetInt32())] : [amount.GetInt32(), amount.GetInt32(), amount.GetInt32()];

    [Test]
    public async Task EveryChoice_HasATile_ButAPartsOwn_WhichItsTabsHold()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var names = await page.Page.EvaluateAsync<string[]>("async () => (await (await fetch('api/genres/choices')).json()).map(x => x.name)");
        // a part's instrument, rhythm, feel or drum role, and the kit's pieces, are its tabs'
        string[] tabs = ["kit", "drumPieces", "drumSetup"];
        var missing = new List<string>();
        foreach (var name in names.Where(x => !x.Contains('.') && !tabs.Contains(x)))
            if (await dialog.Locator($".genre-tile[data-choice='{name}']").CountAsync() == 0)
                missing.Add(name);
        await Assert.That(missing).IsEmpty();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task AMeterChanged_IsTheGenres_SavedAmongOnesOwn_AndListedOnTheNextVisit()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var time = await Section(dialog, "Time and feel");

        var tune = await Change(time, "meter");
        await (await Pick(tune, "3/4")).CheckAsync();
        await LeaveOutAllBut(tune, "3/4");
        await Done(tune);
        await Assert.That(await Override(time, "meter").IsCheckedAsync()).IsTrue();
        await dialog.Locator("input[aria-label='Genre name']").FillAsync("Waltzes");
        var count = page.Requests.Count;
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await page.Until(() => page.Requests.Count > count, "the song asked for in the genre made");
        await Assert.That(page.Requests[^1].Body.GetProperty("genre").GetProperty("meter").EnumerateObject().Select(x => x.Name)).IsEquivalentTo(["3/4"]);
        await page.Settled();

        await page.Page.GotoAsync("/");
        var genre = page.Page.Locator("select[aria-label='Genre']");
        await page.Until(async () => await genre.Locator("optgroup[label='Yours'] option").CountAsync() == 1, "the genre made listed");
        await Assert.That(await genre.Locator("optgroup[label='Yours'] option").TextContentAsync()).IsEqualTo("Waltzes");
        await Assert.That(await genre.InputValueAsync()).IsEqualTo("own:0");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task TheEditorClosed_AsksBeforeLosingAChange_ButNotOneAlreadyPlayed()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var asked = 0;
        var accept = false;
        page.Page.Dialog += (_, prompt) =>
        {
            asked++;
            _ = accept ? prompt.AcceptAsync() : prompt.DismissAsync();
        };
        var close = dialog.Locator("> header .genre-close");

        // nothing changed: closed, nothing asked
        await close.ClickAsync();
        await Assert.That(await dialog.IsVisibleAsync()).IsFalse();
        await Assert.That(asked).IsEqualTo(0);

        // a change: the × and Escape ask, and declined, the editor stays open as it was
        await page.NewGenre();
        await Override(await Section(dialog, "Time and feel"), "meter").CheckAsync();
        await close.ClickAsync();
        await page.Page.Keyboard.PressAsync("Escape");
        await Assert.That(asked).IsEqualTo(2);
        await Assert.That(await dialog.IsVisibleAsync()).IsTrue();
        await Assert.That(await Override(dialog, "meter").IsCheckedAsync()).IsTrue();

        // played, it is kept and closing asks nothing; changed again and the loss confirmed, it closes
        await Played(page, dialog);
        await close.ClickAsync();
        await Assert.That(asked).IsEqualTo(2);
        await Assert.That(await dialog.IsVisibleAsync()).IsFalse();
        await page.FromManager("This song's", "Unnamed", "Edit");
        await Override(await Section(dialog, "Time and feel"), "meter").UncheckAsync();
        accept = true;
        await close.ClickAsync();
        await Assert.That(asked).IsEqualTo(3);
        await Assert.That(await dialog.IsVisibleAsync()).IsFalse();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task UnderTheEditor_ThePageHoldsStill_AndEachEndOfAGraphIsOneLine()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        await Section(dialog, "Parts");

        // the editor scrolled past its end, and the wheel over the backdrop: the page where it was
        var before = await page.Page.EvaluateAsync<double>("() => scrollY");
        await page.Page.Mouse.MoveAsync(640, 300);
        for (var i = 0; i < 3; i++)
            await page.Page.Mouse.WheelAsync(0, 2000);
        await page.Page.Mouse.MoveAsync(5, 300);
        await page.Page.Mouse.WheelAsync(0, 2000);
        await Assert.That(await page.Page.EvaluateAsync<double>("() => scrollY")).IsEqualTo(before);

        // a part's instruments, whose names are long, said at each end on a line
        var instruments = Tile(dialog, "instrument.melody");
        await page.Until(async () => await instruments.Locator(".genre-ends > span").CountAsync() == 3, "the instruments' ends said");
        var heights = await instruments.Locator(".genre-ends > span").EvaluateAllAsync<double[]>("spans => spans.map(x => x.getBoundingClientRect().height)");
        await Assert.That(heights.All(x => x <= 18)).IsTrue();
        // and where the words are cut short, the number is whole, within its end
        var numbersWhole = await instruments.Locator(".genre-ends > span").EvaluateAllAsync<bool[]>("spans => spans.map(x => { const n = x.querySelector('.genre-end-number');"
            + " const r = n.getBoundingClientRect(), e = x.getBoundingClientRect(); return n.textContent.trim() !== '' && r.width > 0 && r.right <= e.right + 1 && n.scrollWidth <= n.clientWidth + 1; })");
        await Assert.That(numbersWhole.All(x => x)).IsTrue();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task APresetsName_IsTheDrafts_ToPlay_ButNeverToSaveAsOnesOwn()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var name = dialog.Locator("input[aria-label='Genre name']");
        var save = dialog.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true });
        var play = dialog.Locator("footer button", new() { HasTextString = "Play" });

        // whatever its case and spacing
        await name.FillAsync("  hip-HOP ");
        await Assert.That(await save.IsDisabledAsync()).IsTrue();
        await Assert.That(await play.IsDisabledAsync()).IsFalse();
        await Assert.That(await dialog.Locator("footer .warning").TextContentAsync()).IsEqualTo("hip-HOP is a preset's name: save yours under another.");
        await name.FillAsync("Hip-hop of mine");
        await Assert.That(await save.IsDisabledAsync()).IsFalse();
        await Assert.That(await dialog.Locator("footer .warning").IsHiddenAsync()).IsTrue();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task AGenresDescription_IsOnesOwn_ShownUnderItChosen_AndAPresetsSentenceUnderThePreset()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var about = page.Page.Locator(".genre-about");
        var genre = page.Page.Locator("select[aria-label='Genre']");
        var dialog = await OpenEditor(page);
        var description = dialog.Locator("textarea[aria-label='Genre description']");
        // two lines tall at least, however full the editor, every section open
        await dialog.EvaluateAsync("d => d.querySelectorAll('details').forEach(x => x.open = true)");
        await Assert.That((await description.BoundingBoxAsync())!.Height).IsGreaterThanOrEqualTo(48);

        // made from none, described, saved and chosen: its sentence under the list
        await dialog.Locator("input[aria-label='Genre name']").FillAsync("Slow rain");
        await description.FillAsync("Long, quiet songs for a wet afternoon.");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await page.Until(async () => await about.TextContentAsync() == "Long, quiet songs for a wet afternoon.", "its description shown");
        await dialog.Locator("> header .genre-close").ClickAsync();

        // a preset chosen: its own sentence; edited, the draft takes none of it
        await genre.EvaluateAsync("select => { select.value = 'preset:jazz'; select.dispatchEvent(new Event('change')); }");
        await page.Until(async () => (await about.TextContentAsync() ?? "").Length > 20 && await about.TextContentAsync() != "Long, quiet songs for a wet afternoon.", "the preset's sentence shown");
        await page.FromManager("Presets", "Jazz", "Duplicate");
        await Assert.That(await description.InputValueAsync()).IsEqualTo("");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task TheChosenOption_KeepsItsGreyOutlined_AndTheOnePointedAt_IsNamedWithItsShare()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var tune = await Change(await Section(dialog, "Time and feel"), "meter");
        var chart = tune.Locator(".tune-shares");
        await page.Until(async () => await chart.Locator(".share-chosen").CountAsync() == 1, "the chances drawn");

        // the chosen, 4/4, its own grey as every option has, and an outline in the accent over all
        await Assert.That(await chart.Locator("path[data-option='4/4']").GetAttributeAsync("fill")).StartsWith("#");
        await Assert.That(await chart.Locator("path").Last.GetAttributeAsync("class")).IsEqualTo("share-chosen");
        await Assert.That(await tune.Locator(".tune-legend > span.chosen").TextContentAsync()).IsEqualTo("4/4");

        // pointed at, low at the plainest end: 4/4, named at once with its share there, and lit in the legend
        var box = (await chart.BoundingBoxAsync())!;
        await page.Page.Mouse.MoveAsync(box.X + 2, box.Y + box.Height - 4);
        await Assert.That(await tune.Locator(".tune-pointed").TextContentAsync()).IsEqualTo("4/4 · 100% at the plainest end");
        await Assert.That(await chart.Locator(".share-pointed").CountAsync()).IsEqualTo(1);
        await Assert.That(await tune.Locator(".tune-legend > span.pointed").TextContentAsync()).IsEqualTo("4/4");
        // and let go of off the chart
        await page.Page.Mouse.MoveAsync(box.X + box.Width / 2, box.Y - 60);
        await Assert.That(await tune.Locator(".tune-pointed").IsHiddenAsync()).IsTrue();
        await Assert.That(await chart.Locator(".share-pointed").CountAsync()).IsEqualTo(0);
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task ALength_IsShownAndGivenInMinutesAndSeconds_ATempoInBeatsAMinute()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var time = await Section(dialog, "Time and feel");

        var tune = await Change(time, "length");
        await Assert.That(await tune.Locator("h3").First.TextContentAsync()).IsEqualTo("Length: From, in minutes and seconds");
        await Assert.That(await tune.Locator(".tune-row[data-option='from'] .tune-values").TextContentAsync()).IsEqualTo("1:33 · 1:44 · 2:32");
        var plainest = tune.Locator("input[aria-label='From, plainest']");
        await Assert.That(await plainest.InputValueAsync()).IsEqualTo("1:33");
        // typed as minutes and seconds, or minutes alone, and what is no time put back
        await plainest.FillAsync("3:30");
        await plainest.PressAsync("Tab");
        await Assert.That(await tune.Locator(".tune-row[data-option='from'] .tune-values").TextContentAsync()).IsEqualTo("3:30 · 1:44 · 2:32");
        await plainest.FillAsync("4");
        await plainest.PressAsync("Tab");
        await Assert.That(await plainest.InputValueAsync()).IsEqualTo("4:00");
        await plainest.FillAsync("soon");
        await plainest.PressAsync("Tab");
        await Assert.That(await plainest.InputValueAsync()).IsEqualTo("4:00");
        await Done(tune);

        tune = await Change(time, "tempo");
        await Assert.That(await tune.Locator("h3").First.TextContentAsync()).IsEqualTo("Tempo: From, in beats a minute");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task APresetDuplicated_IsYours_UnderItsNameAndTheFirstNumberFree()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        await page.FirstSong();
        await page.Settled();
        var genre = page.Page.Locator("select[aria-label='Genre']");
        var dialog = page.Page.Locator("dialog.genre-dialog");
        var name = dialog.Locator("input[aria-label='Genre name']");

        // a preset duplicated under its name and the first number free, kept among one's own only once it is saved
        await page.FromManager("Presets", "Rock", "Duplicate");
        await Assert.That(await name.InputValueAsync()).IsEqualTo("Rock 1");
        await dialog.Locator("> header .genre-close").ClickAsync();
        await Assert.That(await genre.Locator("optgroup[label='Yours'] option").CountAsync()).IsEqualTo(0);
        await page.FromManager("Presets", "Rock", "Duplicate");
        await Assert.That(await name.InputValueAsync()).IsEqualTo("Rock 1");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await dialog.Locator("> header .genre-close").ClickAsync();
        await page.FromManager("Presets", "Rock", "Duplicate");
        await Assert.That(await name.InputValueAsync()).IsEqualTo("Rock 2");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await dialog.Locator("> header .genre-close").ClickAsync();
        await Assert.That(await genre.Locator("optgroup[label='Yours'] option").AllTextContentsAsync()).IsEquivalentTo(["Rock 1", "Rock 2"]);
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task SaveAs_KeepsTheDraftUnderANameAsked_TheOneItCameFromAsItWas()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var answer = "";
        page.Page.Dialog += (_, prompt) => _ = prompt.Type == "prompt" ? prompt.AcceptAsync(answer) : prompt.AcceptAsync();
        await dialog.Locator("input[aria-label='Genre name']").FillAsync("First");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

        var count = page.Requests.Count;
        answer = "Second";
        await dialog.Locator("footer button", new() { HasTextString = "Save as" }).ClickAsync();
        await page.Until(() => page.Requests.Count > count, "the song asked for in the genre saved");
        var genre = page.Page.Locator("select[aria-label='Genre']");
        await Assert.That(await genre.Locator("optgroup[label='Yours'] option").AllTextContentsAsync()).IsEquivalentTo(["First", "Second"]);
        await Assert.That(await dialog.Locator("input[aria-label='Genre name']").InputValueAsync()).IsEqualTo("Second");

        // a preset's name refused, nothing kept
        answer = "jazz";
        await dialog.Locator("footer button", new() { HasTextString = "Save as" }).ClickAsync();
        await Assert.That(await genre.Locator("optgroup[label='Yours'] option").CountAsync()).IsEqualTo(2);
        await Assert.That(await dialog.Locator("footer .warning").TextContentAsync()).IsEqualTo("jazz is a preset's name: save yours under another.");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task OnesOwnGenre_IsRenamedInItsPlace_ADeletionAsked_AndNamesOneWhateverTheirCase()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var accept = true;
        var asked = new List<string>();
        page.Page.Dialog += (_, prompt) =>
        {
            asked.Add(prompt.Message);
            _ = prompt.Type == "prompt" ? prompt.AcceptAsync("Other") : accept ? prompt.AcceptAsync() : prompt.DismissAsync();
        };
        var name = dialog.Locator("input[aria-label='Genre name']");
        var save = dialog.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true });
        var yours = page.Page.Locator("select[aria-label='Genre'] optgroup[label='Yours'] option");
        await name.FillAsync("First");
        await save.ClickAsync();
        await name.FillAsync("Second");
        await save.ClickAsync();
        await page.Until(async () => await yours.CountAsync() == 1, "the second saved");

        // renamed in its place, the first let go of, nothing asked
        await name.FillAsync("Third");
        await save.ClickAsync();
        await page.Until(async () => (await yours.AllTextContentsAsync()).SequenceEqual(["Third"]), "renamed in its place");
        await Assert.That(asked).IsEmpty();

        // another saved as "Other", then this one renamed to "other": one of one's own, whatever its case, asked about
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save as…" }).ClickAsync();
        await page.Until(() => Task.FromResult(asked.Count == 1), "the name asked for");
        await page.Until(async () => await yours.CountAsync() == 2, "the copy saved");
        accept = false;
        await name.FillAsync("third");
        await save.ClickAsync();
        await Assert.That(asked.Last()).IsEqualTo("Replace your genre third?");

        // a deletion asked about, and declined, nothing let go of
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        await Assert.That(asked.Last()).StartsWith("Delete your genre ");
        await Assert.That(await yours.CountAsync()).IsEqualTo(2);
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task ALinksGenre_IsKept_AndEveryGenreOfOnesOwn_IsExportedAndImportedAsOneFile()
    {
        // a draft played, unsaved: the link to its song carries its genre by the server's key, its button Edit here
        await using var maker = await SongPage.Open(site, "?song=1");
        var made = await OpenEditor(maker);
        await made.Locator("input[aria-label='Genre name']").FillAsync("Played");
        await Override(await Section(made, "Time and feel"), "meter").CheckAsync();
        await Played(maker, made);
        await maker.Settled();
        maker.Page.Dialog += (_, prompt) => _ = prompt.AcceptAsync();
        await made.Locator("> header .genre-close").ClickAsync();
        var link = maker.Page.Url;

        // opened elsewhere: the link's genre kept from the window of every genre under its name among one's own, the song
        // as it was, and no longer the song's own there
        await using var page = await SongPage.Open(site, link[link.IndexOf('?')..]);
        await page.FirstSong();
        await page.Settled();
        var yours = page.Page.Locator("select[aria-label='Genre'] optgroup[label='Yours'] option");
        var dialog = await page.Manager();
        var count = page.Requests.Count;
        await dialog.Locator("ul[aria-label=\"This song's\"] li", new() { HasText = "Played" }).GetByRole(AriaRole.Button, new() { Name = "Keep", Exact = true }).ClickAsync();
        await Assert.That((await yours.AllTextContentsAsync()).ToArray()).IsEquivalentTo(["Played"]);
        await Assert.That(await dialog.Locator("ul[aria-label=\"This song's\"]").CountAsync()).IsEqualTo(0);
        await Assert.That(page.Requests.Count).IsEqualTo(count);

        // every one of one's own as one file, from the window of every genre
        var download = await page.Page.RunAndWaitForDownloadAsync(() => dialog.GetByRole(AriaRole.Button, new() { Name = "Export all" }).ClickAsync());
        var exported = await File.ReadAllTextAsync((await download.PathAsync())!);
        await Assert.That(System.Text.Json.JsonDocument.Parse(exported).RootElement.GetArrayLength()).IsEqualTo(1);

        // and read back as a list: every one kept, under its name or the first number free
        await dialog.Locator("input[type=file]").SetInputFilesAsync(new FilePayload { Name = "genres.json", MimeType = "application/json", Buffer = System.Text.Encoding.UTF8.GetBytes(exported) });
        await page.Until(async () => await dialog.Locator(".genre-told").TextContentAsync() == "One genre kept among yours.", "the list kept");
        await Assert.That((await yours.AllTextContentsAsync()).ToArray()).IsEquivalentTo(["Played", "Played 1"]);
        await Assert.That(maker.Errors).IsEmpty();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task TheEditor_AndTheWindowOverIt_NeverScrollSideways()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var time = await Section(dialog, "Time and feel");
        await Section(dialog, "Parts");
        var instruments = Tile(dialog, "instrument.melody");
        await page.Until(async () => await instruments.Locator(".genre-ends > span").CountAsync() == 3, "the instruments' ends said");

        const string sideways = "x => [...x.querySelectorAll('*')].filter(e => e.scrollWidth > e.clientWidth + 1"
            + " && ['auto', 'scroll'].includes(getComputedStyle(e).overflowX)).map(e => e.className)";
        await Assert.That(await dialog.EvaluateAsync<string[]>(sideways)).IsEmpty();
        // the tempo's, whose chart has no legend beside it, its handles at its edge
        var tune = await Change(time, "tempo");
        await Assert.That(await tune.EvaluateAsync<string[]>(sideways)).IsEmpty();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task AChoicesGraph_DrawsWhatTheSongsDraw_AndSaysWhatEachEndDraws()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var time = await Section(dialog, "Time and feel");

        // by default: greyed, its graph theirs, every song in the plainest in 4/4
        var meter = Tile(time, "meter");
        await page.Until(async () => await meter.Locator(".genre-graph path").CountAsync() > 1, "the meter's graph drawn");
        await Assert.That(await meter.GetAttributeAsync("class")).Contains("own");
        await Assert.That(await meter.Locator(".genre-ends > span").First.TextContentAsync()).IsEqualTo("Plainest: 4/4 100%");
        // every option's share named where it is pointed at, by its label
        await Assert.That(await meter.Locator(".genre-graph path title").AllTextContentsAsync()).Contains("4/4");
        await Assert.That(await Tile(time, "feelChances").Locator(".genre-graph path title").AllTextContentsAsync()).Contains("Swings");
        // every part plays in the plainest songs of no genre, said as one with how many more, not as the only one
        var parts = Tile(await Section(dialog, "Parts"), "parts");
        await page.Until(async () => await parts.Locator(".genre-ends > span").CountAsync() == 3, "the parts' ends said");
        await Assert.That(await parts.Locator(".genre-ends > span").First.TextContentAsync()).Matches(@"^Plainest: .+ and \d+ more 100%$");
        await Assert.That(await Tile(time, "tempo").Locator(".genre-ends > span").First.TextContentAsync()).IsEqualTo("Plainest: 90–175 bpm");
        // a length's range in minutes and seconds, the shortest tenth of the plainest songs to the longest
        await Assert.That(await Tile(time, "length").Locator(".genre-ends > span").First.TextContentAsync()).IsEqualTo("Plainest: 1:33–3:28");
        // the effects, a vibrato and the rest, as the songs have them
        await Assert.That(await Tile(await Section(dialog, "Sound"), "effects").Locator(".genre-ends > span").First.TextContentAsync()).IsEqualTo("Plainest: none");
        // the chords off the scale, none in the plainest songs, applied dominants the commonest in a middling one
        var offTheScale = Tile(await Section(dialog, "Harmony"), "chordScales");
        await page.Until(async () => await offTheScale.Locator(".genre-ends > span").CountAsync() == 3, "the chords off the scale said");
        await Assert.That(await offTheScale.Locator(".genre-ends > span").First.TextContentAsync()).IsEqualTo("Plainest: none");
        await Assert.That(await offTheScale.Locator(".genre-ends > span").Nth(1).TextContentAsync()).IsEqualTo("Middling: Applied dominants 35%");
        // the lines' scales, the plainest songs' pentatonic the commonest
        var lineScale = Tile(await Section(dialog, "Harmony"), "lineScale");
        await page.Until(async () => await lineScale.Locator(".genre-ends > span").CountAsync() == 3, "the lines' scales said");
        await Assert.That(await lineScale.Locator(".genre-ends > span").First.TextContentAsync()).StartsWith("Plainest: Pentatonic");
        await Assert.That(await Tile(await Section(dialog, "Harmony"), "harmonicRhythm").IsVisibleAsync()).IsTrue();
        // a turnaround, a reharmonisation, never at the plain end
        await Assert.That(await Tile(await Section(dialog, "Harmony"), "turnaround").Locator(".genre-ends > span").First.TextContentAsync()).IsEqualTo("Plainest: none");
        await Assert.That(await Tile(await Section(dialog, "Harmony"), "planing").Locator(".genre-ends > span").First.TextContentAsync()).IsEqualTo("Plainest: none");
        await Assert.That(await Tile(await Section(dialog, "Harmony"), "harmonyDevices").Locator(".genre-ends > span").First.TextContentAsync()).IsEqualTo("Plainest: none");
        await Assert.That(await Tile(await Section(dialog, "Sound"), "articulation").Locator(".genre-ends > span").First.TextContentAsync()).IsEqualTo("Plainest: none");
        await Assert.That(await Tile(await Section(dialog, "Form"), "loop").Locator(".genre-ends > span").First.TextContentAsync()).IsEqualTo("Plainest: none");
        await Assert.That(await Tile(await Section(dialog, "Sound"), "levels").Locator(".genre-ends > span").First.TextContentAsync()).IsEqualTo("Plainest: default");
        await Assert.That(await Tile(await Section(dialog, "Sound"), "rests").Locator(".genre-ends > span").First.TextContentAsync()).IsEqualTo("Plainest: default");
        await Assert.That(await Tile(time, "grooveHold").IsVisibleAsync()).IsTrue();
        await Assert.That(await Tile(time, "pocket").IsVisibleAsync()).IsTrue();
        await Assert.That(await Tile(await Section(dialog, "Form"), "drop").Locator(".genre-ends > span").First.TextContentAsync()).IsEqualTo("Plainest: none");
        // the bass's own choices in its tab, its leading and its landing, the plainest songs' on the root, and the drums' fills in theirs
        var partsSection = await Section(dialog, "Parts");
        await partsSection.GetByRole(AriaRole.Tab, new() { Name = "Bass", Exact = true }).ClickAsync();
        await Assert.That(await Tile(partsSection, "bass").IsVisibleAsync()).IsTrue();
        var landing = Tile(partsSection, "bassLanding");
        await page.Until(async () => await landing.Locator(".genre-ends > span").CountAsync() == 3, "the bass's landing said");
        await Assert.That(await landing.Locator(".genre-ends > span").First.TextContentAsync()).IsEqualTo("Plainest: The root 100%");
        await Assert.That(await Tile(await Section(dialog, "Harmony"), "bassLanding").CountAsync()).IsEqualTo(0);
        await partsSection.GetByRole(AriaRole.Tab, new() { Name = "Drums", Exact = true }).ClickAsync();
        await Assert.That(await Tile(partsSection, "fills").IsVisibleAsync()).IsTrue();
        await Assert.That(await Tile(time, "fills").CountAsync()).IsEqualTo(0);
        await Assert.That(await Tile(dialog, "pan").Locator(".genre-ends > span").First.TextContentAsync()).IsEqualTo("Plainest: default");
        // the forms, the plainest songs always in one of them
        await Assert.That(await Tile(await Section(dialog, "Form"), "formChances").Locator(".genre-ends > span").First.TextContentAsync()).IsEqualTo("Plainest: One of the forms 100%");
        // a section's pattern four bars, as every song's
        await Assert.That(await Tile(dialog, "phrases").Locator(".genre-ends > span").First.TextContentAsync()).IsEqualTo("Plainest: 4 bars 100%");
        // the root motions, each as the songs have it
        await Assert.That(await Tile(await Section(dialog, "Harmony"), "rootMotion").Locator(".genre-ends > span").First.TextContentAsync()).IsEqualTo("Plainest: default");
        await Assert.That(await Tile(time, "feelValues").Locator(".genre-ends > span").First.TextContentAsync()).IsEqualTo("Plainest: default");

        // overridden, its own, and how many options it keeps
        await Override(time, "meter").CheckAsync();
        await Assert.That(await meter.GetAttributeAsync("class")).DoesNotContain("own");
        await Assert.That(await meter.Locator(".genre-count").TextContentAsync()).EndsWith(" options");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task AnOverrideUnchecked_IsAsTheSongsHaveIt_AndCheckedAgainAsItWas()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var harmony = await Section(dialog, "Harmony");

        // overridden, the scales the songs have, major among them, and one left out
        await Override(harmony, "scale").CheckAsync();
        var tune = await Change(harmony, "scale");
        await Assert.That(await Kept(tune)).Contains("major");
        await tune.Locator(".tune-row[data-option='major'] .tune-remove").ClickAsync();
        await Done(tune);
        var genre = (await Played(page, dialog)).GetProperty("genre");
        await Assert.That(genre.GetProperty("scale").TryGetProperty("major", out _)).IsFalse();
        await Assert.That(genre.GetProperty("scale").EnumerateObject().Count()).IsGreaterThan(0);

        await Override(harmony, "scale").UncheckAsync();
        // a genre naming nothing asked for as none
        await Assert.That((await Played(page, dialog)).TryGetProperty("genre", out var none) && none.TryGetProperty("scale", out _)).IsFalse();

        await Override(harmony, "scale").CheckAsync();
        tune = await Change(harmony, "scale");
        await Assert.That(await Kept(tune)).DoesNotContain("major");
        await Assert.That(await Kept(tune)).Contains("naturalMinor");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task TheWindowsOverride_IsCheckedByAChange_AndUncheckedLeavesTheChoiceToTheSongs()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var time = await Section(dialog, "Time and feel");

        var tune = await Change(time, "meter");
        var own = tune.Locator(".tune-override input");
        await Assert.That(await own.IsCheckedAsync()).IsFalse();
        await tune.Locator("input[aria-label='4/4, experimental']").FillAsync("20");
        await tune.Locator("input[aria-label='4/4, experimental']").DispatchEventAsync("change");
        await Assert.That(await own.IsCheckedAsync()).IsTrue();
        await Done(tune);
        await Assert.That(Ends((await Played(page, dialog)).GetProperty("genre").GetProperty("meter").GetProperty("4/4"))[2]).IsEqualTo(20);

        tune = await Change(time, "meter");
        await tune.Locator(".tune-override input").UncheckAsync();
        await Done(tune);
        await Assert.That(await Override(time, "meter").IsCheckedAsync()).IsFalse();
        await Assert.That((await Played(page, dialog)).TryGetProperty("genre", out var none) && none.TryGetProperty("meter", out _)).IsFalse();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task APoolsLastOption_IsKept_AndAGenreFileOfAPoolKeepingNone_IsAProblem()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var time = await Section(dialog, "Time and feel");
        var tune = await Change(time, "meter");
        await LeaveOutAllBut(tune, "4/4");

        await Assert.That(await Kept(tune)).IsEquivalentTo(["4/4"]);
        await Assert.That(await tune.Locator(".tune-row[data-option='4/4'] .tune-remove").IsDisabledAsync()).IsTrue();
        await Assert.That(await (await Pick(tune, "4/4")).IsDisabledAsync()).IsTrue();
        await tune.Locator(".genre-close").ClickAsync();

        await dialog.Locator("input[type=file]").SetInputFilesAsync(new FilePayload { Name = "none.json", MimeType = "application/json", Buffer = System.Text.Encoding.UTF8.GetBytes("""{ "name": "None", "choices": { "meter": {} } }""") });
        await page.Until(async () => await dialog.Locator("footer .warning").TextContentAsync() == "Meter keeps none of its options.", "the problem said");
        await Assert.That(await dialog.Locator("footer button", new() { HasTextString = "Play" }).IsDisabledAsync()).IsTrue();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task TheOptionsToPick_AreFoundByTheirNames_AndAPickedOneIsKeptAtItsOwnAmounts()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var parts = await Section(dialog, "Parts");
        await parts.GetByRole(AriaRole.Tab, new() { Name = "Bass", Exact = true }).ClickAsync();

        // a part's tile and its window titled by the part and what of it it sets
        await Assert.That(await Tile(parts, "instrument.bass").Locator("h4").TextContentAsync()).IsEqualTo("Bass – Instruments");
        var tune = await Change(parts, "instrument.bass");
        await Assert.That(await tune.Locator("h2").TextContentAsync()).IsEqualTo("Bass – Instruments");
        await tune.Locator(".tune-pick-open").ClickAsync();
        await tune.Locator(".tune-popup input[type=search]").FillAsync("tub");
        await Assert.That(await tune.Locator(".tune-picks label:visible").AllTextContentsAsync()).IsEquivalentTo(["Tuba"]);
        await tune.Locator(".tune-picks label[data-option='tuba'] input").CheckAsync();
        await Assert.That(await Kept(tune)).Contains("tuba");
        // the window open, the list closed by Escape
        await page.Page.Keyboard.PressAsync("Escape");
        await Assert.That(await tune.Locator(".tune-popup").IsHiddenAsync()).IsTrue();
        await Assert.That(await tune.IsVisibleAsync()).IsTrue();
        await Done(tune);

        await parts.GetByRole(AriaRole.Tab, new() { Name = "Melody", Exact = true }).ClickAsync();
        await Assert.That(await Override(parts, "instrument.melody").IsCheckedAsync()).IsFalse();
        await Assert.That(await parts.Locator("[role=tab].named").First.TextContentAsync()).IsEqualTo("Bass");
        var genre = (await Played(page, dialog)).GetProperty("genre");
        await Assert.That(genre.GetProperty("instrument.bass").TryGetProperty("tuba", out _)).IsTrue();
        await Assert.That(genre.TryGetProperty("instrument.melody", out _)).IsFalse();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task SwingAndHalfTime_LeftOutAreNever_AndTheSwingsValuesLeftOutAsTheyAre()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var time = await Section(dialog, "Time and feel");

        var chances = await Change(time, "feelChances");
        await Assert.That(await Kept(chances)).Contains("halfTime");
        // chances are no shares: no chart of them
        await Assert.That(await chances.Locator(".tune-shares").IsHiddenAsync()).IsTrue();
        await LeaveOutAllBut(chances);
        await Done(chances);
        var values = await Change(time, "feelValues");
        await LeaveOutAllBut(values);
        await Done(values);

        var genre = (await Played(page, dialog)).GetProperty("genre");
        await Assert.That(genre.GetProperty("feelChances").EnumerateObject().Count()).IsEqualTo(0);
        await Assert.That(genre.TryGetProperty("feelValues", out _)).IsFalse();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task AScaleAndAChordShape_SayHowTheySound_OverTheirNames()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var harmony = await Section(dialog, "Harmony");
        var scale = await Change(harmony, "scale");
        await (await Pick(scale, "dorian")).IsVisibleAsync();
        await Assert.That(await scale.Locator(".tune-picks label[data-option='dorian']").GetAttributeAsync("title")).IsEqualTo("Minor with a raised sixth: dark but hopeful, at home in funk, folk and jazz. On C: C D E♭ F G A B♭.");
        await scale.Locator(".genre-close").ClickAsync();

        var shapes = await Change(harmony, "chordShape");
        await (await Pick(shapes, "triad")).IsVisibleAsync();
        await Assert.That(await shapes.Locator(".tune-picks label[data-option='triad']").GetAttributeAsync("title")).IsEqualTo("Root, third and fifth: the plain chord. On C: C E♭/E G.");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task TheGenre_IsExportedAsAJsonFile_ByItsName()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        await dialog.Locator("input[aria-label='Genre name']").FillAsync("Waltzes");
        var time = await Section(dialog, "Time and feel");
        await Override(time, "meter").CheckAsync();

        var download = await page.Page.RunAndWaitForDownloadAsync(() => dialog.Locator("footer button", new() { HasTextString = "Export JSON" }).ClickAsync());

        await Assert.That(download.SuggestedFilename).IsEqualTo("Waltzes.json");
        using var json = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(await download.PathAsync()));
        await Assert.That(json.RootElement.GetProperty("name").GetString()).IsEqualTo("Waltzes");
        await Assert.That(json.RootElement.GetProperty("choices").TryGetProperty("meter", out _)).IsTrue();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task AGenre_IsExportedInFamilies_AndAFileInThemImportedAsItWas()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        await page.FirstSong();
        await page.Settled();
        var dialog = await page.FromManager("Presets", "Rock", "Duplicate");

        // a family's choices one object of its members, no name with a dot
        var download = await page.Page.RunAndWaitForDownloadAsync(() => dialog.Locator("footer button", new() { HasTextString = "Export JSON" }).ClickAsync());
        var text = await File.ReadAllTextAsync(await download.PathAsync());
        using var json = System.Text.Json.JsonDocument.Parse(text);
        var choices = json.RootElement.GetProperty("choices");
        await Assert.That(choices.EnumerateObject().Select(x => x.Name)).DoesNotContain(x => x.Contains('.'));
        await Assert.That(choices.GetProperty("instrument").GetProperty("riff").TryGetProperty("overdrivenGuitar", out _)).IsTrue();

        // read back as it was written: exported again, the same file
        await dialog.Locator("input[aria-label='Genre name']").FillAsync("Elsewhere");
        await dialog.Locator("input[type=file]").SetInputFilesAsync(new FilePayload { Name = "rock.json", MimeType = "application/json", Buffer = System.Text.Encoding.UTF8.GetBytes(text) });
        await page.Until(async () => await dialog.Locator("input[aria-label='Genre name']").InputValueAsync() == "Rock 1", "the file read");
        var again = await page.Page.RunAndWaitForDownloadAsync(() => dialog.Locator("footer button", new() { HasTextString = "Export JSON" }).ClickAsync());
        await Assert.That(await File.ReadAllTextAsync(await again.PathAsync())).IsEqualTo(text);
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task AGenreFile_IsImported_AndOneThatIsNotAGenre_IsAProblem()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var file = dialog.Locator("input[type=file]");

        await file.SetInputFilesAsync(new FilePayload { Name = "waltzes.json", MimeType = "application/json", Buffer = System.Text.Encoding.UTF8.GetBytes("""{ "name": "Waltzes", "choices": { "meter": { "3/4": 100 } } }""") });

        await page.Until(async () => await dialog.Locator("input[aria-label='Genre name']").InputValueAsync() == "Waltzes", "the file read");
        var time = dialog.Locator("details[data-section='Time and feel']");
        await Assert.That(await time.GetAttributeAsync("open")).IsNotNull();
        await Assert.That(await Override(time, "meter").IsCheckedAsync()).IsTrue();
        var tune = await Change(time, "meter");
        await Assert.That(await Kept(tune)).IsEquivalentTo(["3/4"]);
        await tune.Locator(".genre-close").ClickAsync();

        await file.SetInputFilesAsync(new FilePayload { Name = "other.json", MimeType = "application/json", Buffer = System.Text.Encoding.UTF8.GetBytes("""{ "name": "Other", "choices": { "nothing": {} } }""") });
        // read as the file is, after the choosing
        await page.Until(async () => await dialog.Locator("footer .warning").TextContentAsync() == "That file is not a genre: a name and the choices the editor knows, or a list of them.", "the file refused");
        await Assert.That(await dialog.Locator("input[aria-label='Genre name']").InputValueAsync()).IsEqualTo("Waltzes");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task TheLineup_IsFirstAmongTheParts_AndAPartItNeverPlays_HasNoTab()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var parts = await Section(dialog, "Parts");
        await Assert.That(await parts.Locator(".genre-tile").First.GetAttributeAsync("data-choice")).IsEqualTo("parts");
        await Assert.That(await Tile(parts, "parts").Locator("h4").TextContentAsync()).IsEqualTo("Lineup");
        var soloist = parts.GetByRole(AriaRole.Tab, new() { Name = "Soloist", Exact = true });
        var riff = parts.GetByRole(AriaRole.Tab, new() { Name = "Riff", Exact = true });
        await soloist.ClickAsync();

        // the soloist left out and the riff at 0 everywhere: neither plays, so neither has a tab, the first shown in its place
        var tune = await Change(parts, "parts");
        await tune.Locator(".tune-row[data-option='solo'] .tune-remove").ClickAsync();
        await tune.Locator(".tune-row[data-option='riff'] .tune-option").ClickAsync();
        foreach (var end in new[] { "plainest", "middling", "experimental" })
            await tune.Locator($"input[aria-label='Riff, {end}']").FillAsync("0");
        await Done(tune);
        await Assert.That(await soloist.IsVisibleAsync()).IsFalse();
        await Assert.That(await riff.IsVisibleAsync()).IsFalse();
        await Assert.That(await parts.Locator("[role=tab][aria-selected=true]").First.TextContentAsync()).IsEqualTo("Melody");
        await Assert.That(await Tile(parts, "instrument.melody").IsVisibleAsync()).IsTrue();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task ThePartsTilesAndTabs_StandApart()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var parts = await Section(dialog, "Parts");

        // every tile and tab strip shown, top to bottom, at least a little apart from the one above it
        const string gaps = "x => [...x.querySelectorAll('.genre-tile, .tab-strip')].filter(e => e.offsetParent).map(e => e.getBoundingClientRect())"
            + ".sort((a, b) => a.top - b.top).flatMap((r, i, all) => i === 0 || r.top < all[i - 1].bottom - 1 ? [] : [r.top - all[i - 1].bottom])";
        await Assert.That((await parts.EvaluateAsync<double[]>(gaps)).Min()).IsGreaterThanOrEqualTo(8);
        await parts.GetByRole(AriaRole.Tab, new() { Name = "Drums", Exact = true }).ClickAsync();
        await Assert.That((await parts.EvaluateAsync<double[]>(gaps)).Min()).IsGreaterThanOrEqualTo(8);
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task TheWindow_KeepsItsHeightAsAnOptionIsPicked_AndItsFirstTabIsWhole()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var tune = await Change(await Section(dialog, "Parts"), "instrument.melody");
        await page.Until(async () => await tune.Locator(".tune-lists li").CountAsync() > 0, "the chances said");

        // the first tab inside the list's edges, which clip it, its border and focus ring too
        var clear = await tune.EvaluateAsync<double>("t => { const list = t.querySelector('.tune-list'), row = list.querySelector('.tune-row');"
            + " return row.getBoundingClientRect().top - list.getBoundingClientRect().top; }");
        await Assert.That(clear).IsGreaterThanOrEqualTo(2);

        // an option picked: the chances under the chart stand until the server says them again, so what the window shows
        // is no shorter, however tall the screen
        await tune.Locator(".tune-pick-open").ClickAsync();
        var shorter = await tune.EvaluateAsync<double>("t => { const main = t.querySelector('.tune-main'), before = main.scrollHeight;"
            + " t.querySelector('.tune-picks label:not(:has(input:checked)) input').click(); return before - main.scrollHeight; }");
        await Assert.That(shorter).IsLessThanOrEqualTo(0);
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task TheRiffsTwin_IsLeftOutWithTheRiff_AndPickedOnlyWithIt()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var playing = await Section(dialog, "Parts");
        var tune = await Change(playing, "parts");
        await Assert.That(await Kept(tune)).Contains("riffTwin");

        await tune.Locator(".tune-row[data-option='riff'] .tune-remove").ClickAsync();
        await Assert.That(await Kept(tune)).DoesNotContain("riffTwin");
        var twin = await Pick(tune, "riffTwin");
        await Assert.That(await twin.IsDisabledAsync()).IsTrue();
        await Done(tune);
        var parts = (await Played(page, dialog)).GetProperty("genre").GetProperty("parts");
        await Assert.That(parts.TryGetProperty("riffTwin", out _)).IsFalse();

        tune = await Change(playing, "parts");
        await (await Pick(tune, "riff")).CheckAsync();
        await Assert.That(await tune.Locator(".tune-picks label[data-option='riffTwin'] input").IsDisabledAsync()).IsFalse();
        await Assert.That(await tune.Locator(".tune-picks label[data-option='riffTwin'] input").IsCheckedAsync()).IsFalse();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task HowAGenreWorks_IsOpenAboveTheSections_UntilItIsClosed_OnTheNextVisitToo()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var principles = dialog.Locator("details.genre-principles");
        await Assert.That(await principles.GetAttributeAsync("open")).IsNotNull();
        await Assert.That(await principles.Locator("li").CountAsync()).IsGreaterThan(3);

        await principles.Locator("summary").ClickAsync();
        await Assert.That(await principles.GetAttributeAsync("open")).IsNull();
        // kept once the panel says it closed
        await page.Until(() => page.Page.EvaluateAsync<bool>("() => localStorage.getItem('rmg.genre-principles') === 'off'"), "the closing kept");
        await page.Page.ReloadAsync();
        dialog = await OpenEditor(page);
        await Assert.That(await dialog.Locator("details.genre-principles").GetAttributeAsync("open")).IsNull();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task TheTempo_IsAskedForInBeatsAMinute_AndItsLeanToo()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var time = await Section(dialog, "Time and feel");
        var tune = await Change(time, "tempo");
        // its ends and its lean, none left out nor picked
        await Assert.That(await Kept(tune)).IsEquivalentTo(["from", "to", "lean"]);
        await Assert.That(await tune.Locator(".tune-remove").CountAsync()).IsEqualTo(0);
        await Assert.That(await tune.Locator(".tune-pick").IsHiddenAsync()).IsTrue();
        await tune.Locator(".tune-option[data-option='lean']").ClickAsync();
        await tune.Locator("input[aria-label='Lean, middling']").FillAsync("60");
        await tune.Locator("input[aria-label='Lean, middling']").DispatchEventAsync("change");
        await Done(tune);

        var asked = (await Played(page, dialog)).GetProperty("genre").GetProperty("tempo");
        // the songs' own, 90 to about 175, leaning as given
        await Assert.That(Ends(asked.GetProperty("from"))[1]).IsEqualTo(90);
        await Assert.That(Ends(asked.GetProperty("to"))[1]).IsBetween(170, 180);
        await Assert.That(Ends(asked.GetProperty("lean"))[1]).IsEqualTo(60);
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task ARangeNamedByItsEndsAlone_IsDrawnLeaningToTheMiddle_AndTheGraphsAfterItToo()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        await page.FirstSong();
        await page.Settled();
        // Rock names its tempo's ends and no lean, the editor's first range
        await page.FromManager("Presets", "Rock", "Duplicate");
        var time = await Section(page.Page.Locator("dialog.genre-dialog"), "Time and feel");
        await page.Until(async () => await Tile(time, "meter").Locator(".genre-graph path").CountAsync() > 0, "the graphs drawn");
        await Assert.That(await Tile(time, "length").Locator(".genre-band-centre").CountAsync()).IsEqualTo(1);
        await Assert.That(await Tile(time, "tempo").Locator(".genre-band-centre").CountAsync()).IsEqualTo(1);
        // its window's lean as the songs have it, its handles on the band all the same
        var tune = await Change(time, "tempo");
        await Assert.That(await tune.Locator(".tune-row[data-option='lean'] .tune-values").TextContentAsync()).IsEqualTo("0 · 0 · 0");
        await Assert.That(await tune.Locator(".tune-handle").CountAsync()).IsEqualTo(9);
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task ARange_IsOneBand_ItsEndsAndLeanHandlesOnIt_ALeansHandleMovingWhereItsSongsCentre()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var time = await Section(dialog, "Time and feel");
        // the tile's band shaded, with where its songs centre
        await page.Until(async () => await Tile(time, "length").Locator(".genre-band-centre").CountAsync() == 1, "the length's band drawn");
        await Assert.That(await Tile(time, "length").Locator(".genre-band mask image").CountAsync()).IsEqualTo(1);

        var tune = await Change(time, "length");
        await Assert.That(await tune.Locator(".tune-chart .genre-band-centre").CountAsync()).IsEqualTo(1);
        await Assert.That(await tune.Locator(".tune-handle").CountAsync()).IsEqualTo(9);
        var lean = tune.Locator(".tune-handle[data-option='lean'][data-end='1']");
        var below = double.Parse(await lean.GetAttributeAsync("cy") ?? "");
        // a lean's handle taken chooses its tab, and up leans longer, its handle higher
        await lean.FocusAsync();
        await Assert.That(await tune.Locator(".tune-row[data-option='lean']").GetAttributeAsync("aria-current")).IsEqualTo("true");
        for (var i = 0; i < 3; i++) await lean.PressAsync("PageUp");
        await Assert.That(await tune.Locator("input[aria-label='Lean, middling']").InputValueAsync()).IsEqualTo("30");
        var above = double.Parse(await tune.Locator(".tune-handle[data-option='lean'][data-end='1']").GetAttributeAsync("cy") ?? "");
        await Assert.That(above).IsLessThan(below - 5);
        // the lean's handle follows its ends'
        await tune.Locator(".tune-handle[data-option='to'][data-end='1']").PressAsync("PageUp");
        await Assert.That(double.Parse(await tune.Locator(".tune-handle[data-option='lean'][data-end='1']").GetAttributeAsync("cy") ?? "")).IsLessThan(above);
        // dragged down, toward the band's from, a lean's handle leans shorter
        var box = (await tune.Locator(".tune-handle[data-option='lean'][data-end='2']").BoundingBoxAsync())!;
        await page.Page.Mouse.MoveAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
        await page.Page.Mouse.DownAsync();
        await page.Page.Mouse.MoveAsync(box.X + box.Width / 2, box.Y + box.Height / 2 + 15, new() { Steps = 5 });
        await page.Page.Mouse.UpAsync();
        await Assert.That(int.Parse(await tune.Locator("input[aria-label='Lean, experimental']").InputValueAsync())).IsLessThan(-20);
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task AFormsOptions_AreToldByTheirLetters_WhereRoomIsShort_TheChartsKeepingTheirWidth()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var tune = await Change(await Section(dialog, "Form"), "form");
        await page.Until(async () => await tune.Locator(".tune-lists li").CountAsync() > 0, "the chances said");

        // the legend and the lists in letters, the words over them; the tabs in words
        var legend = await tune.Locator(".tune-legend > span").AllTextContentsAsync();
        await Assert.That(legend).Contains("VCVCBC");
        await Assert.That(legend.All(x => x.All(c => "VPCB".Contains(c)))).IsTrue();
        await Assert.That(await tune.Locator(".tune-legend > span[data-option='vcvcbc']").GetAttributeAsync("title")).IsEqualTo("Verse, chorus, verse, chorus, bridge, chorus");
        await Assert.That(await tune.Locator(".tune-lists li").First.TextContentAsync()).Matches("^[VPCB]+ \\d+%$");
        await Assert.That(await tune.Locator(".tune-option[data-option='vcvcbc'] span").First.TextContentAsync()).IsEqualTo("Verse, chorus, verse, chorus, bridge, chorus");
        // the charts most of the window's width, nothing scrolling sideways
        var widths = await tune.EvaluateAsync<double[]>(
            "t => { const m = t.querySelector('.tune-main'); return [t.querySelector('.tune-shares').getBoundingClientRect().width, m.clientWidth, m.scrollWidth]; }");
        Console.WriteLine($"the chances {widths[0]:F0} of {widths[1]:F0}");
        await Assert.That(widths[0]).IsGreaterThan(widths[1] / 2);
        await Assert.That(widths[2]).IsLessThanOrEqualTo(widths[1]);
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task AnOptionsAmounts_AreSetByTheirNumbers_ItsTabShowingThem_AndAPoolsSharesToldAtEachEnd()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var time = await Section(dialog, "Time and feel");
        var tune = await Change(time, "meter");

        // the songs' own lines drawn, a pool's chances under them, and what each end draws, the commonest first
        await page.Until(async () => await tune.Locator(".tune-shares path").CountAsync() > 0, "the chances drawn");
        await Assert.That(await tune.Locator(".tune-lists > div").First.Locator("li").AllTextContentsAsync()).IsEquivalentTo(["4/4 100%"]);
        await tune.Locator(".tune-option[data-option='3/4']").ClickAsync();
        await tune.Locator("input[aria-label='3/4, experimental']").FillAsync("80");
        await tune.Locator("input[aria-label='3/4, experimental']").DispatchEventAsync("change");
        await tune.Locator(".tune-option[data-option='7/8']").ClickAsync();
        foreach (var (end, value) in new[] { ("plainest", "0"), ("middling", "0"), ("experimental", "30") })
        {
            await tune.Locator($"input[aria-label='7/8, {end}']").FillAsync(value);
            await tune.Locator($"input[aria-label='7/8, {end}']").DispatchEventAsync("change");
        }
        await Assert.That(await tune.Locator(".tune-option[data-option='7/8'] .tune-values").TextContentAsync()).IsEqualTo("0 · 0 · 30");
        // the charts as wide as each other
        var amounts = await tune.Locator(".tune-plot").BoundingBoxAsync();
        var shares = await tune.Locator(".tune-shares").BoundingBoxAsync();
        await Assert.That(Math.Abs(amounts!.Width - shares!.Width)).IsLessThan(1);
        await Assert.That(Math.Abs(amounts.X - shares.X)).IsLessThan(1);
        await Done(tune);

        var asked = (await Played(page, dialog)).GetProperty("genre").GetProperty("meter");
        await Assert.That(Ends(asked.GetProperty("3/4"))[2]).IsEqualTo(80);
        await Assert.That(Ends(asked.GetProperty("7/8"))).IsEquivalentTo([0, 0, 30]);
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task AHandle_StepsByTheArrowKeys_AndClosingAsksBeforeLettingItGo()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var time = await Section(dialog, "Time and feel");
        await Override(time, "meter").CheckAsync();
        var tune = await Change(time, "meter");

        var middling = tune.Locator(".tune-handle[data-end='1']");
        var before = int.Parse((await middling.GetAttributeAsync("aria-valuenow"))!);
        await middling.FocusAsync();
        await page.Page.Keyboard.PressAsync("ArrowDown");
        await page.Page.Keyboard.PressAsync("ArrowDown");
        await Assert.That(int.Parse((await tune.Locator(".tune-handle[data-end='1']").GetAttributeAsync("aria-valuenow"))!)).IsEqualTo(before - 2);

        // the × and Escape ask before losing the change, and declined, the window stays open
        var prompts = 0;
        var accept = false;
        page.Page.Dialog += (_, prompt) =>
        {
            prompts++;
            _ = accept ? prompt.AcceptAsync() : prompt.DismissAsync();
        };
        await tune.Locator("button.genre-close").ClickAsync();
        await page.Page.Keyboard.PressAsync("Escape");
        await Assert.That(prompts).IsEqualTo(2);
        await Assert.That(await tune.IsVisibleAsync()).IsTrue();

        // the loss confirmed, closed, the editor's as it was
        accept = true;
        await tune.Locator("button.genre-close").ClickAsync();
        await Assert.That(await tune.IsVisibleAsync()).IsFalse();
        var asked = (await Played(page, dialog)).GetProperty("genre").GetProperty("meter");
        await Assert.That(Ends(asked.GetProperty("4/4"))[1]).IsEqualTo(before);
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task TheDrumsSetupsAndRoles_KeepToThePiecesChosen()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var parts = await Section(dialog, "Parts");
        await parts.GetByRole(AriaRole.Tab, new() { Name = "Drums", Exact = true }).ClickAsync();

        // congas and bongos alone, percussion: no kit to set up, and only they to take a role
        var pieces = await Change(parts, "drumPieces");
        await (await Pick(pieces, "conga")).CheckAsync();
        await (await Pick(pieces, "bongo")).CheckAsync();
        await LeaveOutAllBut(pieces, "conga", "bongo");
        await Done(pieces);
        var setup = await Change(parts, "drumSetup");
        await Assert.That(await Kept(setup)).IsEquivalentTo(["percussion"]);
        await setup.Locator(".tune-pick-open").ClickAsync();
        await Assert.That(await setup.Locator(".tune-picks label").EvaluateAllAsync<string[]>("rows => rows.map(x => x.dataset.option)")).IsEquivalentTo(["percussion"]);
        await setup.Locator(".genre-close").ClickAsync();
        var roles = parts.Locator(".genre-tabs .genre-tabs");
        await roles.GetByRole(AriaRole.Tab, new() { Name = "Ground (kick)", Exact = true }).ClickAsync();
        var ground = await Change(parts, "drumRole.ground");
        await ground.Locator(".tune-pick-open").ClickAsync();
        await Assert.That(await ground.Locator(".tune-picks label").EvaluateAllAsync<string[]>("rows => rows.map(x => x.dataset.option)")).IsEquivalentTo(["bongo", "conga"]);
        await Assert.That(page.Errors).IsEmpty();
    }

    /// <summary>The editor as it looks, a preset's, its sections, its melody's tab and its drums', where RMG_SCREENSHOTS names a folder.</summary>
    [Test]
    [Explicit]
    public async Task Screenshots()
    {
        var directory = Environment.GetEnvironmentVariable("RMG_SCREENSHOTS") ?? Path.GetTempPath();
        await using var page = await SongPage.Open(site, "?song=1");
        await page.FirstSong();
        await page.Page.SetViewportSizeAsync(1280, 1000);
        var dialog = await page.FromManager("Presets", "Latin", "Duplicate");
        var time = await Section(dialog, "Time and feel");
        await page.Until(async () => await Tile(time, "meter").Locator(".genre-graph path").CountAsync() > 0, "the graphs drawn");
        await page.Page.ScreenshotAsync(new() { Path = Path.Combine(directory, "genre-editor-sections.png") });
        var parts = await Section(dialog, "Parts");
        await parts.ScrollIntoViewIfNeededAsync();
        await page.Page.ScreenshotAsync(new() { Path = Path.Combine(directory, "genre-editor-melody.png") });
        await parts.GetByRole(AriaRole.Tab, new() { Name = "Drums", Exact = true }).ClickAsync();
        await Tile(parts, "drumSetup").ScrollIntoViewIfNeededAsync();
        await page.Page.ScreenshotAsync(new() { Path = Path.Combine(directory, "genre-editor-drums.png") });
        var pieces = await Change(parts, "drumPieces");
        await page.Page.ScreenshotAsync(new() { Path = Path.Combine(directory, "genre-tune-many.png") });
        await pieces.Locator(".genre-close").ClickAsync();
        var tune = await Change(time, "meter");
        await page.Until(async () => await tune.Locator(".tune-shares path").CountAsync() > 0, "the chances drawn");
        await page.Page.ScreenshotAsync(new() { Path = Path.Combine(directory, "genre-tune.png") });
        await tune.Locator(".tune-pick-open").ClickAsync();
        await page.Page.ScreenshotAsync(new() { Path = Path.Combine(directory, "genre-tune-options.png") });
        await page.Page.SetViewportSizeAsync(390, 844);
        await page.Page.ScreenshotAsync(new() { Path = Path.Combine(directory, "genre-tune-phone.png") });
    }
}
