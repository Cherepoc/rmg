using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Core.Versions;
using Rmg.WebApi.Songs;

namespace Rmg.WebApi.Admin;

/// <summary>A song's description, as its link plays it, for the operator: in the admin area (<see cref="AdminArea" />).</summary>
public static class SongDescriptionEndpoints
{
    public static void MapSongDescriptions(this IEndpointRouteBuilder routes)
    {
        routes.MapGet($"{AdminArea.Path}/api/songs/describe", Describe);
    }

    /// <summary>
    ///     The song of a link, its seed and its settings, described as the songs make it now, waiting its turn as any song
    ///     does.
    /// </summary>
    private static async Task<IResult> Describe(HttpContext context, SongQueue queue, string? song, string? settings)
    {
        if (song is null || Base64.ToSeed(song.Trim()) is not { } seed)
            return Results.BadRequest(new ErrorResponse($"'{song}' is not a seed. A seed is up to {Base64.SeedLength} letters and digits."));

        var text = Base64.FromSeed(seed);
        SongSettings? given = null;
        if (settings is not null && (given = SongSettings.Parse(settings.Trim())) is null)
            return Results.BadRequest(new ErrorResponse($"'{settings}' are not settings this version reads."));

        if (!(given?.ToRequest(text) ?? new GenerateSongRequest(text)).TryRead(out _, out var overrides, out var mix, out var error))
            return Results.BadRequest(new ErrorResponse(error!));

        using var turn = await queue.Enter(SongQueue.ClientOf(context.Connection.RemoteIpAddress), context.RequestAborted);
        if (turn.Refusal is not null)
        {
            context.Response.Headers.RetryAfter = "1";
            return Results.Problem("The server is busy making other songs.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var description = SongDescriptions.Describe(seed, overrides, mix, context.RequestAborted);
        var settingsText = given?.Format();
        return Results.Ok(new SongDescriptionResponse(
            SongsVersion.Number,
            text,
            settingsText,
            description,
            SongDescriptionText.ToMarkdown(description, text, settingsText, SongsVersion.Number)
        ));
    }
}

/// <param name="SongsVersion">The songs' version that made the song described.</param>
/// <param name="Seed">The song's seed.</param>
/// <param name="Settings">Its settings, where the link gave them.</param>
/// <param name="Description">The song described (<see cref="SongDescription" />).</param>
/// <param name="Text">The description as text, for an agent to name the song by (<see cref="SongDescriptionText" />).</param>
public sealed record SongDescriptionResponse(string SongsVersion, string Seed, string? Settings, SongDescription Description, string Text);
