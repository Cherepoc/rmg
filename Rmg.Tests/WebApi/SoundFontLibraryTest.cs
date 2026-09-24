using Rmg.WebApi.SoundFonts;

namespace Rmg.Tests.WebApi;

public sealed class SoundFontLibraryTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Test]
    public async Task DirectoryIsMadeWhenItIsNotThere()
    {
        var library = SoundFontLibrary.Create(_root);

        await Assert.That(Directory.Exists(library.Directory)).IsTrue();
        await Assert.That(library.List().ToArray()).IsEmpty();
    }

    [Test]
    public async Task OnlySoundFontsAreListed_ByName()
    {
        var library = Given("b.sf3", "a.sf2", "notes.txt", "song.mid");

        var soundFonts = library.List().ToArray();

        await Check.That(soundFonts.Select(soundFont => soundFont.File)).IsEquivalentTo(new[] { "a.sf2", "b.sf3" });
        await Assert.That(soundFonts[0].Name).IsEqualTo("a");
    }

    [Test]
    public async Task SizeIsTheFileSize()
    {
        var library = Given(("a.sf2", "12345"));

        await Assert.That(library.List().Single().Size).IsEqualTo(5);
    }

    [Test]
    public async Task SoundFontWithNoLicenceBesideItHasNone()
    {
        var library = Given("a.sf2", "MuseScore_General_License.md");

        await Assert.That(library.List().Single().License).IsNull();
    }

    [Test]
    [Arguments("MuseScore_General.sf3", "MuseScore_General_License.md")]
    [Arguments("MS Basic.sf3", "MS Basic_License.md")]
    [Arguments("GeneralUser-GS.sf2", "GeneralUser-GS-Licence.txt")]
    [Arguments("Font.sf2", "Font.Copyright.md")]
    public async Task LicenceIsFoundHoweverItIsNamed(string soundFont, string license)
    {
        var library = Given(soundFont, license);

        await Assert.That(library.List().Single().License).IsEqualTo(license);
    }

    [Test]
    public async Task LicenceCoversWhateverItsNameStarts()
    {
        // what MuseScore ships: the licence is named for the family, the soundfont for the variant
        var library = Given("FluidR3Mono_GM.sf3", "FluidR3Mono_License.md");

        await Assert.That(library.List().Single().License).IsEqualTo("FluidR3Mono_License.md");
    }

    [Test]
    public async Task ReadmeAndChangelogAreNotLicences()
    {
        var library = Given("MS Basic.sf3", "MS Basic_Readme.md", "MS_Basic_Changelog.md");

        await Assert.That(library.List().Single().License).IsNull();
    }

    [Test]
    public async Task LicenceNamedNothingElseStandsForTheWholeDirectory()
    {
        var library = Given("a.sf2", "b.sf3", "License.md");

        await Check.That(library.List().Select(soundFont => soundFont.License))
            .IsEquivalentTo(new[] { "License.md", "License.md" });
    }

    [Test]
    public async Task MostParticularLicenceWins()
    {
        var library = Given("MuseScore_General.sf3", "License.txt", "MuseScore_General_License.md");

        await Assert.That(library.List().Single().License).IsEqualTo("MuseScore_General_License.md");
    }

    [Test]
    public async Task TheFirstByNameIsTheDefaultWhenNoneIsNamed()
    {
        var library = Given("b.sf3", "a.sf2", "c.sf2");

        var offered = library.List().ToArray();

        await Assert.That(offered[0].Name).IsEqualTo("a");
        await Assert.That(offered[0].IsDefault).IsTrue();
        await Check.That(offered.Count(soundFont => soundFont.IsDefault)).IsEqualTo(1);
        await Assert.That(library.IsPreferredMissing()).IsFalse();
    }

    [Test]
    [Arguments("b")]
    [Arguments("b.sf3")]
    [Arguments("B.SF3")]
    public async Task TheNamedOneIsTheDefault_SpeltEitherWay(string preferred)
    {
        var library = GivenPreferring(preferred, "b.sf3", "a.sf2", "c.sf2");

        var chosen = library.List().Single(soundFont => soundFont.IsDefault);

        await Assert.That(chosen.Name).IsEqualTo("b");
        await Assert.That(library.IsPreferredMissing()).IsFalse();
    }

    [Test]
    public async Task ANamedOneThatIsNotThereFallsBackAndSaysSo()
    {
        var library = GivenPreferring("missing.sf2", "a.sf2", "b.sf3");

        var chosen = library.List().Single(soundFont => soundFont.IsDefault);

        await Assert.That(chosen.Name).IsEqualTo("a");
        await Assert.That(library.IsPreferredMissing()).IsTrue();
    }

    [Test]
    public async Task AnEmptyDirectoryHasNoDefaultAndNothingMissing()
    {
        var library = SoundFontLibrary.Create(_root);

        await Assert.That(library.List().ToArray()).IsEmpty();
        await Assert.That(library.IsPreferredMissing()).IsFalse();
    }

    [Test]
    public async Task ADirectoryOfItsOwnIsUsedWhenOneIsGiven()
    {
        var elsewhere = Path.Combine(_root, "elsewhere");

        var library = SoundFontLibrary.Create(Path.Combine(_root, "web"), elsewhere);
        File.WriteAllText(Path.Combine(library.Directory, "a.sf2"), "");

        await Assert.That(library.Directory).IsEqualTo(Path.GetFullPath(elsewhere));
        await Assert.That(library.List().Single().Name).IsEqualTo("a");
    }

    private SoundFontLibrary GivenPreferring(string preferred, params string[] files)
    {
        var library = SoundFontLibrary.Create(_root, null, preferred);
        foreach (var file in files) File.WriteAllText(Path.Combine(library.Directory, file), "");

        return library;
    }

    private SoundFontLibrary Given(params string[] files)
    {
        return Given(files.Select(file => (file, "")).ToArray());
    }

    private SoundFontLibrary Given(params (string File, string Content)[] files)
    {
        var library = SoundFontLibrary.Create(_root);
        foreach (var (file, content) in files) File.WriteAllText(Path.Combine(library.Directory, file), content);

        return library;
    }
}
