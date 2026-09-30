using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;
using Rmg.Core.Versions;

namespace Rmg.Tests.Versions;

public sealed class SongsVersionTest
{
    [Test]
    public async Task TheBuild_StampsTheNumberOfTheFileVersion()
    {
        var file = await File.ReadAllLinesAsync(FindVersionFile());

        await Assert.That(SongsVersion.IsNumber(SongsVersion.Number)).IsTrue();
        await Assert.That(file).Contains($"version {SongsVersion.Number}");
    }

    [Test]
    [Arguments("0.5.000", true)]
    [Arguments("1.12.1000", true)]
    [Arguments("0.5.1", false)]
    [Arguments("0.5", false)]
    [Arguments("0.5.000-dirty", false)]
    [Arguments("", false)]
    public async Task IsNumber_TakesMajorMinorAndAPatchOfThreeDigits(string text, bool expected)
    {
        await Assert.That(SongsVersion.IsNumber(text)).IsEqualTo(expected);
    }

    [Test]
    public async Task TheLabel_IsWrittenAtTheFilesStart()
    {
        var song = Render.RenderSong(SongGenerator.GenerateSong(3));
        using var labelled = new MemoryStream();
        song.Write(labelled, SongsVersion.Label(3));

        var text = System.Text.Encoding.UTF8.GetString(labelled.ToArray());
        await Assert.That(text).Contains($"RMG {SongsVersion.Number}");
        await Assert.That(text).Contains("seed 3");
    }

    [Test]
    public async Task AGivenUnconventionality_NamesTheSong_AsItsSeedDoes()
    {
        await Assert.That(SongsVersion.Label(3, 64)).IsEqualTo($"{SongsVersion.Label(3)}, unconventionality 64/127");
        await Assert.That(SongsVersion.Label(3, null)).IsEqualTo(SongsVersion.Label(3));
        await Assert.That(Rmg.Core.Songs.SongFile.GetName(3, 64)).IsEqualTo("song-3-u64.mid");
        await Assert.That(Rmg.Core.Songs.SongFile.GetName(3, null)).IsEqualTo("song-3.mid");
    }

    [Test]
    public async Task TheFingerprint_IsTheSameEveryTime()
    {
        await Assert.That(SongFingerprint.Compute(3)).IsEqualTo(SongFingerprint.Compute(3));
        await Assert.That(SongFingerprint.Compute(3)).IsNotEqualTo(SongFingerprint.Compute(4));
    }

    private static string FindVersionFile()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "VERSION");
            if (File.Exists(path))
                return path;
        }

        throw new FileNotFoundException("No file VERSION above the tests.");
    }
}
