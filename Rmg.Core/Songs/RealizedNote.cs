using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Core.Songs;

/// <summary>
///     A note as the song plays it: its pitches, one for a single note and several for a chord, or a drum's sound; its
///     loudness before the song's velocities are spread over the MIDI range; and its length, in beats. The state it
///     was decided from comes with it, for what it says of the note, such as its chord or how strong its beat is; a
///     change made to the note afterwards does not show in it.
/// </summary>
public sealed record RealizedNote(ImmutableArray<int> Pitches, double Velocity, double Duration, StateMap State);
