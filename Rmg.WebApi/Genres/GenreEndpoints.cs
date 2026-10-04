using Rmg.Core.Composition;
using Rmg.WebApi.Songs;

namespace Rmg.WebApi.Genres;

/// <summary>The choices a genre may speak of, which the page makes genres of (<see cref="GenreChoices" />).</summary>
public static class GenreEndpoints
{
    public static void MapGenres(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/genres/choices", () => Results.Ok(Choices));

        // what a choice of a genre makes of its options from the plainest songs to the wildest, for the editor's chart
        routes.MapPost("/api/genres/preview", (GenrePreviewRequest request) => GenrePreview.Of(request, out var error) is { } preview
            ? Results.Ok(preview)
            : Results.BadRequest(new ErrorResponse(error!)));

        // a genre a link names by its key (GenreStore), as it was asked for
        routes.MapGet("/api/genres/{key}", (string key, GenreStore store) => store.Find(key) is { } body
            ? Results.Content(body, "application/json")
            : Results.NotFound(new ErrorResponse($"'{key}' is no genre kept here.")));
    }

    /// <summary>Every choice, in the order the page shows them and a link writes them.</summary>
    public static IReadOnlyList<GenreChoiceResponse> Choices { get; } =
    [
        ..GenreChoices.All.Select(x => new GenreChoiceResponse(
            x.Name,
            x.Label,
            x.Kind.ToString().ToLowerInvariant(),
            [..x.Options.Select(o => new GenreOptionResponse(o.Name, o.Label, [..new[] { o.Own.Plain, o.Own.Tuned, o.Own.Wild }.Select(a => GenreAmounts.ToNumber(x, o.Name, a))], o.Tags, o.Description))]
        ))
    ];
}

/// <param name="Name">How a genre names it, such as "meter".</param>
/// <param name="Label">How the page shows it.</param>
/// <param name="Kind">"pool", its options a pool, each its weights at the ends, one left out never; "chances", each its chances at the ends, one left out never; "values", each from 0, its least, through 50, as it is, to 100, its most; or "range", its "from" and "to" its ends, the first no greater, and its "lean". A genre gives every amount in whole numbers (<see cref="GenreAmounts" />).</param>
/// <param name="Options">Its options, in order.</param>
public sealed record GenreChoiceResponse(string Name, string Label, string Kind, IReadOnlyList<GenreOptionResponse> Options);

/// <param name="Name">How a genre names it.</param>
/// <param name="Label">How the page shows it.</param>
/// <param name="Own">
///     Its amounts as the songs have them, at the plain end, in the middle and at the wild, as a request gives them, not
///     rounded (<see cref="GenreAmounts" />), which a genre naming its choice starts from: a pool's option's weights, every
///     end's heaviest 100, 0 at all three for one the songs never play; a chance's chances; a value's 50; the tempo's ends
///     and its lean.
/// </param>
/// <param name="Tags">What the page needs to know of it besides, such as a drum piece's families, "kit" and "percussion", or another option it plays only with, "with:riff".</param>
/// <param name="Description">What the page shows over its name, such as how a scale sounds and its notes; empty for most.</param>
public sealed record GenreOptionResponse(string Name, string Label, IReadOnlyList<double> Own, IReadOnlyList<string> Tags, string Description);
