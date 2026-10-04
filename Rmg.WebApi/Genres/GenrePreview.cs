using System.Collections.Immutable;
using System.Text.Json;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.WebApi.Songs;

namespace Rmg.WebApi.Genres;

/// <summary>
///     What a choice of a genre makes of its options from the plainest songs to the wildest, as the songs draw it: a pool's
///     every option's share, a chance's chance, a value's amount, and the tempo's ends and its lean, at points across the
///     conventionality of the choice's facet. Its own, not where a role leans it further, such as a home's chord.
/// </summary>
public static class GenrePreview
{
    /// <summary>How many points, from 0 to 1, evenly.</summary>
    public const int Points = 21;

    /// <summary>The choice's options' amounts at every point, as a request gives them (<see cref="GenreAmounts" />); or what is wrong with them.</summary>
    public static GenrePreviewResponse? Of(GenrePreviewRequest request, out string? error)
    {
        if (!new GenerateSongRequest(Genre: new Dictionary<string, IReadOnlyDictionary<string, JsonElement>> { [request.Choice] = request.Options })
                .TryRead(out _, out var overrides, out _, out error))
            return null;

        var choice = GenreChoices.All.Single(x => x.Name == request.Choice);
        var options = overrides.Genre!.Choices[choice.Name];
        double[] at = [..Enumerable.Range(0, Points).Select(x => x / (double)(Points - 1))];
        IReadOnlyList<double> Along(Func<double, string, double> amount, string option) => [..at.Select(c => amount(c, option))];

        Func<double, string, double> amountAt = choice.Kind switch
        {
            GenreChoiceKind.Pool => (c, option) =>
            {
                var weights = ByConvention.Weigh(choice.Options.Select(x => (x.Name, x.Own)), c, name => options.TryGetValue(name, out var weight) ? weight : null);
                return 100 * weights.Where(x => x.Value == option).Sum(x => x.Weight) / weights.Sum(x => x.Weight);
            },
            _ => (c, option) => GenreAmounts.ToNumber(choice, option, options[option].At(c))
        };
        return new GenrePreviewResponse(at, options.Keys.Order(StringComparer.Ordinal).ToImmutableSortedDictionary(x => x, x => Along(amountAt, x), StringComparer.Ordinal));
    }
}

/// <param name="Choice">The choice's name (<c>/api/genres/choices</c>).</param>
/// <param name="Options">Its options' amounts, as a song's request gives them (<see cref="GenerateSongRequest.Genre" />).</param>
public sealed record GenrePreviewRequest(string Choice, IReadOnlyDictionary<string, JsonElement> Options);

/// <param name="Conventionality">The points, from 0, the plainest, to 1, the wildest.</param>
/// <param name="Amounts">
///     Every option's amount at every point: a pool's share of the songs in percent, a chance's chance in percent, a
///     value's amount, 50 as it is, and the tempo's ends in beats a minute and its lean, as a request gives them.
/// </param>
public sealed record GenrePreviewResponse(IReadOnlyList<double> Conventionality, ImmutableSortedDictionary<string, IReadOnlyList<double>> Amounts);
