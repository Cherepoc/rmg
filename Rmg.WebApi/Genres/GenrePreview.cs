using System.Collections.Immutable;
using System.Text.Json;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.WebApi.Songs;

namespace Rmg.WebApi.Genres;

/// <summary>
///     What a choice of a genre makes of its options from the plainest songs to the wildest, as the songs draw it: every
///     option's amounts eased between its three, and a pool's every option's share of the songs, at points across the
///     conventionality of the choice's facet. Its own, not where a role leans it further, such as a home's chord.
/// </summary>
public static class GenrePreview
{
    /// <summary>How many points, from 0 to 1, evenly.</summary>
    public const int Points = 21;

    private static readonly ImmutableArray<double> At = [..Enumerable.Range(0, Points).Select(x => x / (double)(Points - 1))];

    /// <summary>The choice's options' amounts at every point, as a request gives them (<see cref="GenreAmounts" />); or what is wrong with them.</summary>
    public static GenrePreviewResponse? Of(GenrePreviewRequest request, out string? error)
    {
        if (!new GenerateSongRequest(Genre: new Dictionary<string, IReadOnlyDictionary<string, JsonElement>> { [request.Choice] = request.Options })
                .TryRead(out _, out var overrides, out _, out error))
            return null;

        var choice = GenreChoices.All.Single(x => x.Name == request.Choice);
        var (curves, shares) = Along(choice, overrides.Genre!.Choices[choice.Name]);
        return new GenrePreviewResponse(At, curves!, shares);
    }

    /// <summary>
    ///     Every choice of a genre at every point: those it names as it names them, and the others as the songs have them,
    ///     a pool's shares alone; or what is wrong with it.
    /// </summary>
    public static GenrePreviewsResponse? OfGenre(IReadOnlyDictionary<string, IReadOnlyDictionary<string, JsonElement>> genre, out string? error)
    {
        if (!new GenerateSongRequest(Genre: genre).TryRead(out _, out var overrides, out _, out error))
            return null;

        return new GenrePreviewsResponse(At, GenreChoices.All.ToImmutableSortedDictionary(
            x => x.Name,
            x =>
            {
                var (curves, shares) = Along(x, overrides.Genre!.Choices.GetValueOrDefault(x.Name));
                return new GenreChoicePreview(curves, shares);
            },
            StringComparer.Ordinal));
    }

    /// <summary>
    ///     A choice's options along the points: every option's amounts eased between its three, and a pool's every
    ///     option's share of the songs; as the songs have them where the genre names none, the options they play, a pool's
    ///     shares alone.
    /// </summary>
    private static (ImmutableSortedDictionary<string, IReadOnlyList<double>>? Curves, ImmutableSortedDictionary<string, IReadOnlyList<double>>? Shares) Along(
        GenreChoice choice, IReadOnlyDictionary<string, ByConvention>? options)
    {
        var amounts = options ?? choice.Options.Where(x => x.IsPlayed).ToDictionary(x => x.Name, x => x.Own);
        var names = amounts.Keys.Order(StringComparer.Ordinal).ToArray();
        ImmutableSortedDictionary<string, IReadOnlyList<double>> Each(Func<double, string, double> amount) =>
            names.ToImmutableSortedDictionary(x => x, x => (IReadOnlyList<double>)[..At.Select(c => amount(c, x))], StringComparer.Ordinal);

        double CurveAt(double c, string option) => GenreAmounts.ToNumber(choice, option, amounts[option].At(c));
        double ShareAt(double c, string option)
        {
            var weights = ByConvention.Weigh(choice.Options.Select(x => (x.Name, x.Own)), c, options is null ? null : name => options.TryGetValue(name, out var weight) ? weight : null);
            return 100 * weights.Where(x => x.Value == option).Sum(x => x.Weight) / weights.Sum(x => x.Weight);
        }

        var isPool = choice.Kind == GenreChoiceKind.Pool;
        return (options is null && isPool ? null : Each(CurveAt), isPool ? Each(ShareAt) : null);
    }
}

/// <param name="Choice">The choice's name (<c>/api/genres/choices</c>).</param>
/// <param name="Options">Its options' amounts, as a song's request gives them (<see cref="GenerateSongRequest.Genre" />).</param>
public sealed record GenrePreviewRequest(string Choice, IReadOnlyDictionary<string, JsonElement> Options);

/// <param name="Conventionality">The points, from 0, the plainest, to 1, the wildest.</param>
/// <param name="Curves">
///     Every option's amounts eased between its three at every point, as a request gives them: a pool's weight, a chance's
///     chance in percent, a value's amount, 50 as it is, and the tempo's ends in beats a minute and its lean.
/// </param>
/// <param name="Shares">A pool's every option's share of the songs at every point, in percent; none for another kind.</param>
public sealed record GenrePreviewResponse(
    IReadOnlyList<double> Conventionality,
    ImmutableSortedDictionary<string, IReadOnlyList<double>> Curves,
    ImmutableSortedDictionary<string, IReadOnlyList<double>>? Shares);

/// <param name="Conventionality">The points, from 0, the plainest, to 1, the wildest.</param>
/// <param name="Choices">Every choice's preview by its name: those the genre names as it names them, the others as the songs have them.</param>
public sealed record GenrePreviewsResponse(IReadOnlyList<double> Conventionality, ImmutableSortedDictionary<string, GenreChoicePreview> Choices);

/// <param name="Curves">Every option's amounts at every point, as in <see cref="GenrePreviewResponse.Curves" />; none for a pool the genre does not name.</param>
/// <param name="Shares">A pool's every option's share of the songs at every point, in percent; none for another kind.</param>
public sealed record GenreChoicePreview(
    ImmutableSortedDictionary<string, IReadOnlyList<double>>? Curves,
    ImmutableSortedDictionary<string, IReadOnlyList<double>>? Shares);
