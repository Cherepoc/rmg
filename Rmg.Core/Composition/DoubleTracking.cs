using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A driven guitar's part played twice, as a mix doubles a heavy guitar from side to side: the riff, its twin, the
///     rhythm part and the chords, on a driven guitar, each all the way to a side and its second take all the way to the
///     other, on the other driven guitar, so that the two sound apart rather than as one in the middle, a little late, as
///     a second take is, and each as loud as the two together are the part alone.
/// </summary>
internal static class DoubleTracking
{
    /// <summary>How late the second take plays, in seconds: close enough not to flam a chug, apart enough to widen.</summary>
    public const double Delay = 0.008;

    /// <summary>How loud each take plays, as a part of the part's volume, so that the two sound as loud as the part.</summary>
    public static readonly double Level = Math.Sqrt(0.5);

    /// <summary>Whether a part on the program given is played twice.</summary>
    public static bool Doubles(TrackRole role, int program) =>
        role is TrackRole.Riff or TrackRole.RiffTwin or TrackRole.Rhythm or TrackRole.Chords && InstrumentTraits.Has(program, InstrumentTrait.Driven);

    /// <summary>The second take's program: the other driven guitar, overdriven for distortion and back, or the same for any other.</summary>
    public static int SecondTake(int program) => program switch
    {
        29 => 30,
        30 => 29,
        _ => program
    };
}
