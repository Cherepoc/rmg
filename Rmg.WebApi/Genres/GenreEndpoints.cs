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
            [..x.Options.Select(o => new GenreOptionResponse(o.Name, o.Label, Songs.StepScale.ToStep(o.Default), o.IsPlayed, o.Tags, o.Description))]
        ))
    ];
}

/// <param name="Name">How a genre names it, such as "meter".</param>
/// <param name="Label">How the page shows it.</param>
/// <param name="Kind">"pool", its options a pool each weighing from 0, only in wilder songs, to 63 in a middling song, one left out never; "chances", each its chance in a middling song from 0, only in wilder songs, to 63, always, one left out never; "values", each from 0, its least, through 32, as it is, to 63, its most; or "range", its "from" and "to" the steps of its ends, the first no greater.</param>
/// <param name="Options">Its options, in order.</param>
public sealed record GenreChoiceResponse(string Name, string Label, string Kind, IReadOnlyList<GenreOptionResponse> Options);

/// <param name="Name">How a genre names it.</param>
/// <param name="Label">How the page shows it.</param>
/// <param name="Default">Its step as the songs have it, which a genre naming its choice starts from: a pool's option's weight in the middle, the heaviest's 63; a chance's in the middle; a value's 32.</param>
/// <param name="IsPlayed">Whether the songs play it at all, at one end or another, which a genre starting from theirs keeps.</param>
/// <param name="Tags">What the page needs to know of it besides, such as a drum piece's families, "kit" and "percussion", or another option it plays only with, "with:riff".</param>
/// <param name="Description">What the page shows over its name, such as how a scale sounds and its notes; empty for most.</param>
public sealed record GenreOptionResponse(string Name, string Label, int Default, bool IsPlayed, IReadOnlyList<string> Tags, string Description);
