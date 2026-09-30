using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Probabilities;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;
using Rmg.Core.Versions;

namespace Rmg;

public static class SongBatch
{
    public const int Success = 0;
    public const int SongsFailed = 1;
    public const int InvalidOutput = 2;

    /// <summary>
    ///     Draws the seed of every song from a randomizer seeded with <paramref name="seed" />,
    ///     so the first <c>n</c> songs of a batch don't depend on the batch size.
    /// </summary>
    public static ImmutableArray<ulong> GenerateSongSeeds(ulong seed, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var context = new GenerationContext(seed);
        var seeds = ImmutableArray.CreateBuilder<ulong>(count);
        for (var i = 0; i < count; i++)
            seeds.Add(context.GenerateSeed());

        return seeds.MoveToImmutable();
    }

    /// <returns>Process exit code.</returns>
    public static int Run(CliOptions options, Func<ulong, Song> generateSong, TextWriter output, TextWriter error)
    {
        try
        {
            Directory.CreateDirectory(options.OutputDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            error.WriteLine($"Cannot use output directory '{options.OutputDirectory}': {ex.Message}");
            return InvalidOutput;
        }

        output.WriteLine($"Seed: {Base64.FromSeed(options.Seed)}");

        var songSeeds = GenerateSongSeeds(options.Seed, options.Count);
        var failedCount = 0;
        for (var i = 0; i < songSeeds.Length; i++)
        {
            var index = i + 1;
            var songSeed = songSeeds[i];
            var path = Path.Combine(options.OutputDirectory, SongFile.GetName(songSeed));
            try
            {
                var renderedSong = Render.RenderSong(generateSong(songSeed));

                // written in one go so a failing song never leaves a partial file behind
                using var stream = new MemoryStream();
                renderedSong.Write(stream, SongsVersion.Label(songSeed));
                File.WriteAllBytes(path, stream.ToArray());

                output.WriteLine($"[{index}/{options.Count}] seed {Base64.FromSeed(songSeed)} saved to {path}");
            }
            catch (Exception ex)
            {
                failedCount++;
                error.WriteLine($"[{index}/{options.Count}] seed {Base64.FromSeed(songSeed)} failed: {ex.Message}");
            }
        }

        return failedCount == 0 ? Success : SongsFailed;
    }
}
