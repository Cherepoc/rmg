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
        await page.Page.Locator("#settings-panel summary").ClickAsync();
        var edit = page.Page.Locator("button.genre-edit");
        await page.Until(() => edit.IsEnabledAsync(), "the editor ready");
        await edit.ClickAsync();
        var dialog = page.Page.Locator("dialog.genre-dialog");
        await page.Until(() => dialog.IsVisibleAsync(), "the editor open");
        return dialog;
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
        await dialog.Locator("footer button", new() { HasTextString = "Save" }).ClickAsync();
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
        await page.Page.Locator("button.genre-edit").ClickAsync();
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
        await page.Page.Locator("button.genre-edit").ClickAsync();
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
        await page.Until(async () => await instruments.Locator(".genre-ends span").CountAsync() == 3, "the instruments' ends said");
        var heights = await instruments.Locator(".genre-ends span").EvaluateAllAsync<double[]>("spans => spans.map(x => x.getBoundingClientRect().height)");
        await Assert.That(heights.All(x => x <= 18)).IsTrue();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task AChoicesGraph_DrawsWhatTheSongsDraw_AndSaysWhatEachEndDraws()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var time = await Section(dialog, "Time and feel");

        // as the songs have it: greyed, its graph theirs, every song in the plainest in 4/4
        var meter = Tile(time, "meter");
        await page.Until(async () => await meter.Locator(".genre-graph path").CountAsync() > 1, "the meter's graph drawn");
        await Assert.That(await meter.GetAttributeAsync("class")).Contains("own");
        await Assert.That(await meter.Locator(".genre-ends span").First.TextContentAsync()).IsEqualTo("Plainest: 4/4 100%");
        // every option's share named where it is pointed at, by its label
        await Assert.That(await meter.Locator(".genre-graph path title").AllTextContentsAsync()).Contains("4/4");
        await Assert.That(await Tile(time, "feelChances").Locator(".genre-graph path title").AllTextContentsAsync()).Contains("Swings");
        // every part plays in the plainest songs of no genre, said as one with how many more, not as the only one
        var parts = Tile(await Section(dialog, "Parts playing"), "parts");
        await page.Until(async () => await parts.Locator(".genre-ends span").CountAsync() == 3, "the parts' ends said");
        await Assert.That(await parts.Locator(".genre-ends span").First.TextContentAsync()).Matches(@"^Plainest: .+ and \d+ more 100%$");
        await Assert.That(await Tile(time, "tempo").Locator(".genre-ends span").First.TextContentAsync()).IsEqualTo("Plainest: 90–175 bpm");
        await Assert.That(await Tile(time, "feelValues").Locator(".genre-ends span").First.TextContentAsync()).IsEqualTo("Plainest: as is");

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

        // a part's tile titled by what of it it sets, its window by the part and that
        await Assert.That(await Tile(parts, "instrument.bass").Locator("h4").TextContentAsync()).IsEqualTo("Instruments");
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
        await page.Until(async () => await dialog.Locator("footer .warning").TextContentAsync() == "That file is not a genre: a name and the choices the editor knows.", "the file refused");
        await Assert.That(await dialog.Locator("input[aria-label='Genre name']").InputValueAsync()).IsEqualTo("Waltzes");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task TheRiffsTwin_IsLeftOutWithTheRiff_AndPickedOnlyWithIt()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var playing = await Section(dialog, "Parts playing");
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
    public async Task AHandle_StepsByTheArrowKeys_AndClosingLetsItGo()
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

        // closed with the ×, the editor's as it was
        await tune.Locator("button.genre-close").ClickAsync();
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
        await page.Page.Locator("#settings-panel summary").ClickAsync();
        var genre = page.Page.Locator("select[aria-label='Genre']");
        await page.Until(() => genre.IsEnabledAsync(), "the genres listed");
        await genre.EvaluateAsync("s => { s.value = 'preset:latin'; s.dispatchEvent(new Event('change')); }");
        await page.Page.Locator("button.genre-edit").EvaluateAsync("b => b.click()");
        var dialog = page.Page.Locator("dialog.genre-dialog");
        await page.Until(() => dialog.IsVisibleAsync(), "the editor open");
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
