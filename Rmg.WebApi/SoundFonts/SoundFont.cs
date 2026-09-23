namespace Rmg.WebApi.SoundFonts;

/// <param name="Name">Name to show, which is the file name without its extension.</param>
/// <param name="File">Name of the file, which is what it is served under.</param>
/// <param name="Size">Size of the file in bytes, so a page can say what a download will cost.</param>
public sealed record SoundFont(string Name, string File, long Size);
