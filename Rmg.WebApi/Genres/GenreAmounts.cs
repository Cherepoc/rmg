using Rmg.Core.Composition;

namespace Rmg.WebApi.Genres;

/// <summary>
///     A genre's amounts as a request gives them, whole numbers, against the amounts the songs take, from 0 to 1: a
///     pool's weights, chances and values in percents; a tempo's ends in beats a minute, from 60 to 240, and its lean
///     from -100, to its slow end, through 0, its middle, to 100, to its fast.
/// </summary>
public static class GenreAmounts
{
    /// <summary>The least and the most a request gives an option of a choice.</summary>
    public static (int Least, int Most) RangeOf(GenreChoice choice, string option) => choice.Kind switch
    {
        GenreChoiceKind.Range when option == "lean" => (-100, 100),
        GenreChoiceKind.Range => (60, 240),
        _ => (0, 100)
    };

    /// <summary>An amount, from 0 to 1, of an option's number as a request gives it.</summary>
    public static double ToAmount(GenreChoice choice, string option, int number) => choice.Kind switch
    {
        GenreChoiceKind.Range when option == "lean" => (number + 100) / 200.0,
        GenreChoiceKind.Range => GenreChoices.AmountOfTempo(number / Meter.BaseTempo),
        _ => number / 100.0
    };

    /// <summary>An option's number as a request gives it, of an amount from 0 to 1, not rounded.</summary>
    public static double ToNumber(GenreChoice choice, string option, double amount) => choice.Kind switch
    {
        GenreChoiceKind.Range when option == "lean" => 200 * amount - 100,
        GenreChoiceKind.Range => GenreChoices.TempoOf(amount) * Meter.BaseTempo,
        _ => 100 * amount
    };
}
