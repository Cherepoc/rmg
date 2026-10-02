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

    /// <summary>
    ///     The options' weights at a conventionality within a pool, such as a genre's, whose weights are the options' in
    ///     the middle, where the songs gather, in place of their tuned ones, and those of none left out at every
    ///     conventionality; the ends keep to the options they allow, times the pool's weights, and an end the pool leaves
    ///     with no option allowed, as 4/4 alone leaves the wild end of the meters, takes the middle's, so that what the
    ///     pool keeps is its plainest and its wildest.
    /// </summary>
    /// <param name="pool">An option's weight in the pool, from 0, out of it, to 1; none for the options as they are.</param>
    public static ImmutableArray<Weighted<T>> Weigh<T>(IEnumerable<(T Option, ByConvention Weight)> options, double conventionality, Func<T, double>? pool)
    {
        if (pool is null)
            return Weigh(options, conventionality);

        var kept = options.Select(x => (x.Option, x.Weight, Pool: pool(x.Option))).Where(x => x.Pool > 0).ToArray();
        if (kept.Length == 0)
            throw new InvalidOperationException("The pool keeps none of the options.");

        var ends = WithEnds([..kept.Select(x => new ByConvention(x.Weight.Plain * x.Pool, x.Pool, x.Weight.Wild * x.Pool))]);
        return Weigh(kept.Select((x, i) => (x.Option, ends[i])), conventionality);
    }

    /// <summary>
    ///     The options' weights at a conventionality leaned by a pool, such as a genre's, for a choice drawn in roles of
    ///     its own weights, such as a cadence's chord: every end's weights times the pool's, those of none left out, and
    ///     an end left with none taking the middle's, or the pool's own where the middle has none either.
    /// </summary>
    /// <param name="pool">An option's weight in the pool, from 0, out of it, to 1; none for the options as they are.</param>
    public static ImmutableArray<Weighted<T>> Lean<T>(IEnumerable<(T Option, ByConvention Weight)> options, double conventionality, Func<T, double>? pool)
    {
        if (pool is null)
            return Weigh(options, conventionality);

        var kept = options.Select(x => (x.Option, x.Weight, Pool: pool(x.Option))).Where(x => x.Pool > 0).ToArray();
        if (kept.Length == 0)
            throw new InvalidOperationException("The pool keeps none of the options.");

        var leaned = kept.Select(x => new ByConvention(x.Weight.Plain * x.Pool, x.Weight.Tuned * x.Pool, x.Weight.Wild * x.Pool)).ToArray();
        if (leaned.All(x => x is { Plain: 0, Tuned: 0, Wild: 0 }))
            leaned = [..kept.Select(x => new ByConvention(0, x.Pool, 0))];
        var ends = WithEnds(leaned);
        return Weigh(kept.Select((x, i) => (x.Option, ends[i])), conventionality);
    }

    /// <summary>
    ///     A chance at a conventionality by a pool's weight of it, from 0, never, through 1/2, the chance as it is, to 1,
    ///     always: its odds at each end times the weight's, and an end it leaves with neither a yes nor a no, as always
    ///     leaves one that never does, taking the middle's; none for the chance as it is.
    /// </summary>
    public double At(double conventionality, double? pool)
    {
        if (pool is not { } weight)
            return At(conventionality);

        var ends = WithEnds([
            new ByConvention(Plain * weight, Tuned * weight, Wild * weight),
            new ByConvention((1 - Plain) * (1 - weight), (1 - Tuned) * (1 - weight), (1 - Wild) * (1 - weight))
        ]);
        var (yes, no) = (ends[0], ends[1]);
        static double Share(double yes, double no) => yes + no > 0 ? yes / (yes + no) : 0;
        return new ByConvention(Share(yes.Plain, no.Plain), Share(yes.Tuned, no.Tuned), Share(yes.Wild, no.Wild)).At(conventionality);
    }

    /// <summary>The options' weights, an end with none allowed taking the middle's, or, where the middle has none, the other end's.</summary>
    private static ByConvention[] WithEnds(ByConvention[] weights)
    {
        double[] Of(Func<ByConvention, double> end) => [..weights.Select(end)];
        double[] Either(params double[][] ends) => ends.FirstOrDefault(x => x.Sum() > 0) ?? ends[0];
        var (plain, tuned, wild) = (Of(x => x.Plain), Of(x => x.Tuned), Of(x => x.Wild));
        (plain, tuned, wild) = (Either(plain, tuned, wild), Either(tuned, plain, wild), Either(wild, tuned, plain));
        return [..weights.Select((_, i) => new ByConvention(plain[i], tuned[i], wild[i]))];
    }
}