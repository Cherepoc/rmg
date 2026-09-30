using System.Collections.Immutable;

namespace Rmg.Core.Probabilities;

/// <summary>
///     An option's weight, a chance or a value, such as how strictly a progression keeps to its rules, by
///     conventionality: <see cref="Plain" /> at 0, the plainest, where only what convention allows plays; <see
///     cref="Tuned" /> at 0.5, as the generator is tuned; and <see cref="Wild" /> at 1, the wildest. A weight of 0 at
///     an end leaves the option out there, and a chance of 1 has it happen every time. Between, the value follows a
///     smooth curve through the three, which never leaves the range between its neighbours, so that it only rises or
///     only falls between two of them and is never below 0.
/// </summary>
public readonly record struct ByConvention(double Plain, double Tuned, double Wild)
{
    /// <summary>The value at a conventionality from 0 to 1.</summary>
    public double At(double conventionality)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(conventionality, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(conventionality, 1);

        // a monotone cubic through the three: each half a Hermite curve, the middle's slope the harmonic mean of the
        // halves' when they rise or fall alike and flat otherwise, the ends' their half's own
        var (low, high) = ((Tuned - Plain) * 2, (Wild - Tuned) * 2);
        var middle = low * high > 0 ? 2 * low * high / (low + high) : 0;
        return conventionality <= 0.5
            ? Hermite(Plain, Tuned, low, middle, conventionality * 2)
            : Hermite(Tuned, Wild, middle, high, conventionality * 2 - 1);
    }

    /// <summary>The curve over a half from one value to the next, with their slopes over the whole range, at t from 0 to 1.</summary>
    private static double Hermite(double from, double to, double fromSlope, double toSlope, double t)
    {
        var (t2, t3) = (t * t, t * t * t);
        return (2 * t3 - 3 * t2 + 1) * from + (t3 - 2 * t2 + t) * fromSlope / 2 + (-2 * t3 + 3 * t2) * to + (t3 - t2) * toSlope / 2;
    }

    /// <summary>
    ///     The options' weights at a conventionality, those it leaves out dropped, for a pick; a choice that allows no
    ///     option there is wrong and fails.
    /// </summary>
    public static ImmutableArray<Weighted<T>> Weigh<T>(IEnumerable<(T Option, ByConvention Weight)> options, double conventionality)
    {
        var weighted = options.Select(x => new Weighted<T>(x.Weight.At(conventionality), x.Option)).Where(x => x.Weight > 0).ToImmutableArray();
        if (weighted.IsEmpty)
            throw new InvalidOperationException($"No option is allowed at a conventionality of {conventionality}.");
        return weighted;
    }
}
