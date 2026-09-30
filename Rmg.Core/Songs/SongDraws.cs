using System.Collections.Immutable;
using Rmg.Core.Composition;

namespace Rmg.Core.Songs;

/// <summary>What a generated song drew, or was given in place of drawing (<see cref="SongOverrides" />).</summary>
/// <param name="Unconventionality">How far it strays from convention, as a whole and by every facet.</param>
/// <param name="Parts">The parts it has (<see cref="SongParts" />), which its sections play or rest.</param>
/// <param name="DrumSetup">The drums it plays.</param>
public sealed record SongDraws(Unconventionality Unconventionality, ImmutableHashSet<TrackRole> Parts, DrumSetup DrumSetup);
