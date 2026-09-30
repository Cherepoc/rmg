namespace Rmg.Core.Songs;

/// <summary>What a track plays in a song, which the generation asks of it, not its number.</summary>
public enum TrackRole
{
    Chords,
    Melody,
    Bass,

    /// <summary>Held chords under the band, a chord at every change, as strings or a synth pad play them.</summary>
    Pad,

    /// <summary>A drum, one of the song's, which plays in its group's rhythm.</summary>
    Drum
}
