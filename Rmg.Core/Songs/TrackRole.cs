namespace Rmg.Core.Songs;

/// <summary>What a track plays in a song, which the generation asks of it, not its number.</summary>
public enum TrackRole
{
    Chords,
    Melody,
    Bass,

    /// <summary>Held chords under the band, a chord at every change, as strings or a synth pad play them.</summary>
    Pad,

    /// <summary>A second line under the melody, slower, as strings or a horn play one.</summary>
    CounterMelody,

    /// <summary>A drum, one of the song's, which plays in its group's rhythm.</summary>
    Drum,

    /// <summary>A short figure played again and again, as a guitar or a synth plays a riff, under the melody or alone.</summary>
    Riff,

    /// <summary>A second part playing the chords, in a rhythm of its own, as a rhythm guitar comps beside the keys.</summary>
    Rhythm,

    /// <summary>The riff's twin, its line a third above or a sixth below, on the other side, as twin guitars play a riff.</summary>
    RiffTwin
}

public static class TrackRoles
{
    /// <summary>Whether the part plays the chords, voiced and led from one to the next: the chords and the rhythm part.</summary>
    public static bool PlaysChords(this TrackRole role) => role is TrackRole.Chords or TrackRole.Rhythm;
}
