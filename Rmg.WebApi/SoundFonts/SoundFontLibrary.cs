using System.Collections.Immutable;

namespace Rmg.WebApi.SoundFonts;

/// <summary>
///     The soundfonts the server offers. No soundfont is shipped with the project, because the good ones
///     are large and the well known ones are not all redistributable: the library holds whatever is dropped
///     in its directory, which is the operator's call.
/// </summary>
public sealed class SoundFontLibrary
{
    public const string DirectoryName = "soundfonts";

    public static readonly ImmutableArray<string> Extensions = [".sf2", ".sf3", ".sfogg", ".dls"];

    /// <summary>What a licence is written in, which is what the soundfonts themselves ship theirs as.</summary>
    public static readonly ImmutableArray<string> LicenseExtensions = [".md", ".txt"];

    /// <summary>How the name of a licence file ends, before its extension, whatever it is a licence for.</summary>
    private static readonly ImmutableArray<string> LicenseWords = ["license", "licence", "copyright"];

    /// <summary>What may stand between the name a licence is for and the word saying it is a licence.</summary>
    private static readonly char[] LicenseSeparators = ['_', '-', '.', ' '];

    private SoundFontLibrary(string directory, string? preferred)
    {
        Directory = directory;
        Preferred = preferred;
    }

    public string Directory { get; }

    /// <summary>The name the page should take unasked, or null to leave it to take the first offered.</summary>
    public string? Preferred { get; }

    /// <summary>
    ///     Takes the directory it is given, or the one under <paramref name="webRootPath" /> when it is
    ///     given none, and creates it if it is not there.
    /// </summary>
    /// <param name="preferred">
    ///     The soundfont the page should take unasked, named with or without its extension. A name that
    ///     matches nothing in the directory is left to say so at startup rather than quietly ignored.
    /// </param>
    public static SoundFontLibrary Create(string webRootPath, string? directory = null, string? preferred = null)
    {
        var path = directory is null ? Path.Combine(webRootPath, DirectoryName) : Path.GetFullPath(directory);
        System.IO.Directory.CreateDirectory(path);

        return new SoundFontLibrary(path, preferred);
    }

    public IEnumerable<SoundFont> List()
    {
        var files = new DirectoryInfo(Directory).EnumerateFiles().ToArray();
        var licenses = FindLicenses(files);

        var offered = files
            .Where(file => Extensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .Select(file =>
            {
                var name = Path.GetFileNameWithoutExtension(file.Name);
                return new SoundFont(name, file.Name, file.Length, FindLicense(licenses, name), false);
            })
            .ToArray();

        // the first is the default when nothing was asked for, and when what was asked for is not here
        var chosen = Array.FindIndex(offered, soundFont => IsPreferred(soundFont));
        if (chosen < 0 && offered.Length > 0) chosen = 0;
        if (chosen >= 0) offered[chosen] = offered[chosen] with { IsDefault = true };

        return offered;
    }

    /// <summary>Named either way round: <c>MuseScore_General</c> or <c>MuseScore_General.sf3</c>.</summary>
    private bool IsPreferred(SoundFont soundFont)
    {
        return Preferred is not null
               && (Preferred.Equals(soundFont.Name, StringComparison.OrdinalIgnoreCase)
                   || Preferred.Equals(soundFont.File, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether a name was asked for that the directory does not hold, which is worth saying.</summary>
    public bool IsPreferredMissing()
    {
        return Preferred is not null && !List().Any(soundFont => IsPreferred(soundFont));
    }

    /// <summary>
    ///     Pairs every licence file in the directory with the name it is a licence for, which is what is left
    ///     once the word saying it is a licence is taken off: <c>FluidR3Mono_License.md</c> is a licence for
    ///     anything called <c>FluidR3Mono</c> and then some. Longest first, so the most particular one wins.
    /// </summary>
    private static ImmutableArray<(string Stem, string File)> FindLicenses(IEnumerable<FileInfo> files)
    {
        return files
            .Select(file => (Stem: ReadStem(file), file.Name))
            .Where(candidate => candidate.Stem is not null)
            .Select(candidate => (Stem: candidate.Stem!, File: candidate.Name))
            .OrderByDescending(license => license.Stem.Length)
            .ToImmutableArray();
    }

    /// <summary>The name a licence file is a licence for, or null when the file is not a licence at all.</summary>
    private static string? ReadStem(FileInfo file)
    {
        if (!LicenseExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase)) return null;

        var name = Path.GetFileNameWithoutExtension(file.Name);

        foreach (var word in LicenseWords)
        {
            if (!name.EndsWith(word, StringComparison.OrdinalIgnoreCase)) continue;

            return name[..^word.Length].TrimEnd(LicenseSeparators);
        }

        return null;
    }

    /// <summary>
    ///     The licence for <paramref name="name" />, which is the longest stem its name starts with. A file
    ///     called nothing but <c>License.md</c> leaves an empty stem and so stands for the whole directory,
    ///     which is how one notice covers several soundfonts.
    /// </summary>
    private static string? FindLicense(ImmutableArray<(string Stem, string File)> licenses, string name)
    {
        foreach (var (stem, file) in licenses)
            if (name.StartsWith(stem, StringComparison.OrdinalIgnoreCase))
                return file;

        return null;
    }
}
