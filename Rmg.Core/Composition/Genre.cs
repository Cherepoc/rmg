using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>How a genre speaks of a choice (<see cref="GenreChoice" />).</summary>
public enum GenreChoiceKind
{
    /// <summary>
    ///     A pool: the options the choice keeps to, each its weights from 0 to 1 at the plain end, in the middle and at the
    ///     wild, drawn as the songs' own are (<see cref="ByConvention.Weigh{T}(IEnumerable{ValueTuple{T, ByConvention}}, double, Func{T, ByConvention?})" />):
    ///     one at 0 in the middle and above it at the wild end plays only the wilder a song is, one it does not name never,
    ///     and one alone always.
    /// </summary>
    Pool,

    /// <summary>
    ///     Chances, each its own, from 0 to 1 at the plain end, in the middle and at the wild: 1 at every end always, and
    ///     one not named never.
    /// </summary>
    Chances,

    /// <summary>Values, each its own: from 0, its least, through 1/2, as it is, to 1, its most; one not named as it is.</summary>
    Values,

    /// <summary>
    ///     A range, its options its ends, "from" and "to", each from 0 to 1, the first no greater, both named or neither, and
    ///     its "lean" within them, from 0, to its first, through 1/2, its middle, to 1, to its last, the middle where not named.
    /// </summary>
    Range
}

/// <param name="Name">How a genre names it, unique in its choice.</param>
/// <param name="Label">How the page shows it.</param>
/// <param name="Own">
///     Its amounts as the songs have them, at the plain end, in the middle and at the wild, which a genre that names its
///     choice starts from: a pool's option's weight, every end's heaviest 1; a chance's; a value's 1/2, as it is; a range's
///     end's.
/// </param>
public sealed record GenreOption(string Name, string Label, ByConvention Own)
{
    /// <summary>Its amount as the songs have it in the middle.</summary>
    public double Default => Own.Tuned;

    /// <summary>Whether the songs play it at all, at one end or another, which a genre starting from theirs keeps.</summary>
    public bool IsPlayed => Own.Plain > 0 || Own.Tuned > 0 || Own.Wild > 0;

    /// <summary>
    ///     What the page needs to know of it besides, such as a drum piece's families, "kit" and "percussion", or another
    ///     option it plays only with, "with:riff".
    /// </summary>
    public ImmutableArray<string> Tags { get; init; } = [];

    /// <summary>
    ///     Whether a genre may play it at the plain end; a reharmonisation's chance may not, a plain song being never
    ///     reharmonised, its amount there read as 0 whatever the genre says.
    /// </summary>
    public bool HasPlainEnd { get; init; } = true;

    /// <summary>What the page shows over its name, such as how a scale sounds and its notes; none for most.</summary>
    public string Description { get; init; } = "";
}

/// <summary>A choice a genre may speak of: one the songs draw already, with its options by stable names.</summary>
/// <param name="Name">How a genre names it, such as "meter" or "instrument.bass".</param>
/// <param name="Label">How the page shows it.</param>
public sealed record GenreChoice(string Name, string Label, GenreChoiceKind Kind, ImmutableArray<GenreOption> Options)
{
    /// <summary>How a range's ends read as numbers, which every range has; none for another kind.</summary>
    public RangeScale? Scale { get; init; }

    /// <summary>The option of the name; one the choice does not have is a mistake.</summary>
    public GenreOption this[string name] => Options.FirstOrDefault(x => x.Name == name)
                                            ?? throw new ArgumentOutOfRangeException(nameof(name), name, $"The choice {Name} has no option {name}.");
}

/// <summary>
///     A genre: what it says of the choices the songs draw, every option's amounts at the plain end, in the middle and at the
///     wild, each from 0 to 1, a choice it does not name as the songs have it. Made by the page, which keeps it and sends
///     it; the songs know the choices (<see cref="GenreChoices" />), never a genre. It is not a facet, and leaves the
///     unconventionality as it is.
/// </summary>
/// <param name="Choices">Every choice it names, by its name, and its options' amounts, by theirs.</param>
public sealed record Genre(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>> Choices)
{
    /// <summary>No genre: every choice as the songs have it.</summary>
    public static Genre None { get; } = new(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty);

    /// <summary>Whether it names the choice.</summary>
    public bool Names(GenreChoice choice) => Choices.ContainsKey(choice.Name);

    /// <summary>
    ///     The pool of a choice, every option's weights at its ends by the option's name, none for one it leaves out; none
    ///     where the genre does not name the choice, which is then as the songs have it.
    /// </summary>
    /// <param name="name">An option's name, as the choice gives it.</param>
    public Func<T, ByConvention?>? Pool<T>(GenreChoice choice, Func<T, string> name)
    {
        if (choice.Kind != GenreChoiceKind.Pool)
            throw new ArgumentException($"The choice {choice.Name} is not a pool.", nameof(choice));
        if (!Choices.TryGetValue(choice.Name, out var pool))
            return null;

        return option =>
        {
            var named = name(option);
            _ = choice[named];
            return pool.TryGetValue(named, out var weights) ? weights : null;
        };
    }

