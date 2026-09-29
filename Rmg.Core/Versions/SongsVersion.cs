using System.Reflection;
using System.Text.RegularExpressions;

namespace Rmg.Core.Versions;

/// <summary>
///     RMG's version as it is heard: a number, such as 0.5.001, that goes up when the songs change, so that a seed
///     listened to or rated is known by the songs it was heard as. It is read from the file VERSION at build, beside
///     the songs' fingerprint it stands for (<see cref="SongFingerprint" />), which the deploy compares to bump the
///     number; the seeds stay free to change from one number to the next.
/// </summary>
public static partial class SongsVersion
{
    private static readonly Assembly Assembly = typeof(SongsVersion).Assembly;

    /// <summary>The number, as major.minor.patch, the patch in three digits at least.</summary>
    public static string Number { get; } = ReadNumber();

    /// <summary>The commit RMG is built from, as the build stamps it, or null where it was built outside git.</summary>
    public static string? Commit { get; } = ReadCommit();

    /// <summary>What a song's MIDI file says it is: the number, the commit and the seed.</summary>
    public static string Label(int seed)
    {
        return Commit is null ? $"RMG {Number}, seed {seed}" : $"RMG {Number} ({Commit[..Math.Min(7, Commit.Length)]}), seed {seed}";
    }

    /// <summary>Whether a text is a number of the version's form, which is all the analytics accept as one.</summary>
    public static bool IsNumber(string? text)
    {
        return text is not null && NumberPattern().IsMatch(text);
    }

    private static string ReadNumber()
    {
        var number = Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().SingleOrDefault(x => x.Key == nameof(SongsVersion))?.Value;
        if (!IsNumber(number))
            throw new InvalidOperationException($"The build stamped no songs' version of the form 0.5.000 from the file VERSION, but '{number}'.");

        return number!;
    }

    private static string? ReadCommit()
    {
        var informational = Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var plus = informational?.IndexOf('+') ?? -1;
        return plus < 0 ? null : informational![(plus + 1)..];
    }

    [GeneratedRegex(@"^\d+\.\d+\.\d{3,}$")]
    private static partial Regex NumberPattern();
}
