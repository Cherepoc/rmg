using System.Text.Json;
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
    ///     The song of a link, its seed, its settings and its genre, a preset's id or a kept genre's key, described as the
    ///     songs make it now, waiting its turn as any song does.
    /// </summary>
    private static async Task<IResult> Describe(HttpContext context, SongQueue queue, Genres.GenreStore genres, string? song, string? settings, string? genre)
    {
        if (song is null || Base62.ToSeed(song.Trim()) is not { } seed)
            return Results.BadRequest(new ErrorResponse($"'{song}' is not a seed. A seed is up to {Base62.SeedLength} letters and digits."));

        var text = Base62.FromSeed(seed);
        SongSettings? given = null;
        if (settings is not null && (given = SongSettings.Parse(settings.Trim())) is null)
            return Results.BadRequest(new ErrorResponse($"'{settings}' are not settings this version reads."));

        var request = given?.ToRequest(text) ?? new GenerateSongRequest(text);
        if (genre?.Trim() is { Length: > 0 } named)
        {
            if (Genres.GenrePresetFile.All.FirstOrDefault(x => x.Id == named) is { } preset)
                request = request with { Genre = preset.Choices.ToDictionary(x => x.Key, x => (IReadOnlyDictionary<string, JsonElement>)x.Value), GenreName = preset.Name };
            else if (genres.Find(named) is { } body)
            {
                // as the page is served it, without what was taken out since
                using var kept = JsonDocument.Parse(Genres.GenreEndpoints.Known(body));
                request = request with
                {
                    Genre = kept.RootElement.GetProperty("choices").EnumerateObject()
                        .ToDictionary(x => x.Name, x => (IReadOnlyDictionary<string, JsonElement>)x.Value.EnumerateObject().ToDictionary(o => o.Name, o => o.Value.Clone())),
                    // a genre of no name is none's, not one called nothing
                    GenreName = kept.RootElement.GetProperty("name").GetString() is { Length: > 0 } name ? name : null
                };
            }
            else
                return Results.BadRequest(new ErrorResponse($"'{named}' is neither a preset nor a genre this server keeps."));
        }

        if (!request.TryRead(out _, out var overrides, out var mix, out var error))
            return Results.BadRequest(new ErrorResponse(error!));

        using var turn = await queue.Enter(SongQueue.ClientOf(context.Connection.RemoteIpAddress), context.RequestAborted);
        if (turn.Refusal is { } refusal)
            return SongEndpoints.Refused(context, refusal);

        var description = SongDescriptions.Describe(seed, overrides, mix, context.RequestAborted);
        var settingsText = given?.Format();
        return Results.Ok(new SongDescriptionResponse(
            SongsVersion.Number,
            text,
            settingsText,
            description,
            SongDescriptionText.ToMarkdown(description, text, settingsText, request.GenreName, SongsVersion.Number)
        ));
    }
}

/// <param name="SongsVersion">The songs' version that made the song described.</param>
/// <param name="Seed">The song's seed.</param>
/// <param name="Settings">Its settings, where the link gave them.</param>
/// <param name="Description">The song described (<see cref="SongDescription" />).</param>
/// <param name="Text">The description as text, for an agent to name the song by (<see cref="SongDescriptionText" />).</param>
public sealed record SongDescriptionResponse(string SongsVersion, string Seed, string? Settings, SongDescription Description, string Text);
