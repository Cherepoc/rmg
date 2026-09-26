namespace Rmg.Core.Events;

/// <summary>
///     The finest positions a timeline item takes, in beats. A position computed from a tuplet period, such as 4/3,
///     is not exact in binary, so two ways to the same moment can end a hair apart and play as two; snapping both to
///     the grid makes them one.
/// </summary>
public static class TimelineGrid
{
    /// <summary>
    ///     2^10 · 3 · 5 · 7: every subdivision down to 1/1024 of a beat and every triplet, quintuplet and septuplet of
    ///     them falls on the grid exactly. The tuplets of 11 and 13 fall between, to the nearest tick.
    /// </summary>
    public const int TicksPerBeat = 1024 * 3 * 5 * 7;

    public static double Snap(double position)
    {
        return Math.Round(position * TicksPerBeat) / TicksPerBeat;
    }
}
