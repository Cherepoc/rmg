using System.Collections.Immutable;

namespace Rmg.Core.Probabilities;

/// <summary>
///     An option's weight, a chance or a value, such as how strictly a progression keeps to its rules, by
///     conventionality: <see cref="Plain" /> at 0, the plainest, where only what convention allows plays; <see
///     cref="Tuned" /> at 0.5, as the generator is tuned; and <see cref="Wild" /> at 1, the wildest. A weight of 0 at
///     an end leaves the option out there, and a chance of 1 has it happen every time. Between, the value eases from
///     the tuned middle towards either end (<see cref="Ease" />), so that it only rises or only falls between two of
///     them, never leaves the range between them, and is never below 0.
/// </summary>
public readonly record struct ByConvention(double Plain, double Tuned, double Wild)
{
    /// <summary>
    ///     How the value eases from its tuned middle towards an end: the part of the way it has come, the part of the way
    ///     to the end the conventionality has come, to this power, so that the songs about the middle play as tuned and the
    ///     ends tell near them.
    /// </summary>
    public const double Ease = 3;

    /// <summary>The value at a conventionality from 0 to 1.</summary>
    public double At(double conventionality)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(conventionality, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(conventionality, 1);

        var towards = (conventionality - 0.5) * 2;
        return Tuned + ((towards < 0 ? Plain : Wild) - Tuned) * Math.Pow(Math.Abs(towards), Ease);
    }

    /// <summary>
    ///     The options' weights at a conventionality, those it leaves out dropped, for a pick; a choice that allows no
    ///     option there is wrong and fails. The weights of every end are shares, as they would be picked there: each end's
    ///     are made to sum to one before the curves join them, so that ten rare options each as likely as the rest at the
    ///     wild end do not outweigh a common one as soon as the conventionality passes the middle.
    /// </summary>
    public static ImmutableArray<Weighted<T>> Weigh<T>(IEnumerable<(T Option, ByConvention Weight)> options, double conventionality)
    {
        var list = options.ToArray();
        var (plain, tuned, wild) = (list.Sum(x => x.Weight.Plain), list.Sum(x => x.Weight.Tuned), list.Sum(x => x.Weight.Wild));
        var weighted = list
            .Select(x => new Weighted<T>(new ByConvention(Share(x.Weight.Plain, plain), Share(x.Weight.Tuned, tuned), Share(x.Weight.Wild, wild)).At(conventionality), x.Option))
            .Where(x => x.Weight > 0)
            .ToImmutableArray();
        if (weighted.IsEmpty)
            throw new InvalidOperationException($"No option is allowed at a conventionality of {conventionality}.");
        return weighted;

        // an end where no option is allowed is left at 0, which the pick there refuses
        static double Share(double weight, double sum) => sum > 0 ? weight / sum : 0;
    }
}
