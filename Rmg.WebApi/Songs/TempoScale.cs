using Rmg.Core.Composition;

namespace Rmg.WebApi.Songs;

/// <summary>
///     The tempos a song may be given, in the 62 steps of <see cref="StepScale" />, from a third of
///     <see cref="Meter.BaseTempo" /> to twice it, the base tempo at the middle step and each step a like ratio from the
///     next on either side of it: 40, 120 and 240 beats a minute, as slow as a genre's tempo reaches.
/// </summary>
public static class TempoScale
{
    public const int Steps = StepScale.Steps;

    /// <summary>How many times the base tempo the fastest step is, and the slowest a part of it.</summary>
    private const double Fastest = 2, Slowest = 3;

    /// <summary>A step's tempo, as a multiple of <see cref="Meter.BaseTempo" />.</summary>
    public static double Multiplier(int step) => StepScale.ToAmount(step) is var amount && amount >= 0.5
        ? Math.Pow(Fastest, 2 * amount - 1)
        : Math.Pow(Slowest, 2 * amount - 1);

    /// <summary>The step nearest a tempo, as a multiple of <see cref="Meter.BaseTempo" />, by its ratio to it.</summary>
    public static int StepOf(double multiplier) => StepScale.Nearest(Multiplier, multiplier);
}
