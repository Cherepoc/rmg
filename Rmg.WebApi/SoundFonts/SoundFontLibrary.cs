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

    private SoundFontLibrary(string directory)
    {
        Directory = directory;
    }

    public string Directory { get; }

    /// <summary>Takes the directory under <paramref name="webRootPath" />, and creates it if it is not there.</summary>
    public static SoundFontLibrary Create(string webRootPath)
    {
        var directory = Path.Combine(webRootPath, DirectoryName);
        System.IO.Directory.CreateDirectory(directory);

        return new SoundFontLibrary(directory);
    }

    public IEnumerable<SoundFont> List()
    {
        return new DirectoryInfo(Directory)
            .EnumerateFiles()
            .Where(file => Extensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .Select(file => new SoundFont(Path.GetFileNameWithoutExtension(file.Name), file.Name, file.Length));
    }
}
