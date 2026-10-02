using System.Collections.Immutable;
using Rmg.Core.Composition;

namespace Rmg.Core.Songs;

/// <summary>What a generated song drew, or was given in place of drawing (<see cref="SongOverrides" />).</summary>
/// <param name="Unconventionality">How far it strays from convention, as a whole and by every facet.</param>
/// <param name="Parts">The parts it has (<see cref="SongParts" />), which its sections play or rest.</param>
/// <param name="DrumSetup">The drums it plays.</param>
/// <param name="DrumPieces">Its drum pieces, by their names (<see cref="Composition.DrumPieces" />).</param>
/// <param name="Scale">The scale it starts in.</param>
/// <param name="Swing">How it swings.</param>
public sealed record SongDraws(Unconventionality Unconventionality, ImmutableHashSet<TrackRole> Parts, DrumSetup DrumSetup, ImmutableArray<string> DrumPieces, Scale Scale, Swing Swing)
{
    /// <summary>How much it swings, as a part of a triplet's swing: 0 straight, 1 the second of a pair a third late.</summary>
    public double SwingAmount => Swing.Delay * 6 / Swing.Period;
}
