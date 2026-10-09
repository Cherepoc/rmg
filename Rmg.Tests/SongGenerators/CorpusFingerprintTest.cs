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

    /// <summary>Each corpus song's hash, by its seed, so that two builds' lists compared count the songs a change changed.</summary>
    [Test]
    [Explicit]
    public async Task ReportBySong()
    {
        foreach (var (seed, file) in Enumerable.Range(0, SongFingerprint.CorpusSize).AsParallel().AsOrdered().Select(seed => (seed, SongFingerprint.Write(seed, Rmg.Core.Composition.SongOverrides.None))))
            Console.WriteLine($"song {seed} {Convert.ToHexString(SHA256.HashData(file))[..16]}");
        await Task.CompletedTask;
    }

    /// <summary>The songs of every preset of the page's, a few seeds each, which the corpus does not play, to show that a refactor leaves them as they were.</summary>
    [Test]
    [Explicit]
    public async Task ReportGenres()
    {
        var text = new StringBuilder();
        foreach (var preset in Genres.GenrePresets.All)
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 4), seed => TestCorpus.Get(seed, new Rmg.Core.Composition.SongOverrides(Genre: Genres.GenrePresets.Genre(preset.Id)))))
        foreach (var (track, notes) in song.Song.Notes!)
        foreach (var note in notes)
            text.Append($"{preset.Id} {track} {note.Position:R} {note.Value.Velocity:R} {note.Value.Duration:R} {string.Join(",", note.Value.Pitches)};");

        Console.WriteLine($"Genres' fingerprint: {Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..16]}");
        await Task.CompletedTask;
    }

    /// <summary>The corpus's notes but the melody's, to show that a change to the melody alone leaves the rest.</summary>
    [Test]
    [Explicit]
    public async Task ReportWithoutMelody()
    {
        var text = new StringBuilder();
        foreach (var song in TestCorpus.Range(SongFingerprint.CorpusSize))
        foreach (var (track, notes) in song.Song.Notes!.Where(x => x.Key != song.Track(TrackRole.Melody)))
        foreach (var note in notes)
            text.Append($"{track} {note.Position:R} {note.Value.Velocity:R} {note.Value.Duration:R} {string.Join(",", note.Value.Pitches)};");

        Console.WriteLine($"Corpus fingerprint without the melody: {Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..16]}");
        await Task.CompletedTask;
    }
}
