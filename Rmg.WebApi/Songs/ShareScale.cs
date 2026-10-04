using System.Collections.Immutable;
using Rmg.Core.Composition;

namespace Rmg.WebApi.Songs;

/// <summary>
///     The steps a genre's weight or chance is given in: places a hand sets, every one a whole percent, finer where the
///     amounts are small, the least above 0 a hundredth, and every chance the songs have in the middle among them; and the
///     steps of a genre's every amount, by its choice's kind, a value's and a range's the 64 of <see cref="StepScale" />.
/// </summary>
public static class ShareScale
{
    /// <summary>Every step's amount, from 0 to 1.</summary>
    public static ImmutableArray<double> Amounts { get; } =
        [..new[] { 0, 1, 2, 3, 4, 5, 6, 8, 10, 12, 15 }.Concat(Enumerable.Range(4, 17).Select(x => 5 * x)).Select(x => x / 100.0)];

    /// <summary>The last step, an amount of 1.</summary>
    public static int Last => Amounts.Length - 1;

    /// <summary>The nearest step of an amount.</summary>
    public static int ToStep(double amount) => Amounts.Select((x, i) => (Distance: Math.Abs(x - amount), Step: i)).MinBy(x => x.Distance).Step;

    /// <summary>Whether a choice's amounts are steps of this scale: a pool's weights and chances.</summary>
    public static bool Steps(GenreChoice choice) => choice.Kind is GenreChoiceKind.Pool or GenreChoiceKind.Chances;

    /// <summary>The last step of a choice's amounts.</summary>
    public static int LastOf(GenreChoice choice) => Steps(choice) ? Last : StepScale.Last;

    /// <summary>A step of a choice's amount as the amount.</summary>
    public static double AmountOf(GenreChoice choice, int step) => Steps(choice) ? Amounts[step] : StepScale.ToAmount(step);

    /// <summary>A choice's amount as its nearest step.</summary>
    public static int StepOf(GenreChoice choice, double amount) => Steps(choice) ? ToStep(amount) : StepScale.ToStep(amount);
}
