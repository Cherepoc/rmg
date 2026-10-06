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
/// <param name="Kit">The drum kit's sound, as the General MIDI program of the drums' channel (<see cref="GeneralMidi.KitName" />).</param>
/// <param name="Room">The room its reverb is, as the Roland GS reverb macro that sets it, none for the player's own (<see cref="GenreChoices.Room" />).</param>
/// <param name="Insertion">The effect it plays one part through, and the part, none for none (<see cref="GenreChoices.Insertion" />).</param>
/// <param name="Appearances">What every section the song plays does, in its order, as its map has them: its role and the parts that solo it, none for most.</param>
public sealed record SongDraws(Unconventionality Unconventionality, ImmutableHashSet<TrackRole> Parts, DrumSetup DrumSetup, ImmutableArray<string> DrumPieces, Scale Scale, Swing Swing, int Kit,
    int? Room, Insertion? Insertion, ImmutableArray<(SectionRole Role, ImmutableArray<TrackRole> Soloists)> Appearances)
{
    /// <summary>How much it swings, as a part of a triplet's swing: 0 straight, 1 the second of a pair a third late.</summary>
    public double SwingAmount => Swing.Delay * 6 / Swing.Period;
}
