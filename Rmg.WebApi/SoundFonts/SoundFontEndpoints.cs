namespace Rmg.WebApi.SoundFonts;

public static class SoundFontEndpoints
{
    public static void MapSoundFonts(this IEndpointRouteBuilder routes, SoundFontLibrary library)
    {
        routes.MapGet("/api/soundfonts", () => Results.Ok(library.List()));
    }
}
