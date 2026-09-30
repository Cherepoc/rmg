using System.Text.Json;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;
using Rmg.Core.Versions;

namespace Rmg.WebApi.Songs;

public static class SongEndpoints
{
    public static void MapSongs(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/songs/generate", GenerateSong);

        // what the page shows as RMG's version
        routes.MapGet("/api/version", () => Results.Ok(new VersionResponse(SongsVersion.Number, SongsVersion.Commit)));
    }

    private static readonly JsonSerializerOptions HeaderJson = new(JsonSerializerDefaults.Web);

    /// <summary>
    ///     Writes a song as a MIDI file. What it is made of is reported in the headers, since the body is the file
    ///     itself.
    /// </summary>
    private static IResult GenerateSong(HttpContext context, GenerateSongRequest? request)
    {
        request ??= new GenerateSongRequest();

        if (!request.TryRead(out var seed, out var overrides, out var mix, out var error))
            return Results.BadRequest(new ErrorResponse(error!));

        var songSeed = seed ?? Seeds.Random();

        MemoryStream stream;
        SongReport report;
        try
        {
            var song = SongGenerator.GenerateSong(songSeed, overrides);
            var renderedSong = Render.RenderSong(song, mix);
            report = SongReport.Of(song, renderedSong, overrides);

            stream = new MemoryStream();
            renderedSong.Write(stream, SongsVersion.Label(songSeed));
            stream.Seek(0, SeekOrigin.Begin);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Song {Base62.FromSeed(songSeed)}: {ex.Message}", statusCode: 500);
        }

        // lets the page show and reuse the seed it actually got when it asked for a random one
        context.Response.Headers["X-Song-Seed"] = Base62.FromSeed(songSeed);

        // and which songs' version it is, which the page reports with what it tells of the song
        context.Response.Headers["X-Song-Version"] = SongsVersion.Number;

        // and everything the song drew or was given, and which channel plays which part, for the page to show and
        // set before a note of it is heard
        context.Response.Headers["X-Song-Settings"] = JsonSerializer.Serialize(report, HeaderJson);

        return Results.File(stream, "audio/midi", SongFile.GetName(songSeed));
    }
}

/// <summary>RMG's version: the songs' number, which goes up when the songs change, and the commit it is built from.</summary>
public sealed record VersionResponse(string Version, string? Commit);
