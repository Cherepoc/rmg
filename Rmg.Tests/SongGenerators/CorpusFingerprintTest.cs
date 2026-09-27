using System.Security.Cryptography;
using System.Text;

namespace Rmg.Tests.SongGenerators;

/// <summary>A fingerprint of the corpus's notes, to show that a change leaves the songs as they were.</summary>
public sealed class CorpusFingerprintTest
{
    [Test]
    [Explicit]
    public async Task Report()
    {
        var text = new StringBuilder();
        foreach (var song in TestCorpus.Range(200))
        foreach (var (track, notes) in song.Song.Notes!)
        foreach (var note in notes)
            text.Append($"{track} {note.Position:R} {note.Value.Velocity:R} {note.Value.Duration:R} {string.Join(",", note.Value.Pitches)};");

        Console.WriteLine($"Corpus fingerprint: {Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..16]}");
        await Task.CompletedTask;
    }
}
