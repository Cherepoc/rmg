using Rmg.Core.Composition;

namespace Rmg.WebApi.Songs;

/// <summary>
///     The tempos a song may be given, in the 62 steps of <see cref="StepScale" />, from half
///     <see cref="Meter.BaseTempo" /> to twice it, the base tempo at the middle step and each step a like ratio from the
///     next on either side of it: 60, 120 and 240 beats a minute.
/// </summary>
public static class TempoScale
{
    public const int Steps = StepScale.Steps;

    public const int Middle = StepScale.Middle;

    /// <summary>A step's tempo, as a multiple of <see cref="Meter.BaseTempo" />.</summary>
    public static double Multiplier(int step) => Math.Pow(2, 2 * StepScale.ToAmount(step) - 1);

    /// <summary>The step nearest a tempo, as a multiple of <see cref="Meter.BaseTempo" />, by its ratio to it.</summary>
    public static int StepOf(double multiplier) => StepScale.Nearest(Multiplier, multiplier);
}
