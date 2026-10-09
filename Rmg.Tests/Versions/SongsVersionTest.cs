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
    [Arguments("0.5.\u0663\u0664\u0665", false)]
    [Arguments("0.5.0000000000000000000000", false)]
    public async Task IsNumber_TakesMajorMinorAndAPatchOfThreeDigits(string text, bool expected)
    {
        await Assert.That(SongsVersion.IsNumber(text)).IsEqualTo(expected);
    }

    [Test]
    public async Task AVersionIsHeard_OnlyUpToThisOne()
    {
        await Assert.That(SongsVersion.IsHeard(SongsVersion.Number)).IsTrue();
        await Assert.That(SongsVersion.IsHeard("0.0.000")).IsTrue();
        await Assert.That(SongsVersion.IsHeard("999.0.000")).IsFalse();
        await Assert.That(SongsVersion.IsHeard("not a version")).IsFalse();
        await Assert.That(SongsVersion.Compare("0.5.1000", "0.5.999")).IsGreaterThan(0);
        await Assert.That(SongsVersion.Compare("garbage", "0.5.000")).IsLessThan(0);
    }

    [Test]
    public async Task TheLabel_IsWrittenAtTheFilesStart()
    {
        using var labelled = new MemoryStream();
        TestCorpus.Get(3).Rendered.Write(labelled, SongsVersion.Label(3));

        // the first event of the first track, after the header's 14 bytes, the track's 8 and the event's own 4
        var bytes = labelled.ToArray();
        await Assert.That(bytes.AsSpan().IndexOf(System.Text.Encoding.ASCII.GetBytes($"RMG {SongsVersion.Number}"))).IsEqualTo(26);
        await Assert.That(System.Text.Encoding.ASCII.GetString(bytes)).Contains("seed 3");
    }

    [Test]
    public async Task ASong_IsNamedByItsSeed_InLettersAndDigits()
    {
        await Assert.That(Rmg.Core.Songs.SongFile.GetName(62)).IsEqualTo("song-10.mid");
        await Assert.That(SongsVersion.Label(62)).EndsWith("seed 10");
    }

    [Test]
    public async Task TheFingerprint_IsTheSameEveryTime()
    {
        await Assert.That(SongFingerprint.Compute(1)).IsEqualTo(SongFingerprint.Compute(1));
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