    /// <summary>
    ///     The pool of a choice and the songs' own weights beside it, for one drawn in roles of its own weights, which the
    ///     pool leans by its weights over the songs' (<see cref="ByConvention.Lean{T}" />); none for an option it leaves
    ///     out, and none where the genre does not name the choice.
    /// </summary>
    public Func<T, (ByConvention Pool, ByConvention Songs)?>? Lean<T>(GenreChoice choice, Func<T, string> name) => Pool(choice, name) is { } pool
        ? option => pool(option) is { } weights ? (weights, choice[name(option)].Own) : null
        : null;

    /// <summary>
    ///     A chance's chances at its ends; never where the genre names the choice but not the chance; none where it does not
    ///     name the choice, for the chance as it is.
    /// </summary>
    public ByConvention? Chance(GenreChoice choice, string option)
    {
        Check(choice, GenreChoiceKind.Chances, option);
        if (!Choices.TryGetValue(choice.Name, out var chances))
            return null;
        return chances.TryGetValue(option, out var chance) ? chance : default(ByConvention);
    }

    /// <summary>
    ///     A range at a conventionality: its ends, from 0 to 1, the first no greater, and how it leans within them, from -1,
    ///     to its first, through 0, its middle, to 1, to its last, each eased between its own ends; none where it is not named.
    /// </summary>
    public (double From, double To, double Lean)? Range(GenreChoice choice, double conventionality)
    {
        if (choice.Kind != GenreChoiceKind.Range)
            throw new ArgumentException($"The choice {choice.Name} is not a range.", nameof(choice));
        if (!Choices.TryGetValue(choice.Name, out var range))
            return null;
        var lean = range.TryGetValue("lean", out var leaning) ? 2 * leaning.At(conventionality) - 1 : 0;
        return (range["from"].At(conventionality), range["to"].At(conventionality), lean);
    }

    /// <summary>A value's shift, from -1, its least, through 0, as it is, to 1, its most.</summary>
    public double Shift(GenreChoice choice, string option)
    {
        Check(choice, GenreChoiceKind.Values, option);
        return Choices.TryGetValue(choice.Name, out var values) && values.TryGetValue(option, out var value) ? 2 * value.Tuned - 1 : 0;
    }

    private static void Check(GenreChoice choice, GenreChoiceKind kind, string option)
    {
        if (choice.Kind != kind)
            throw new ArgumentException($"The choice {choice.Name} is not of {kind}.", nameof(choice));
        _ = choice[option];
    }

    /// <summary>
    ///     A genre read from its choices' names, its options' and their amounts at the plain end, in the middle and at the
    ///     wild, or what is wrong with it: a choice or an option the songs do not have, an amount outside 0 to 1, a pool that
    ///     keeps nothing, or a range not of two ends in order.
    /// </summary>
    public static Genre? Read(IReadOnlyDictionary<string, IReadOnlyDictionary<string, ByConvention>> choices, out string? error)
    {
        error = null;
        var read = ImmutableDictionary.CreateBuilder<string, ImmutableDictionary<string, ByConvention>>();
        foreach (var (name, options) in choices)
        {
            var choice = GenreChoices.All.FirstOrDefault(x => x.Name == name);
            if (choice is null)
            {
                error = $"{name} is not a choice a genre may name (/api/genres/choices).";
                return null;
            }

            if (options.Keys.FirstOrDefault(x => choice.Options.All(o => o.Name != x)) is { } unknown)
            {
                error = $"{unknown} is not an option of {name}.";
                return null;
            }

            if (options.Values.SelectMany(x => new[] { x.Plain, x.Tuned, x.Wild }).Any(x => x is < 0 or > 1 or double.NaN))
            {
                error = $"An amount of {name} is not from 0 to 1.";
                return null;
            }

            if (choice.Kind == GenreChoiceKind.Pool && options.Count == 0)
            {
                error = $"The pool of {name} keeps none of its options.";
                return null;
            }

            if (choice.Kind == GenreChoiceKind.Range && !(options.TryGetValue("from", out var from) && options.TryGetValue("to", out var to)
                                                          && from.Plain <= to.Plain && from.Tuned <= to.Tuned && from.Wild <= to.Wild))
            {
                error = $"The range of {name} is from one end to the other, the first no greater at every end.";
                return null;
            }

            read[name] = options.ToImmutableDictionary(x => x.Key, x => choice[x.Key].HasPlainEnd ? x.Value : x.Value with { Plain = 0 });
        }

        return new Genre(read.ToImmutable());
    }
}

/// <summary>
///     How a range's ends read as numbers, as the page shows and gives them: their unit, the least and the most, and a
///     number of an amount from 0 to 1 and back.
/// </summary>
/// <param name="Unit">What the numbers count, such as "bpm" or "seconds".</param>
public sealed record RangeScale(string Unit, double Least, double Most, Func<double, double> NumberOf, Func<double, double> AmountOf);
