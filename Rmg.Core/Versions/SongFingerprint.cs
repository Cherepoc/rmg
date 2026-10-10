using System.Collections.Immutable;
using System.Security.Cryptography;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;

namespace Rmg.Core.Versions;

/// <summary>
///     A fingerprint of the songs: a hash of the MIDI files of the corpus's seeds, written with no label, and of a few of
///     them played with values supplied (<see cref="Overrides" />) and in every genre given, so that it changes when the
///     songs do, their notes, tempo or fades, those of a song with a facet at an end, a tempo or a genre given too, and only
///     then. A refactor keeps it; the
///     deploy bumps the songs' version when it changes (<see cref="SongsVersion" />), and when the page's genres do.
/// </summary>
public static class SongFingerprint
{
    /// <summary>How many seeds, from 0, the corpus has, which the deploy and the corpus report hash.</summary>
    public const int CorpusSize = 256;

    /// <summary>How many seeds, from 0, are played with each of the values supplied.</summary>
    public const int OverrideSeeds = 8;

    /// <summary>How many seeds, from 0, are played in each genre.</summary>
    public const int GenreSeeds = 4;

    /// <summary>The values supplied a few seeds are played with: the ends, facets at them, and a value of each kind given.</summary>
    private static readonly SongOverrides[] Overrides =
    [
        new(Base: 0),
        new(Base: 1),
        new(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Form, 1).Add(Facet.Melody, 1)),
        new(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Groove, 0).Add(Facet.Sound, 1)),
        new(Tempo: 1.4),
        new(MeterOption: 3),
        new(Key: 5),
        new(Swing: 0.6),
        new(Length: 120),
        new(DrumSetup: DrumSetup.Percussion),
        new(DrumPieces: ["kick", "conga", "tambourine"]),
        new(Parts: ImmutableDictionary<Songs.TrackRole, bool>.Empty.Add(Songs.TrackRole.Pad, false).Add(Songs.TrackRole.Solo, true)),
        new(Scale: Scales.Dorian, Meter: Meter.Options[2].Meter)
    ];

    /// <summary>The fingerprint of the songs of seeds 0 to <paramref name="count" /> - 1 and of <paramref name="genres" />' songs, as 16 hex digits.</summary>
    public static string Compute(int count, IReadOnlyList<(string Id, Genre Genre)> genres)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        // generated side by side, hashed in the seeds' order, and then the songs of values supplied
        foreach (var file in Enumerable.Range(0, count).AsParallel().AsOrdered().Select(seed => Write(seed, SongOverrides.None)))
            hash.AppendData(file);
        foreach (var file in Overrides.SelectMany(x => Enumerable.Range(0, OverrideSeeds).Select(seed => (Seed: seed, Overrides: x))).AsParallel().AsOrdered()
                     .Select(x => Write(x.Seed, x.Overrides)))
            hash.AppendData(file);
        foreach (var (_, file) in GenreSongs(genres))
            hash.AppendData(file);

        return Convert.ToHexString(hash.GetHashAndReset())[..16];
    }

    /// <summary>
    ///     Each song's hash of seeds 0 to <paramref name="count" /> - 1, a line a seed, "seed hash", and then of each genre's
    ///     songs, "genre/seed hash", so that two builds' lists compared count the songs a change changed.
    /// </summary>
    public static IEnumerable<string> ComputeBySong(int count, IReadOnlyList<(string Id, Genre Genre)> genres) =>
        Enumerable.Range(0, count).AsParallel().AsOrdered().Select(seed => $"{seed} {Hash(Write(seed, SongOverrides.None))}").ToList()
            .Concat(GenreSongs(genres).Select(x => $"{x.Name} {Hash(x.File)}"));

    private static string Hash(byte[] file) => Convert.ToHexString(SHA256.HashData(file))[..16];

    /// <summary>Each genre's songs of seeds 0 to <see cref="GenreSeeds" /> - 1, named "genre/seed", in order.</summary>
    private static IEnumerable<(string Name, byte[] File)> GenreSongs(IReadOnlyList<(string Id, Genre Genre)> genres) =>
        genres.SelectMany(x => Enumerable.Range(0, GenreSeeds).Select(seed => (Name: $"{x.Id}/{seed}", x.Genre, Seed: seed))).AsParallel().AsOrdered()
            .Select(x => (x.Name, Write(x.Seed, new SongOverrides(Genre: x.Genre))));

    /// <summary>A song's MIDI file as the fingerprint hashes it, without the build's label.</summary>
    private static byte[] Write(int seed, SongOverrides overrides)
    {
        using var stream = new MemoryStream();
        Render.RenderSong(SongGenerator.GenerateSong((ulong)seed, overrides)).Write(stream, null);
        return stream.ToArray();
    }
}
