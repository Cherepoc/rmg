using System.Collections.Immutable;
using System.Security.Cryptography;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;

namespace Rmg.Core.Versions;

/// <summary>
///     A fingerprint of the songs: a hash of the MIDI files of the corpus's seeds, written with no label, and of a few of
///     them played with values supplied (<see cref="Overrides" />), so that it changes when the songs do, their notes, tempo
///     or fades, those of a song with a facet at an end or a tempo given too, and only then. A refactor keeps it; the
///     deploy bumps the songs' version when it changes (<see cref="SongsVersion" />), and when the page's genres do.
/// </summary>
public static class SongFingerprint
{
    /// <summary>How many seeds, from 0, the corpus has, which the deploy and the corpus report hash.</summary>
    public const int CorpusSize = 256;

    /// <summary>How many seeds, from 0, are played with each of the values supplied.</summary>
    public const int OverrideSeeds = 8;

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
        new(DrumSetup: DrumSetup.Percussion)
    ];

    /// <summary>The fingerprint of the songs of seeds 0 to <paramref name="count" /> - 1, as 16 hex digits.</summary>
    public static string Compute(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        // generated side by side, hashed in the seeds' order, and then the songs of values supplied
        foreach (var file in Enumerable.Range(0, count).AsParallel().AsOrdered().Select(seed => Write(seed, SongOverrides.None)))
            hash.AppendData(file);
        foreach (var file in Overrides.SelectMany(x => Enumerable.Range(0, OverrideSeeds).Select(seed => (Seed: seed, Overrides: x))).AsParallel().AsOrdered()
                     .Select(x => Write(x.Seed, x.Overrides)))
            hash.AppendData(file);

        return Convert.ToHexString(hash.GetHashAndReset())[..16];
    }

    /// <summary>A song's MIDI file as the fingerprint hashes it, without the build's label.</summary>
    internal static byte[] Write(int seed, SongOverrides overrides)
    {
        using var stream = new MemoryStream();
        Render.RenderSong(SongGenerator.GenerateSong((ulong)seed, overrides)).Write(stream, null);
        return stream.ToArray();
    }
}
