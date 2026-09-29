using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     How loud a note is by how strong its beat is: an accent fixed by the beat's rank in its rhythm pattern, from 0,
///     the strongest, to the pattern's max rank, the weakest, and a variation drawn around it; both as far as the
///     track's dynamics have them (<see cref="CompositionStateKinds.NoteDynamics" />), so that a bass hits its beats
///     alike and a melody moves more.
/// </summary>
public static class BeatAccent
{
    /// <summary>How much louder the strongest beat of a pattern is than its weakest, before the track's dynamics.</summary>
    public const double StrongestAccent = 0.6;

    /// <summary>How much of what is left of the way to the weakest beat each weaker rank keeps: the steps halve.</summary>
    private const double StepRatio = 0.5;

    private static readonly Func<IGenerationContext, double> Variation = Generators.SplineValue();

    /// <summary>
    ///     The accent of a beat: <see cref="StrongestAccent" /> at rank 0 and none at the max rank, falling faster than
    ///     the ranks, every step towards the weakest half the one before, so 0.6, 0.07, 0 over three ranks. A pattern
    ///     of a single rank has nothing to set apart.
    /// </summary>
    public static double GetAccent(int rank, int maxRank)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rank);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rank, maxRank);

        if (maxRank == 0)
            return 0;

        var share = (1 - Math.Pow(StepRatio, rank)) / (1 - Math.Pow(StepRatio, maxRank));
        return StrongestAccent * Math.Pow(1 - share, 2);
    }

    /// <summary>How far a note's velocity moves when its beat takes another rank, its variation kept.</summary>
    public static double GetShift(int rank, int newRank, int maxRank, double dynamics = 1)
    {
        return dynamics * (GetAccent(newRank, maxRank) - GetAccent(rank, maxRank));
    }

    /// <summary>A note's velocity by its beat: its accent and a variation drawn around it, times the dynamics.</summary>
    /// <param name="dynamics">How far the track's notes move from its level, 1 as tuned.</param>
    public static Func<IGenerationContext, double> CreateVelocityGenerator(int rank, int maxRank, double dynamics = 1)
    {
        var accent = GetAccent(rank, maxRank);
        return context => dynamics * (accent + Variation(context));
    }
}
