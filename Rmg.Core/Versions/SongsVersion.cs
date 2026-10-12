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
    public static string Label(ulong seed)
    {
        var name = Base62.FromSeed(seed);
        return Commit is null ? $"RMG {Number}, seed {name}" : $"RMG {Number} ({Commit[..Math.Min(7, Commit.Length)]}), seed {name}";
    }

    /// <summary>Whether a text is a number of the version's form, in ASCII digits and nine at most a part.</summary>
    public static bool IsNumber(string? text)
    {
        return text is not null && NumberPattern().IsMatch(text);
    }

    /// <summary>
    ///     Whether a text is a version the analytics accept: a number of the form, and none after this one, which no page
    ///     this server served can have heard.
    /// </summary>
    public static bool IsHeard(string? text) => IsNumber(text) && Compare(text!, Number) <= 0;

    /// <summary>Two numbers by their parts, so that 0.5.1000 comes after 0.5.999, where text would put it before; a text not of the form first.</summary>
    public static int Compare(string first, string second)
    {
        static long[] Parts(string text) => IsNumber(text) ? [..text.Split('.').Select(long.Parse)] : [];
        var (a, b) = (Parts(first), Parts(second));
        if (a.Length == 0 || b.Length == 0)
            return a.Length.CompareTo(b.Length);
        return a.Zip(b, (x, y) => x.CompareTo(y)).FirstOrDefault(x => x != 0);
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

    [GeneratedRegex(@"^[0-9]{1,9}\.[0-9]{1,9}\.[0-9]{3,9}\z")]
    private static partial Regex NumberPattern();
}
