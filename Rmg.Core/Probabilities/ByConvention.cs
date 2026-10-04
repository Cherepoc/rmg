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
    ///     the middle, where the songs gather, in place of their tuned ones, and which never plays an option it leaves out
    ///     (<see cref="Toward{T}" />).
    /// </summary>
    /// <param name="pool">An option's weight in the pool's middle, from 0 up, or none for one it leaves out; none for the options as they are.</param>
    public static ImmutableArray<Weighted<T>> Weigh<T>(IEnumerable<(T Option, ByConvention Weight)> options, double conventionality, Func<T, double?>? pool)
    {
        if (pool is null)
            return Weigh(options, conventionality);

        var kept = Kept(options, pool);
        var own = Shares([..kept.Select(x => x.Weight)]);
        var middles = kept.Select(x => x.Pool).ToArray();
        // an option's middle in the pool over its own, which leans its ends; none where the songs have it in no middle
        var sum = middles.Sum();
        double? Ratio(int i) => own[i].Tuned > 0 ? sum > 0 ? middles[i] / sum / own[i].Tuned : 0 : null;
        return Toward(kept, own, middles, Ratio, conventionality);
    }

    /// <summary>
    ///     The options' weights at a conventionality leaned by a pool, such as a genre's, for a choice drawn in roles of
    ///     its own weights, such as a cadence's chord: every option's own weights, its middle among them, times its lean,
    ///     and an option it leaves out never playing (<see cref="Toward{T}" />).
    /// </summary>
    /// <param name="lean">An option's lean, its weight in the pool over its weight as the songs have it, or none for one it leaves out; none for the options as they are.</param>
    public static ImmutableArray<Weighted<T>> Lean<T>(IEnumerable<(T Option, ByConvention Weight)> options, double conventionality, Func<T, double?>? lean)
    {
        if (lean is null)
            return Weigh(options, conventionality);

        var kept = Kept(options, lean);
        var own = Shares([..kept.Select(x => x.Weight)]);
        return Toward(kept, own, [..kept.Select((x, i) => own[i].Tuned * x.Pool)], i => kept[i].Pool, conventionality);
    }

    /// <summary>
    ///     The options' weights at a conventionality within a pool of plain weights, such as instruments', drawn alike at
    ///     every conventionality, by the pool: each option's weight there, 0 for one it leaves out or the songs never play.
    /// </summary>
    public static Func<T, double> Within<T>(IEnumerable<(T Option, double Weight)> options, double conventionality, Func<T, double?> pool) where T : notnull
    {
        var weights = Weigh(options.Select(x => (x.Option, new ByConvention(x.Weight, x.Weight, x.Weight))), conventionality, pool).ToDictionary(x => x.Value, x => x.Weight);
        return option => weights.GetValueOrDefault(option);
    }

    /// <summary>
    ///     The chance at a conventionality within a pool of its yes and its no, such as a genre's: a chance in the middle,
    ///     the yes's weight and the no's 1 less it, the yes alone for always and the no alone for never, at every
    ///     conventionality (<see cref="Weigh{T}(IEnumerable{ValueTuple{T, ByConvention}}, double, Func{T, double?})" />).
    /// </summary>
    /// <param name="pool">The weights of the yes (true) and the no (false) in the middle, none for one left out; none for the chance as it is.</param>
    public double At(double conventionality, Func<bool, double?>? pool)
    {
        if (pool is null)
            return At(conventionality);

        var weights = Weigh([(true, this), (false, new ByConvention(1 - Plain, 1 - Tuned, 1 - Wild))], conventionality, pool);
        return weights.Where(x => x.Value).Sum(x => x.Weight) / weights.Sum(x => x.Weight);
    }

    private static (T Option, ByConvention Weight, double Pool)[] Kept<T>(IEnumerable<(T Option, ByConvention Weight)> options, Func<T, double?> pool)
    {
        var kept = options.Select(x => (x.Option, x.Weight, Pool: pool(x.Option))).Where(x => x.Pool is not null).Select(x => (x.Option, x.Weight, Pool: x.Pool!.Value)).ToArray();
        if (kept.Length == 0)
            throw new InvalidOperationException("The pool keeps none of the options.");
        return kept;
    }

    /// <summary>Every option's share of each end, 0 at an end that allows none.</summary>
    private static ByConvention[] Shares(ByConvention[] weights)
    {
        var (plain, tuned, wild) = (weights.Sum(x => x.Plain), weights.Sum(x => x.Tuned), weights.Sum(x => x.Wild));
        static double Share(double weight, double sum) => sum > 0 ? weight / sum : 0;
        return [..weights.Select(x => new ByConvention(Share(x.Plain, plain), Share(x.Tuned, tuned), Share(x.Wild, wild)))];
    }

    /// <summary>
    ///     The options toward a pool's middles: the middle the pool's, each end the options' own shares of it, each times
    ///     the option's ratio, its pool's middle over its own, so that a pool leaves an option the plainer or the wilder as
    ///     the songs have it; and the wild end giving back what the pool takes from the options' own middle, an option
    ///     the pool makes rarer there than the songs have it the likelier at the wild end the more, even one at 0, which
    ///     then plays only the wilder a song is. An end left with none takes the middle's, and the middle with none the
    ///     wild end's; at the options' own middles the pool weighs them as they are.
    /// </summary>
    /// <param name="ratio">An option's ratio; none for one the songs have in no middle, whose ends are then the pool's middle.</param>
    private static ImmutableArray<Weighted<T>> Toward<T>((T Option, ByConvention Weight, double Pool)[] kept, ByConvention[] own, double[] middles, Func<int, double?> ratio, double conventionality)
    {
        var sum = middles.Sum();
        var middle = middles.Select(x => sum > 0 ? x / sum : 0).ToArray();
        double[] Leaned(Func<ByConvention, double> end) => [..own.Select((x, i) => ratio(i) is { } r ? end(x) * r : middle[i])];
        double[] Normal(double[] weights) => weights.Sum() is var total and > 0 ? [..weights.Select(x => x / total)] : weights;

        double[] Either(params double[][] ends) => ends.FirstOrDefault(x => x.Sum() > 0) ?? ends[0];
        var taken = own.Select((x, i) => Math.Max(0, x.Tuned - middle[i])).ToArray();
        var away = taken.Sum();
        var (plain, wild) = (Normal(Leaned(x => x.Plain)), Normal(Either(Leaned(x => x.Wild), middle)));
        wild = away > 0 ? [..wild.Zip(Normal(taken), (w, t) => (1 - away) * w + away * t)] : wild;
        (plain, var tuned, wild) = (Either(plain, middle, wild), Either(middle, wild), Either(wild, middle));
        return Weigh(kept.Select((x, i) => (x.Option, new ByConvention(plain[i], tuned[i], wild[i]))), conventionality);
    }
}