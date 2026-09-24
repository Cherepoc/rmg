namespace Rmg.WebApi.SoundFonts;

/// <param name="Name">Name to show, which is the file name without its extension.</param>
/// <param name="File">Name of the file, which is what it is served under.</param>
/// <param name="Size">Size of the file in bytes, so a page can say what a download will cost.</param>
/// <param name="License">
///     Name of the file holding the licence, served from the same directory, or null when the directory has
///     none. Serving a soundfont is distributing it, and the licences these carry ask to travel with the
///     file, so the page has somewhere to point.
/// </param>
/// <param name="IsDefault">Whether this is the one the page takes without being asked.</param>
public sealed record SoundFont(string Name, string File, long Size, string? License, bool IsDefault);
