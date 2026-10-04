using Microsoft.Playwright;

namespace Rmg.Tests.Page;

/// <summary>A genre made in the editor's popup: its sections, its checklists and lists, its parts' tabs, played, kept among one's own.</summary>
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

    /// <summary>A section of several choices, its own checkbox checked, which opens it.</summary>
    private static async Task<ILocator> Checked(ILocator dialog, string title)
    {
        var section = dialog.Locator($"details[data-section='{title}']");
        await section.Locator("summary .genre-choose input").CheckAsync();
        return section;
    }

    private static ILocator Row(ILocator within, string option) => within.Locator($".genre-row[data-option='{option}']:visible");

    /// <summary>A choice's own checkbox, its title, which names it.</summary>
    private static ILocator Choose(ILocator within, string choice) => within.Locator($".genre-choose[data-choice='{choice}'] input");

    /// <summary>A pool's options' checkboxes, those checked unchecked.</summary>
    private static async Task UncheckAll(ILocator within, string choice)
    {
        var checkedOnes = within.Locator($"[data-choice='{choice}'] .genre-row input[type=checkbox]:checked");
        while (await checkedOnes.CountAsync() > 0)
            await checkedOnes.First.ClickAsync();
    }

    private static async Task<System.Text.Json.JsonElement> Played(SongPage page, ILocator dialog)
    {
        var count = page.Requests.Count;
        await dialog.Locator("footer button", new() { HasTextString = "Play" }).ClickAsync();
        await page.Until(() => page.Requests.Count > count, "the song asked for in the genre made");
        return page.Requests[^1].Body;
    }

    [Test]
    public async Task AMeterChecked_IsTheGenres_SavedAmongOnesOwn_AndListedOnTheNextVisit()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);

        var meter = await Section(dialog, "Meter");
        await Choose(meter, "meter").CheckAsync();
        await UncheckAll(meter, "meter");
        await Row(meter, "3/4").Locator("input[type=checkbox]").CheckAsync();
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
    public async Task AChoiceUnchecked_IsAsTheSongsHaveIt_ItsOptionsHidden_AndCheckedAgainAsItWas()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var scale = await Section(dialog, "Scale");
        await Assert.That(await Row(scale, "major").CountAsync()).IsEqualTo(0);

        // checked, the scales the songs have, major among them, and one left out
        await Choose(scale, "scale").CheckAsync();
        await Assert.That(await Row(scale, "major").Locator("input[type=checkbox]").IsCheckedAsync()).IsTrue();
        await Row(scale, "major").Locator("input[type=checkbox]").UncheckAsync();
        var genre = (await Played(page, dialog)).GetProperty("genre");
        await Assert.That(genre.GetProperty("scale").TryGetProperty("major", out _)).IsFalse();
        await Assert.That(genre.GetProperty("scale").EnumerateObject().Count()).IsGreaterThan(0);

        await Choose(scale, "scale").UncheckAsync();
        await Assert.That(await Row(scale, "naturalMinor").CountAsync()).IsEqualTo(0);
        // a genre naming nothing asked for as none
        await Assert.That((await Played(page, dialog)).TryGetProperty("genre", out var none) && none.TryGetProperty("scale", out _)).IsFalse();

        await Choose(scale, "scale").CheckAsync();
        await Assert.That(await Row(scale, "major").Locator("input[type=checkbox]").IsCheckedAsync()).IsFalse();
        await Assert.That(await Row(scale, "naturalMinor").Locator("input[type=checkbox]").IsCheckedAsync()).IsTrue();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task AChoicesCheckbox_ComesBeforeItsSectionsFoldingTitle_AndCheckedOpensIt()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var meter = dialog.Locator("details[data-section='Meter']");
        await Assert.That(await meter.GetAttributeAsync("open")).IsNull();

        var title = meter.Locator("summary .genre-choose");
        await Assert.That(await title.TextContentAsync()).IsEqualTo("Meter");

        // the name folds the section, as a title does, and checks nothing
        await title.Locator("span").ClickAsync(new() { Force = true });
        await Assert.That(await meter.GetAttributeAsync("open")).IsNotNull();
        await Assert.That(await title.Locator("input").IsCheckedAsync()).IsFalse();
        await title.Locator("span").ClickAsync(new() { Force = true });
        await Assert.That(await meter.GetAttributeAsync("open")).IsNull();

        // the checkbox, before the folding mark, checks it and opens the section
        var box = await title.Locator("input").BoundingBoxAsync();
        var name = await title.Locator("span").BoundingBoxAsync();
        await Assert.That(box!.X).IsLessThan(name!.X);
        await title.Locator("input").CheckAsync();
        await Assert.That(await meter.GetAttributeAsync("open")).IsNotNull();
        await Assert.That(await Row(meter, "4/4").Locator("input[type=checkbox]").IsCheckedAsync()).IsTrue();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task APoolKeepingNone_IsAProblem_NotPlayed()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var meter = await Section(dialog, "Meter");
        await Choose(meter, "meter").CheckAsync();
        await UncheckAll(meter, "meter");

        await Assert.That(await dialog.Locator("footer .warning").TextContentAsync()).IsEqualTo("Meter keeps none of its options.");
        await Assert.That(await dialog.Locator("footer button", new() { HasTextString = "Play" }).IsDisabledAsync()).IsTrue();
        await Row(meter, "4/4").Locator("input[type=checkbox]").CheckAsync();
        await Assert.That(await dialog.Locator("footer .warning").IsHiddenAsync()).IsTrue();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task AWeightShowsItsAmount_OnStepsFinerAtTheLowEnd_AChanceItsChanceInAMiddlingSong_AndTheTemposEndsInBeatsAMinute()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);

        var playing = await Checked(dialog, "Parts playing");
        // every song has a melody, and a quarter of the middling ones in a genre where it is set so
        var melody = Row(playing, "melody");
        await Assert.That(await melody.Locator("output").TextContentAsync()).IsEqualTo("100%");
        await melody.Locator("input[type=range]").FillAsync("12");
        await Assert.That(await melody.Locator("output").TextContentAsync()).IsEqualTo("25%");

        var meter = await Section(dialog, "Meter");
        await Choose(meter, "meter").CheckAsync();
        var fourFour = Row(meter, "4/4");
        // on steps finer at the low end, 1, 2, 3 at the first, and by fives from 20
        await Assert.That(await fourFour.Locator("output").TextContentAsync()).IsEqualTo("100");
        await fourFour.Locator("input[type=range]").FillAsync("17");
        await Assert.That(await fourFour.Locator("output").TextContentAsync()).IsEqualTo("50");
        await fourFour.Locator("input[type=range]").FillAsync("1");
        await Assert.That(await fourFour.Locator("input[type=range]").GetAttributeAsync("aria-valuetext")).IsEqualTo("1");

        var tempo = await Section(dialog, "Tempo");
        await tempo.Locator(".genre-check input").First.CheckAsync();
        await Assert.That(await tempo.Locator(".genre-slider").First.Locator(".end").AllTextContentsAsync()).IsEquivalentTo(["60 bpm", "240 bpm"]);
        await Assert.That(await tempo.Locator(".genre-slider output").CountAsync()).IsEqualTo(0);
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task SwingAndHalfTime_HaveACheckboxOfTheirOwn_NamingEveryOneAsTheSongsHaveIt_AndTheirSlidersNoEndWords()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var swing = dialog.Locator("details[data-section='Swing and half time']");
        var title = swing.Locator("summary .genre-choose input");
        await Assert.That(await title.IsCheckedAsync()).IsFalse();

        await title.CheckAsync();
        await Assert.That(await swing.GetAttributeAsync("open")).IsNotNull();
        await Assert.That(await Row(swing, "halfTime").Locator("input[type=checkbox]").IsCheckedAsync()).IsTrue();
        await Assert.That(await swing.Locator(".genre-slider .end").CountAsync()).IsEqualTo(0);
        var genre = (await Played(page, dialog)).GetProperty("genre");
        await Assert.That(genre.GetProperty("feelChances").TryGetProperty("swing", out _)).IsTrue();
        await Assert.That(genre.GetProperty("feelValues").TryGetProperty("swing", out _)).IsTrue();

        // every chance left out, never, and every value as it is
        await UncheckAll(swing, "feelChances");
        await UncheckAll(swing, "feelValues");
        await Assert.That(await title.IsCheckedAsync()).IsTrue();
        var never = (await Played(page, dialog)).GetProperty("genre");
        await Assert.That(never.GetProperty("feelChances").EnumerateObject().Count()).IsEqualTo(0);
        await Assert.That(never.TryGetProperty("feelValues", out _)).IsFalse();

        await title.UncheckAsync();
        await Assert.That(await Row(swing, "swing").CountAsync()).IsEqualTo(0);
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task ThePartsPlayingAndTheParts_HaveACheckboxEach_ThePartsCheckedNamingNoneTillAPartsChoiceIs_AndUncheckedLettingThemGo()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);

        var playing = dialog.Locator("details[data-section='Parts playing']");
        await playing.Locator("summary .genre-choose input").CheckAsync();
        await Assert.That(await Row(playing, "melody").Locator("input[type=checkbox]").IsCheckedAsync()).IsTrue();

        var parts = dialog.Locator("details[data-section='Parts']");
        var title = parts.Locator("summary .genre-choose input");
        await Assert.That(await parts.GetByRole(AriaRole.Tab, new() { Name = "Bass", Exact = true }).IsVisibleAsync()).IsFalse();
        await title.CheckAsync();
        await parts.GetByRole(AriaRole.Tab, new() { Name = "Bass", Exact = true }).ClickAsync();
        await Choose(parts, "instrument.bass").CheckAsync();
        var genre = (await Played(page, dialog)).GetProperty("genre");
        await Assert.That(genre.TryGetProperty("instrument.bass", out _)).IsTrue();
        await Assert.That(genre.GetProperty("parts").TryGetProperty("melody", out _)).IsTrue();

        await title.UncheckAsync();
        await Assert.That((await Played(page, dialog)).GetProperty("genre").TryGetProperty("instrument.bass", out _)).IsFalse();
        await title.CheckAsync();
        await Assert.That((await Played(page, dialog)).GetProperty("genre").TryGetProperty("instrument.bass", out _)).IsTrue();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task ASectionsFields_AreAStepInFromItsCheckbox()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var meter = dialog.Locator("details[data-section='Meter']");
        await meter.Locator("summary .genre-choose input").CheckAsync();

        var title = await meter.Locator("summary .genre-choose input").BoundingBoxAsync();
        var option = await Row(meter, "4/4").Locator("input[type=checkbox]").BoundingBoxAsync();
        await Assert.That(option!.X - title!.X).IsGreaterThan(10);
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task AScaleAndAChordShape_SayHowTheySound_OverTheirNames()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var scale = await Section(dialog, "Scale");
        await Choose(scale, "scale").CheckAsync();
        await Assert.That(await Row(scale, "dorian").GetAttributeAsync("title")).IsEqualTo("Minor with a raised sixth: dark but hopeful, at home in funk, folk and jazz. On C: C D E♭ F G A B♭.");

        var shapes = await Section(dialog, "Chord shapes");
        await Choose(shapes, "chordShape").CheckAsync();
        await Assert.That(await Row(shapes, "triad").GetAttributeAsync("title")).IsEqualTo("Root, third and fifth: the plain chord. On C: C E♭/E G.");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task ASliderDoubleClicked_GoesBackToItsDefault()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var meter = await Section(dialog, "Meter");
        await Choose(meter, "meter").CheckAsync();
        var weight = Row(meter, "4/4").Locator("input[type=range]");
        await weight.FillAsync("10");

        await weight.DblClickAsync(new() { Position = new Position { X = 3, Y = 5 } });

        await Assert.That(await weight.InputValueAsync()).IsEqualTo("27");
        await Assert.That((await Played(page, dialog)).GetProperty("genre").GetProperty("meter").GetProperty("4/4").GetInt32()).IsEqualTo(27);
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task TheGenre_IsExportedAsAJsonFile_ByItsName()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        await Assert.That(await dialog.Locator("details[data-section='As JSON']").CountAsync()).IsEqualTo(0);
        await dialog.Locator("input[aria-label='Genre name']").FillAsync("Waltzes");
        var meter = await Section(dialog, "Meter");
        await Choose(meter, "meter").CheckAsync();

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

        await file.SetInputFilesAsync(new FilePayload { Name = "waltzes.json", MimeType = "application/json", Buffer = System.Text.Encoding.UTF8.GetBytes("""{ "name": "Waltzes", "choices": { "meter": { "3/4": 27 } } }""") });

        await Assert.That(await dialog.Locator("input[aria-label='Genre name']").InputValueAsync()).IsEqualTo("Waltzes");
        var meter = dialog.Locator("details[data-section='Meter']");
        await Assert.That(await meter.GetAttributeAsync("open")).IsNotNull();
        await Assert.That(await Row(meter, "3/4").Locator("input[type=checkbox]").IsCheckedAsync()).IsTrue();
        await Assert.That(await Row(meter, "4/4").Locator("input[type=checkbox]").IsCheckedAsync()).IsFalse();

        await file.SetInputFilesAsync(new FilePayload { Name = "other.json", MimeType = "application/json", Buffer = System.Text.Encoding.UTF8.GetBytes("""{ "name": "Other", "choices": { "nothing": {} } }""") });
        await Assert.That(await dialog.Locator("footer .warning").TextContentAsync()).IsEqualTo("That file is not a genre: a name and the choices the editor knows.");
        await Assert.That(await dialog.Locator("input[aria-label='Genre name']").InputValueAsync()).IsEqualTo("Waltzes");
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task TheRiffsTwin_IsUncheckedAndDisabled_WhileTheRiffIs()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var playing = await Checked(dialog, "Parts playing");
        var twin = Row(playing, "riffTwin").Locator("input[type=checkbox]");
        await Assert.That(await twin.IsCheckedAsync()).IsTrue();

        await Row(playing, "riff").Locator("input[type=checkbox]").UncheckAsync();
        await Assert.That(await twin.IsCheckedAsync()).IsFalse();
        await Assert.That(await twin.IsDisabledAsync()).IsTrue();
        var parts = (await Played(page, dialog)).GetProperty("genre").GetProperty("parts");
        await Assert.That(parts.TryGetProperty("riffTwin", out _)).IsFalse();

        await Row(playing, "riff").Locator("input[type=checkbox]").CheckAsync();
        await Assert.That(await twin.IsDisabledAsync()).IsFalse();
        await Assert.That(await twin.IsCheckedAsync()).IsFalse();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task APartsTab_ItsInstrumentsSwappedInTheirLists_AndTheOtherPartsAsTheSongsHaveThem()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var parts = await Checked(dialog, "Parts");

        await parts.GetByRole(AriaRole.Tab, new() { Name = "Bass", Exact = true }).ClickAsync();
        await Choose(parts, "instrument.bass").CheckAsync();
        var rows = await parts.Locator(".genre-rows .genre-row:visible").CountAsync();
        await Assert.That(rows).IsGreaterThan(0);
        await parts.Locator(".genre-rows .genre-row:visible select").First.SelectOptionAsync("tuba");

        await parts.GetByRole(AriaRole.Tab, new() { Name = "Melody", Exact = true }).ClickAsync();
        await Assert.That(await parts.Locator(".genre-rows .genre-row:visible").CountAsync()).IsEqualTo(0);
        await Assert.That(await parts.Locator("[role=tab].named").First.TextContentAsync()).IsEqualTo("Bass");

        var genre = (await Played(page, dialog)).GetProperty("genre");
        await Assert.That(genre.GetProperty("instrument.bass").TryGetProperty("tuba", out _)).IsTrue();
        await Assert.That(genre.TryGetProperty("instrument.melody", out _)).IsFalse();
        await Assert.That(page.Errors).IsEmpty();
    }

    [Test]
    public async Task TheDrumsSetupsAndRoles_KeepToThePiecesChosen()
    {
        await using var page = await SongPage.Open(site, "?song=1");
        var dialog = await OpenEditor(page);
        var parts = await Checked(dialog, "Parts");
        await parts.GetByRole(AriaRole.Tab, new() { Name = "Drums", Exact = true }).ClickAsync();

        // congas and bongos alone, percussion: no kit to set up, and only they to take a role
        var pieces = parts.Locator(".genre-sliders[data-choice='drumPieces']");
        await Choose(parts, "drumPieces").CheckAsync();
        await UncheckAll(parts, "drumPieces");
        await Row(pieces, "conga").Locator("input[type=checkbox]").CheckAsync();
        await Row(pieces, "bongo").Locator("input[type=checkbox]").CheckAsync();
        var setup = parts.Locator(".genre-sliders[data-choice='drumSetup']");
        await Choose(parts, "drumSetup").CheckAsync();
        await Assert.That(await setup.Locator(".genre-row").EvaluateAllAsync<string[]>("rows => rows.map(x => x.dataset.option)")).IsEquivalentTo(["percussion"]);
        var ground = parts.Locator(".genre-sliders[data-choice='drumRole.ground']");
        await Choose(parts, "drumRole.ground").CheckAsync();
        await Assert.That(await ground.Locator(".genre-row").EvaluateAllAsync<string[]>("rows => rows.map(x => x.dataset.option)")).IsEquivalentTo(["bongo", "conga"]);
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
        await page.Page.ScreenshotAsync(new() { Path = Path.Combine(directory, "genre-editor-sections.png") });
        var parts = await Checked(dialog, "Parts");
        await parts.ScrollIntoViewIfNeededAsync();
        await page.Page.ScreenshotAsync(new() { Path = Path.Combine(directory, "genre-editor-melody.png") });
        await parts.GetByRole(AriaRole.Tab, new() { Name = "Drums", Exact = true }).ClickAsync();
        await parts.Locator(".genre-sliders[data-choice='drumSetup']").ScrollIntoViewIfNeededAsync();
        await page.Page.ScreenshotAsync(new() { Path = Path.Combine(directory, "genre-editor-drums.png") });
    }
}
