namespace Rmg;

/// <param name="OutputDirectory">Directory the songs are written to.</param>
/// <param name="Count">Number of songs to generate.</param>
/// <param name="Seed">
///     Seed of the randomizer that produces the seed of every song, so one value reproduces the whole batch.
/// </param>
public sealed record CliOptions(string OutputDirectory, int Count, int Seed);
