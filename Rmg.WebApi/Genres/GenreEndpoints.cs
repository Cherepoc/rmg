using Rmg.Core.Composition;
using Rmg.WebApi.Songs;

namespace Rmg.WebApi.Genres;

/// <summary>The choices a genre may speak of, which the page makes genres of (<see cref="GenreChoices" />).</summary>
public static class GenreEndpoints
{
    public static void MapGenres(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/genres/choices", () => Results.Ok(Choices));
    }

    /// <summary>Every choice, in the order the page shows them and a link writes them.</summary>
    public static IReadOnlyList<GenreChoiceResponse> Choices { get; } =
    [
        ..GenreChoices.All.Select(x => new GenreChoiceResponse(
            x.Name,
            x.Label,
            x.Kind.ToString().ToLowerInvariant(),
            [..x.Options.Select(o => new GenreOptionResponse(o.Name, o.Label, [o.Own.Plain, o.Own.Tuned, o.Own.Wild], o.Tags, o.Description))]
        ))
    ];
}

/// <param name="Name">How a genre names it, such as "meter".</param>
/// <param name="Label">How the page shows it.</param>
/// <param name="Kind">"pool", its options a pool, each its weights at the ends, one left out never; "chances", each its chances at the ends, one left out never; "values", each from 0, its least, through 50, as it is, to 100, its most; or "range", its "from" and "to" its ends, the first no greater. A genre gives every amount in whole percents (<see cref="Songs.GenerateSongRequest.Genre" />).</param>
/// <param name="Options">Its options, in order.</param>
public sealed record GenreChoiceResponse(string Name, string Label, string Kind, IReadOnlyList<GenreOptionResponse> Options);

/// <param name="Name">How a genre names it.</param>
/// <param name="Label">How the page shows it.</param>
/// <param name="Own">
///     Its amounts as the songs have them, from 0 to 1, at the plain end, in the middle and at the wild, which a genre naming
///     its choice starts from: a pool's option's weights, every end's heaviest 1, 0 at all three for one the songs never
///     play; a chance's chances; a value's 1/2; a range's end's.
/// </param>
/// <param name="Tags">What the page needs to know of it besides, such as a drum piece's families, "kit" and "percussion", or another option it plays only with, "with:riff".</param>
/// <param name="Description">What the page shows over its name, such as how a scale sounds and its notes; empty for most.</param>
public sealed record GenreOptionResponse(string Name, string Label, IReadOnlyList<double> Own, IReadOnlyList<string> Tags, string Description);
