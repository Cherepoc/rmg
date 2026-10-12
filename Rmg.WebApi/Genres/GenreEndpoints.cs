using System.Text.Json;
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

        // every choice of a genre so, those it does not name as the songs have them, for the editor's graphs
        routes.MapPost("/api/genres/previews", (IReadOnlyDictionary<string, IReadOnlyDictionary<string, JsonElement>> genre) =>
            GenrePreview.OfGenre(genre, out var error) is { } previews ? Results.Ok(previews) : Results.BadRequest(new ErrorResponse(error!)));

        // a genre a link names by its key (GenreStore), as it was asked for, but the choices and options taken out since,
        // which every song of it would be refused for
        routes.MapGet("/api/genres/{key}", (string key, GenreStore store) => store.Find(key) is { } body
            ? Results.Content(Known(body), "application/json")
            : Results.NotFound(new ErrorResponse($"'{key}' is no genre kept here.")));
    }

    /// <summary>A kept genre's canonical form without the choices and the options <see cref="GenreChoices" /> no longer has.</summary>
    public static string Known(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var choices = root.GetProperty("choices").EnumerateObject()
            .Select(x => (Choice: GenreChoices.All.FirstOrDefault(c => c.Name == x.Name), x.Value))
            .Where(x => x.Choice is not null)
            .ToDictionary(x => x.Choice!.Name, x => (IReadOnlyDictionary<string, JsonElement>)x.Value.EnumerateObject()
                .Where(o => x.Choice!.Options.Any(c => c.Name == o.Name)).ToDictionary(o => o.Name, o => o.Value.Clone()));
        return GenreStore.Canonical(root.GetProperty("name").GetString()!, choices);
    }

    /// <summary>How many numbers a range's scale is told by, evenly along its amounts, which the page draws it on.</summary>
    private const int ScalePoints = 65;

    /// <summary>Every choice, in the order the page shows them and a link writes them.</summary>
    public static IReadOnlyList<GenreChoiceResponse> Choices { get; } =
    [
        ..GenreChoices.All.Select(x => new GenreChoiceResponse(
            x.Name,
            x.Label,
            x.Kind.ToString().ToLowerInvariant(),
            [..x.Options.Select(o => new GenreOptionResponse(o.Name, o.Label, o.Short, [..new[] { o.Own.Plain, o.Own.Tuned, o.Own.Wild }.Select(a => GenreAmounts.ToNumber(x, o.Name, a))], o.Tags, o.Description))],
            x.Scale is { } scale
                ? new RangeScaleResponse(scale.Unit, scale.Least, scale.Most, [..Enumerable.Range(0, ScalePoints).Select(i => scale.NumberOf(i / (double)(ScalePoints - 1)))], SongGenerator.RangeLeanSkew)
                : null,
            x.Description
        ))
    ];
}

/// <param name="Name">How a genre names it, such as "meter".</param>
/// <param name="Label">How the page shows it.</param>
/// <param name="Kind">"pool", its options a pool, each its weights at the ends, one left out never; "chances", each its chances at the ends, one left out never; "values", each from 0, its least, through 50, as it is, to 100, its most; or "range", its "from" and "to" its ends, the first no greater, and its "lean". A genre gives every amount in whole numbers (<see cref="GenreAmounts" />).</param>
/// <param name="Options">Its options, in order.</param>
/// <param name="Scale">A range's ends as numbers: their unit, such as "bpm" or "seconds", the least and the most; none for another kind.</param>
/// <param name="Description">What it does, in a sentence or two, as the page shows it under it.</param>
public sealed record GenreChoiceResponse(string Name, string Label, string Kind, IReadOnlyList<GenreOptionResponse> Options, RangeScaleResponse? Scale, string Description);

/// <param name="Unit">What a range's ends count, such as "bpm" or "seconds".</param>
/// <param name="Least">The least an end is.</param>
/// <param name="Most">The most an end is.</param>
/// <param name="Numbers">The numbers at even steps of the amounts, from the least to the most, which the songs draw evenly across, such as a length's, every like step a like ratio.</param>
/// <param name="LeanSkew">How far a lean of 100 skews a draw across the range: its place, from 0 at its from to 1 at its to, the mean of two evens, to the power of e to the minus this.</param>
public sealed record RangeScaleResponse(string Unit, double Least, double Most, IReadOnlyList<double> Numbers, double LeanSkew);

/// <param name="Name">How a genre names it.</param>
/// <param name="Label">How the page shows it.</param>
/// <param name="Short">How the page shows it where room is short, such as a form's letters, "VCVCBC"; its label for most.</param>
/// <param name="Own">
///     Its amounts as the songs have them, at the plain end, in the middle and at the wild, as a request gives them, not
///     rounded (<see cref="GenreAmounts" />), which a genre naming its choice starts from: a pool's option's weights, every
///     end's heaviest 100, 0 at all three for one the songs never play; a chance's chances; a value's 50; a range's ends,
///     by its scale, and its lean.
/// </param>
/// <param name="Tags">What the page needs to know of it besides, such as a drum piece's families, "kit" and "percussion", or another option it plays only with, "with:riff".</param>
/// <param name="Description">What the page shows over its name, such as how a scale sounds and its notes; empty for most.</param>
public sealed record GenreOptionResponse(string Name, string Label, string Short, IReadOnlyList<double> Own, IReadOnlyList<string> Tags, string Description);
