using System.Collections.Immutable;
using Rmg.Core.Songs;

namespace Rmg.Core.Rendering;

/// <summary>
///     How a song is heard, as asked: how loud it plays, and every part's and drum group's say over its own sound. It
///     leaves every note as the song decided it; a part or a drum group left out is not written at all.
/// </summary>
/// <param name="Volume">How loud the whole song plays, from 0 to 1, over every part's own volume.</param>
/// <param name="Parts">A part's mix, by its role; a part not named plays as the song has it.</param>
/// <param name="DrumGroups">A drum group's mix, by its name (<see cref="Composition.DrumGroups" />); a group not named plays as the song has it.</param>
public sealed record SongMix(double Volume, ImmutableDictionary<TrackRole, PartMix> Parts, ImmutableDictionary<string, DrumGroupMix> DrumGroups)
{
    /// <summary>The song as it was made.</summary>
    public static SongMix None { get; } = new(1, ImmutableDictionary<TrackRole, PartMix>.Empty, ImmutableDictionary<string, DrumGroupMix>.Empty);
}

/// <param name="Instrument">The General MIDI program it plays, the drums' kit; none for the song's own.</param>
/// <param name="Volume">How loud it plays, from 0 to 1.</param>
/// <param name="Pan">Where it sits, from -1, left, to 1, right; none for the song's own.</param>
/// <param name="IsOn">Whether it is written at all.</param>
public sealed record PartMix(int? Instrument, double Volume, double? Pan, bool IsOn);

/// <param name="Volume">How loud its drums play, from 0 to 1, as a part of every note's own loudness.</param>
/// <param name="IsOn">Whether its drums are written at all.</param>
public sealed record DrumGroupMix(double Volume, bool IsOn);
