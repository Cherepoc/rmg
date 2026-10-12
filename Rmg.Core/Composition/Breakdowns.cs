using System.Collections.Immutable;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A heavy band's breakdown: a section played heavier and sparser, its drums in half time and their kick striking with
///     the riff, the riff chugging on its pedal, the lines and the pad laying out; a bridge's or a section of a free form's,
///     never the song's first, a genre's alone. Made of the devices there already: half time (<see cref="Groove.DrawTimeFeel" />),
///     the chugs (<see cref="Chugs" />) and the kick's figure with the riff (<see cref="Figures" />).
/// </summary>
internal static class Breakdowns
{
    /// <summary>The chance a section breaks down, by its form facet: the songs' never; a genre's where it names it.</summary>
    public static ByConvention Chance { get; } = new(0, 0, 0);

    /// <summary>The parts a breakdown leaves out, so that the riff, the bass and the drums carry it.</summary>
    public static ImmutableHashSet<TrackRole> Resting { get; } = [TrackRole.Melody, TrackRole.CounterMelody, TrackRole.Pad];

    /// <summary>The parts that carry a breakdown, which play through it whatever the section drew.</summary>
    public static ImmutableHashSet<TrackRole> Playing { get; } = [TrackRole.Riff, TrackRole.Bass, TrackRole.Drum];

    /// <summary>Whether a section may break down: a bridge, or a section of a free form, but the song's first.</summary>
    public static bool MayBreakDown(SectionRole role, bool isFirst) => role is SectionRole.Bridge or SectionRole.Free && !isFirst;
}
