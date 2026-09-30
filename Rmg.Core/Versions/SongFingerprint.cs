using System.Security.Cryptography;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;

namespace Rmg.Core.Versions;

/// <summary>
///     A fingerprint of the songs: a hash of the MIDI files of the corpus's seeds, written with no label, so that it
///     changes when the songs do, their notes, tempo or fades, and only then. A refactor keeps it; the deploy bumps the
///     songs' version when it changes (<see cref="SongsVersion" />).
/// </summary>
public static class SongFingerprint
{
    /// <summary>How many seeds, from 0, the corpus has, which the deploy and the corpus report hash.</summary>
    public const int CorpusSize = 256;

    /// <summary>The fingerprint of the songs of seeds 0 to <paramref name="count" /> - 1, as 16 hex digits.</summary>
    public static string Compute(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        // generated side by side, hashed in the seeds' order
        foreach (var file in Enumerable.Range(0, count).AsParallel().AsOrdered().Select(Write))
            hash.AppendData(file);

        return Convert.ToHexString(hash.GetHashAndReset())[..16];
    }

    private static byte[] Write(int seed)
    {
        using var stream = new MemoryStream();
        Render.RenderSong(SongGenerator.GenerateSong((ulong)seed)).Write(stream, null);
        return stream.ToArray();
    }
}
