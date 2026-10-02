using Rmg.Core.Composition;

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
            [..x.Options.Select(o => new GenreOptionResponse(o.Name, o.Label, Songs.StepScale.ToStep(o.Default), o.Tags))]
        ))
    ];
}

/// <param name="Name">How a genre names it, such as "meter".</param>
/// <param name="Label">How the page shows it.</param>
/// <param name="Kind">"pool", its options a pool each weighing from 0 to 63; "chances", each from 0, never, through 32, as it is, to 63, always; "values", each from 0, its least, through 32, as it is, to 63, its most; or "range", its "from" and "to" the steps of its ends, the first no greater.</param>
/// <param name="Options">Its options, in order.</param>
public sealed record GenreChoiceResponse(string Name, string Label, string Kind, IReadOnlyList<GenreOptionResponse> Options);

/// <param name="Name">How a genre names it.</param>
/// <param name="Label">How the page shows it.</param>
/// <param name="Default">Its step as the songs have it, which a genre naming its choice starts from: a pool's option's weight, the heaviest's 63; a chance's or a value's 32.</param>
/// <param name="Tags">What the page needs to know of it besides, such as a drum piece's families, "kit" and "percussion".</param>
public sealed record GenreOptionResponse(string Name, string Label, int Default, IReadOnlyList<string> Tags);
