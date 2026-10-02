using System.Collections.Immutable;

namespace Rmg.Core.Composition;

/// <summary>How a genre speaks of a choice (<see cref="GenreChoice" />).</summary>
public enum GenreChoiceKind
{
    /// <summary>
    ///     A pool: the options the choice keeps to, each weighing from 0, out of it, to 1, at every conventionality; the
    ///     options it does not name are out.
    /// </summary>
    Pool,

    /// <summary>Chances, each its own: from 0, never, through 1/2, as it is, to 1, always; one not named as it is.</summary>
    Chances,

    /// <summary>Values, each its own: from 0, its least, through 1/2, as it is, to 1, its most; one not named as it is.</summary>
    Values,

    /// <summary>A range, its two options its ends, "from" and "to", each from 0 to 1, the first no greater; both named or neither.</summary>
    Range
}

/// <param name="Name">How a genre names it, unique in its choice.</param>
/// <param name="Label">How the page shows it.</param>
/// <param name="Default">
///     Its amount as the songs have it, which a genre that names its choice starts from: a pool's option's weight in the
///     middle, the heaviest's 1; a chance's or a value's 1/2.
/// </param>
public sealed record GenreOption(string Name, string Label, double Default);

/// <summary>A choice a genre may speak of: one the songs draw already, with its options by stable names.</summary>
/// <param name="Name">How a genre names it, such as "meter" or "instrument.bass".</param>
/// <param name="Label">How the page shows it.</param>
public sealed record GenreChoice(string Name, string Label, GenreChoiceKind Kind, ImmutableArray<GenreOption> Options)
{
    /// <summary>The option of the name; one the choice does not have is a mistake.</summary>
    public GenreOption this[string name] => Options.FirstOrDefault(x => x.Name == name)
                                            ?? throw new ArgumentOutOfRangeException(nameof(name), name, $"The choice {Name} has no option {name}.");
}

/// <summary>
///     A genre: what it says of the choices the songs draw, every amount from 0 to 1, a choice it does not name as the songs
///     have it. Made by the page, which keeps it and sends it; the songs know the choices (<see cref="GenreChoices" />),
///     never a genre. It is not a facet, and leaves the unconventionality as it is.
/// </summary>
/// <param name="Choices">Every choice it names, by its name, and its options' amounts, by theirs.</param>
public sealed record Genre(ImmutableDictionary<string, ImmutableDictionary<string, double>> Choices)
{
    /// <summary>No genre: every choice as the songs have it.</summary>
    public static Genre None { get; } = new(ImmutableDictionary<string, ImmutableDictionary<string, double>>.Empty);

    /// <summary>Whether it names the choice.</summary>
    public bool Names(GenreChoice choice) => Choices.ContainsKey(choice.Name);

    /// <summary>
    ///     The pool of a choice, every option's weight in it by the option's name, 0 for one it leaves out; none where the
    ///     genre does not name the choice, which is then as the songs have it.
    /// </summary>
    /// <param name="name">An option's name, as the choice gives it.</param>
    public Func<T, double>? Pool<T>(GenreChoice choice, Func<T, string> name)
    {
        if (choice.Kind != GenreChoiceKind.Pool)
            throw new ArgumentException($"The choice {choice.Name} is not a pool.", nameof(choice));
        if (!Choices.TryGetValue(choice.Name, out var pool))
            return null;

        return option =>
        {
            var named = name(option);
            _ = choice[named];
            return pool.GetValueOrDefault(named);
        };
    }

    /// <summary>A chance's weight, from 0, never, to 1, always; none where it is not named, for the chance as it is.</summary>
    public double? Chance(GenreChoice choice, string option)
    {
        Check(choice, GenreChoiceKind.Chances, option);
        return Choices.TryGetValue(choice.Name, out var chances) && chances.TryGetValue(option, out var chance) ? chance : null;
    }

    /// <summary>A range's ends, from 0 to 1, the first no greater; none where it is not named.</summary>
    public (double From, double To)? Range(GenreChoice choice)
    {
        if (choice.Kind != GenreChoiceKind.Range)
            throw new ArgumentException($"The choice {choice.Name} is not a range.", nameof(choice));
        return Choices.TryGetValue(choice.Name, out var ends) ? (ends["from"], ends["to"]) : null;
    }

    /// <summary>A value's shift, from -1, its least, through 0, as it is, to 1, its most.</summary>
    public double Shift(GenreChoice choice, string option)
    {
        Check(choice, GenreChoiceKind.Values, option);
        return Choices.TryGetValue(choice.Name, out var values) && values.TryGetValue(option, out var value) ? 2 * value - 1 : 0;
    }

    private static void Check(GenreChoice choice, GenreChoiceKind kind, string option)
    {
        if (choice.Kind != kind)
            throw new ArgumentException($"The choice {choice.Name} is not of {kind}.", nameof(choice));
        _ = choice[option];
    }

    /// <summary>
    ///     A genre read from its choices' names, its options' and their amounts, or what is wrong with it: a choice or an
    ///     option the songs do not have, an amount outside 0 to 1, a pool that keeps nothing, or a range not of two ends in order.
    /// </summary>
    public static Genre? Read(IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> choices, out string? error)
    {
        error = null;
        var read = ImmutableDictionary.CreateBuilder<string, ImmutableDictionary<string, double>>();
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

            if (options.Values.Any(x => x is < 0 or > 1 or double.NaN))
            {
                error = $"An amount of {name} is not from 0 to 1.";
                return null;
            }

            if (choice.Kind == GenreChoiceKind.Pool && options.Values.All(x => x == 0))
            {
                error = $"The pool of {name} keeps none of its options.";
                return null;
            }

            if (choice.Kind == GenreChoiceKind.Range && !(options.TryGetValue("from", out var from) && options.TryGetValue("to", out var to) && from <= to))
            {
                error = $"The range of {name} is from one end to the other, the first no greater.";
                return null;
            }

            read[name] = options.ToImmutableDictionary();
        }

        return new Genre(read.ToImmutable());
    }
}
