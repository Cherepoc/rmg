using System.Collections.Immutable;

namespace Rmg.Core.Probabilities;

/// <summary>
///     How far draws lean to one side, as the log of the odds of their high side over their low side: 0, the default,
///     leaves them even, more leans them high and less low. A tilt makes an outcome likelier, never certain, and draws
///     what the untilted draw would, so that no tilt changes nothing. It leans a value around 0 by its skew, and an
///     option of a choice, a step or a chance by how the option leans itself, from -1, low, through 0 to 1, high.
/// </summary>
public readonly record struct Tilt(double LogOdds)
{
    public static Tilt None { get; } = default;

    /// <summary>A tilt of the odds of <paramref name="scale" /> to the power of <paramref name="lean" />.</summary>
    public static Tilt Of(double scale, double lean)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scale);

        return new Tilt(lean * Math.Log(scale));
    }

    /// <summary>The odds of the high side over the low side.</summary>
    public double Odds => Math.Exp(LogOdds);

    /// <summary>The chance of the high side: 1/2 with no tilt.</summary>
    public double HighChance => 1 / (1 + Math.Exp(-LogOdds));

    /// <summary>The skew of a value around 0 (<see cref="Generators.SplineValue" />) that is positive with the high chance.</summary>
    public double Skew => Math.Log(0.5) / Math.Log(1 - HighChance);

    /// <summary>A value around 0, as <see cref="Generators.SplineValue" /> draws it, leaning to the high side.</summary>
    public Func<IGenerationContext, double> SplineValue(double c = 1)
    {
        return Generators.SplineValue(c, Skew);
    }

    /// <summary>
    ///     A step of -1 or 1 with the chance given, each as likely, and 0 otherwise, as a choice of the three that
    ///     leans: -1 low, 0 not at all and 1 high, so that a tilted step is likelier, and likelier to go the tilt's way.
    ///     It draws whether it steps, then which way, as the untilted step does.
    /// </summary>
    public Func<IGenerationContext, int> Step(double chance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(chance);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(chance, 1);

        var low = Weigh(chance / 2, -1);
        var high = Weigh(chance / 2, 1);
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        var stepChance = LogOdds == 0 ? chance : (low + high) / (low + high + (1 - chance));
        var lowChance = LogOdds == 0 ? 0.5 : low / (low + high);
        return context =>
        {
            if (!context.TestProbability(stepChance))
                return 0;

            return context.GenerateDouble() < lowChance ? -1 : 1;
        };
    }

    /// <summary>An option's weight, times the odds to the power of how the option leans.</summary>
    public double Weigh(double weight, double lean)
    {
        return weight * Math.Exp(LogOdds * lean);
    }

    /// <summary>The options' weights, each times the odds to the power of how it leans.</summary>
    public ImmutableArray<Weighted<T>> Weigh<T>(IEnumerable<Weighted<T>> options, Func<T, double> lean)
    {
        var tilt = this;
        return [..options.Select(x => x with { Weight = tilt.Weigh(x.Weight, lean(x.Value)) })];
    }

    /// <summary>A chance whose odds are times the tilt's odds to the power of how it leans, so that it stays a chance.</summary>
    public double Chance(double chance, double lean)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(chance);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(chance, 1);

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (chance is 0 or 1 || lean * LogOdds == 0)
            return chance;

        var odds = chance / (1 - chance) * Math.Exp(LogOdds * lean);
        return odds / (1 + odds);
    }
}
