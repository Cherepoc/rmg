using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     How loud a note is likely to be by how strong its beat is. A beat's rank in its rhythm pattern goes from 0, the
///     strongest, to the pattern's max rank, the weakest. The velocity of a note on a strong beat is tipped towards
///     loud and spread wide, and on the weakest beats it is even and kept close to the middle.
/// </summary>
public static class BeatAccent
{
    /// <summary>The skew of the strongest beat, which makes it louder than the middle 94% of the time.</summary>
    private const double StrongestSkew = 0.25;

    /// <summary>How much of what is left of the skew each weaker rank keeps: the steps towards 1 halve.</summary>
    private const double SkewStepRatio = 0.5;

    private const double StrongestSpread = 0.5;

    private const double WeakestSpread = 2;

    /// <summary>
    ///     The skew of the velocity of a beat: <see cref="StrongestSkew" /> at rank 0 and 1 at the max rank, with every
    ///     step towards 1 half the one before, so 0.25, 0.75, 1 over three ranks. A pattern of a single rank has
    ///     nothing to set apart and is not skewed.
    /// </summary>
    public static double GetVelocitySkew(int rank, int maxRank)
    {
        Validate(rank, maxRank);

        if (maxRank == 0)
            return 1;

        var share = (1 - Math.Pow(SkewStepRatio, rank)) / (1 - Math.Pow(SkewStepRatio, maxRank));
        return StrongestSkew + (1 - StrongestSkew) * share;
    }

    /// <summary>
    ///     The spread (the spline's c) of the velocity of a beat: from <see cref="StrongestSpread" /> at rank 0 to
    ///     <see cref="WeakestSpread" /> at the max rank, the rank being the power, so 0.5, 1, 2 over three ranks. A
    ///     pattern of a single rank keeps the usual spread of 1.
    /// </summary>
    public static double GetVelocitySpread(int rank, int maxRank)
    {
        Validate(rank, maxRank);

        if (maxRank == 0)
            return 1;

        return StrongestSpread * Math.Pow(WeakestSpread / StrongestSpread, (double)rank / maxRank);
    }

    public static Func<IGenerationContext, double> CreateVelocityGenerator(int rank, int maxRank)
    {
        return Generators.SplineValue(GetVelocitySpread(rank, maxRank), GetVelocitySkew(rank, maxRank));
    }

    private static void Validate(int rank, int maxRank)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rank);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rank, maxRank);
    }
}
