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
    ///     The options' weights at a conventionality within a pool, such as a genre's, which gives every option it keeps its
    ///     own weights at each end in place of the songs', and never plays one it leaves out (<see cref="Filled{T}" />).
    /// </summary>
    /// <param name="pool">An option's weights in the pool, none for one it leaves out; none for the options as they are.</param>
    public static ImmutableArray<Weighted<T>> Weigh<T>(IEnumerable<(T Option, ByConvention Weight)> options, double conventionality, Func<T, ByConvention?>? pool)
    {
        if (pool is null)
            return Weigh(options, conventionality);

        return Weigh(Filled(Kept(options.Select(x => x.Option), pool)), conventionality);
    }

    /// <summary>
    ///     The options' weights at a conventionality leaned by a pool, such as a genre's, for a choice drawn in roles of
    ///     its own weights, such as a cadence's chord: every end's own weight times the pool's over the songs' there, the
    ///     pool's alone where the songs' is 0, and an option it leaves out never playing (<see cref="Filled{T}" />).
    /// </summary>
    /// <param name="lean">An option's weights in the pool and as the songs have them, none for one it leaves out; none for the options as they are.</param>
    public static ImmutableArray<Weighted<T>> Lean<T>(IEnumerable<(T Option, ByConvention Weight)> options, double conventionality,
        Func<T, (ByConvention Pool, ByConvention Songs)?>? lean)
    {
        if (lean is null)
            return Weigh(options, conventionality);

        static double Times(double own, double pool, double songs) => own * (songs > 0 ? pool / songs : pool);
        var kept = options.Select(x => (x.Option, x.Weight, Lean: lean(x.Option))).Where(x => x.Lean is not null)
            .Select(x => (x.Option, new ByConvention(Times(x.Weight.Plain, x.Lean!.Value.Pool.Plain, x.Lean.Value.Songs.Plain),
                Times(x.Weight.Tuned, x.Lean.Value.Pool.Tuned, x.Lean.Value.Songs.Tuned), Times(x.Weight.Wild, x.Lean.Value.Pool.Wild, x.Lean.Value.Songs.Wild))))
            .ToArray();
        if (kept.Length == 0)
            throw new InvalidOperationException("The pool keeps none of the options.");
        return Weigh(Filled(kept), conventionality);
    }

    /// <summary>
    ///     The options' weights at a conventionality within a pool, such as a genre's, for a choice the songs draw alike at
    ///     every conventionality, such as an instrument: each option's weight there, 0 for one it leaves out.
    /// </summary>
    public static Func<T, double> Within<T>(IEnumerable<T> options, double conventionality, Func<T, ByConvention?> pool) where T : notnull
    {
        var weights = Weigh(Filled(Kept(options, pool)), conventionality).ToDictionary(x => x.Value, x => x.Weight);
        return option => weights.GetValueOrDefault(option);
    }

    /// <summary>
    ///     The options' weights at a conventionality, an end where none weighs taking the middle's and the middle with none
    ///     the nearer end's (<see cref="Filled{T}" />), such as where a genre takes a drum from its main role.
    /// </summary>
    public static ImmutableArray<Weighted<T>> WeighFilled<T>(IEnumerable<(T Option, ByConvention Weight)> options, double conventionality) =>
        Weigh(Filled([..options]), conventionality);

    /// <summary>The chance at a conventionality, or a pool's, such as a genre's, in place of it; none for the chance as it is.</summary>
    public double At(double conventionality, ByConvention? pool) => (pool ?? this).At(conventionality);

    private static (T Option, ByConvention Weight)[] Kept<T>(IEnumerable<T> options, Func<T, ByConvention?> pool)
    {
        var kept = options.Select(x => (Option: x, Weight: pool(x))).Where(x => x.Weight is not null).Select(x => (x.Option, x.Weight!.Value)).ToArray();
        if (kept.Length == 0)
            throw new InvalidOperationException("The pool keeps none of the options.");
        return kept;
    }

    /// <summary>
    ///     The options with an end where none weighs taking the middle's, and the middle with none the nearer end's, so
    ///     that a pool of options only the wild end plays still plays one at every conventionality.
    /// </summary>
    private static (T Option, ByConvention Weight)[] Filled<T>((T Option, ByConvention Weight)[] options)
    {
        double[] Of(Func<ByConvention, double> end) => [..options.Select(x => end(x.Weight))];
        double[] Either(params double[][] ends) => ends.FirstOrDefault(x => x.Sum() > 0) ?? ends[0];
        var (plain, tuned, wild) = (Of(x => x.Plain), Of(x => x.Tuned), Of(x => x.Wild));
        (plain, tuned, wild) = (Either(plain, tuned, wild), Either(tuned, plain, wild), Either(wild, tuned, plain));
        return [..options.Select((x, i) => (x.Option, new ByConvention(plain[i], tuned[i], wild[i])))];
    }
}