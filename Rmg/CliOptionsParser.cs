using System.Globalization;

namespace Rmg;

public sealed record CliParseResult(CliOptions? Options, string? Error, bool ShowHelp)
{
    public static CliParseResult Help { get; } = new(null, null, true);

    public static CliParseResult Success(CliOptions options)
    {
        return new CliParseResult(options, null, false);
    }

    public static CliParseResult Failure(string error)
    {
        return new CliParseResult(null, error, false);
    }
}

public static class CliOptionsParser
{
    public const string DefaultOutputDirectory = "songs";
    public const int DefaultCount = 1;

    public const string Usage = """
        Usage: rmg [options]

        Options:
          -o, --output <dir>   Directory to write the songs to (default: ./songs)
          -n, --count <n>      Number of songs to generate, at least 1 (default: 1)
          -s, --seed <n>       Seed of the randomizer that provides a seed for every song
                               (default: random, printed so the run can be repeated)
          -h, --help           Show this help
        """;

    /// <param name="args">Command line arguments.</param>
    /// <param name="defaultSeedFactory">Provides the seed when none is passed. Random by default.</param>
    public static CliParseResult Parse(string[] args, Func<int>? defaultSeedFactory = null)
    {
        var outputDirectory = DefaultOutputDirectory;
        var count = DefaultCount;
        int? seed = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is "-h" or "--help" or "-?")
                return CliParseResult.Help;

            var name = arg;
            string? inlineValue = null;
            if (arg.StartsWith("--", StringComparison.Ordinal) && arg.IndexOf('=') is var equalsIndex and > 0)
            {
                name = arg[..equalsIndex];
                inlineValue = arg[(equalsIndex + 1)..];
            }

            switch (name)
            {
                case "-o" or "--output":
                    if (!TryReadValue(args, ref i, name, inlineValue, out var outputValue, out var outputError))
                        return CliParseResult.Failure(outputError);
                    if (string.IsNullOrWhiteSpace(outputValue))
                        return CliParseResult.Failure($"Option '{name}' must not be empty.");
                    outputDirectory = outputValue;
                    break;

                case "-n" or "--count":
                    if (!TryReadInt(args, ref i, name, inlineValue, out var countValue, out var countError))
                        return CliParseResult.Failure(countError);
                    if (countValue < 1)
                        return CliParseResult.Failure($"Option '{name}' must be at least 1.");
                    count = countValue;
                    break;

                case "-s" or "--seed":
                    if (!TryReadInt(args, ref i, name, inlineValue, out var seedValue, out var seedError))
                        return CliParseResult.Failure(seedError);
                    seed = seedValue;
                    break;

                default:
                    return CliParseResult.Failure(
                        arg.StartsWith('-')
                            ? $"Unknown option '{arg}'."
                            : $"Unexpected argument '{arg}'."
                    );
            }
        }

        seed ??= (defaultSeedFactory ?? (() => Random.Shared.Next()))();
        return CliParseResult.Success(new CliOptions(outputDirectory, count, seed.Value));
    }

    private static bool TryReadValue(
        string[] args,
        ref int index,
        string name,
        string? inlineValue,
        out string value,
        out string error
    )
    {
        error = string.Empty;
        if (inlineValue is not null)
        {
            value = inlineValue;
            return true;
        }

        // the next argument is always the value, so negative numbers like "--seed -5" work
        if (index + 1 < args.Length)
        {
            value = args[++index];
            return true;
        }

        value = string.Empty;
        error = $"Option '{name}' requires a value.";
        return false;
    }

    private static bool TryReadInt(
        string[] args,
        ref int index,
        string name,
        string? inlineValue,
        out int value,
        out string error
    )
    {
        value = 0;
        if (!TryReadValue(args, ref index, name, inlineValue, out var text, out error))
            return false;

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            return true;

        error = $"Invalid value '{text}' for '{name}': expected a 32-bit integer.";
        return false;
    }
}
