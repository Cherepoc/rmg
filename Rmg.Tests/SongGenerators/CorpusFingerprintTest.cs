using System.Security.Cryptography;
using System.Text;
using Rmg.Core.Versions;

namespace Rmg.Tests.SongGenerators;

/// <summary>A fingerprint of the corpus, to show that a change leaves the songs as they were.</summary>
public sealed class CorpusFingerprintTest
{
    /// <summary>The songs' fingerprint, which the deploy compares with the file VERSION.</summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        Console.WriteLine($"Corpus fingerprint: {SongFingerprint.Compute(SongFingerprint.CorpusSize)}");
        await Task.CompletedTask;
    }

    /// <summary>The corpus's notes but the melody's, to show that a change to the melody alone leaves the rest.</summary>
    [Test]
    [Explicit]
    public async Task ReportWithoutMelody()
    {
        var text = new StringBuilder();
        foreach (var song in TestCorpus.Range(SongFingerprint.CorpusSize))
        foreach (var (track, notes) in song.Song.Notes!.Where(x => x.Key != Rmg.Core.Composition.SongTracks.MelodyTrack))
        foreach (var note in notes)
            text.Append($"{track} {note.Position:R} {note.Value.Velocity:R} {note.Value.Duration:R} {string.Join(",", note.Value.Pitches)};");

        Console.WriteLine($"Corpus fingerprint without the melody: {Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..16]}");
        await Task.CompletedTask;
    }
}
