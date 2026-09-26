using System.Collections.Immutable;

namespace Rmg.Core.Composition;

/// <summary>The General MIDI sounds of the drums that the fills name, by their note numbers.</summary>
public static class DrumSounds
{
    public const int OpenHiHat = 46;

    public const int LowFloorTom = 41;
    public const int HighFloorTom = 43;
    public const int LowTom = 45;
    public const int LowMidTom = 47;
    public const int HighMidTom = 48;
    public const int HighTom = 50;

    public const int CrashCymbal1 = 49;
    public const int CrashCymbal2 = 57;

    /// <summary>The toms from the highest down, as a run down them plays them.</summary>
    public static ImmutableArray<int> TomsHighToLow { get; } = [HighTom, HighMidTom, LowMidTom, LowTom, HighFloorTom, LowFloorTom];
}
