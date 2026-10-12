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
    RiffTwin,

    /// <summary>A soloist, as a lead guitar or a sax takes a chorus, which plays only where a section's appearance is its solo.</summary>
    Solo
}

public static class TrackRoles
{
    /// <summary>Whether the part plays the chords, voiced and led from one to the next: the chords and the rhythm part.</summary>
    public static bool PlaysChords(this TrackRole role) => role is TrackRole.Chords or TrackRole.Rhythm;

    /// <summary>
    ///     Whether the part plays notes of its own in a section, as all do but the riff's twin, which plays the riff's, and
    ///     the solo part, which plays only a solo's: the parts a section can play its texture with.
    /// </summary>
    public static bool HasNotesOfItsOwn(this TrackRole role) => role is not (TrackRole.RiffTwin or TrackRole.Solo);

    /// <summary>Whether the part plays lines, single notes one after another, rather than chords, a pad or drums.</summary>
    public static bool PlaysLine(this TrackRole role) => role is TrackRole.Melody or TrackRole.Bass or TrackRole.CounterMelody or TrackRole.Riff or TrackRole.RiffTwin or TrackRole.Solo;

    /// <summary>Whether the part sings over the band, a voice's lines: the melody, the counter-melody and the solo part.</summary>
    public static bool Sings(this TrackRole role) => role is TrackRole.Melody or TrackRole.CounterMelody or TrackRole.Solo;

    /// <summary>The part a part plays as: the riff for its twin, which plays the riff's line, and any other itself.</summary>
    public static TrackRole Source(this TrackRole role) => role == TrackRole.RiffTwin ? TrackRole.Riff : role;

    /// <summary>Each part's track by its role, the first of its tracks for a part of several, as the drums.</summary>
    public static IReadOnlyDictionary<TrackRole, int> TrackOf(this IReadOnlyDictionary<int, TrackRole> roles) =>
        roles.GroupBy(x => x.Value).ToDictionary(x => x.Key, x => x.First().Key);

    /// <summary>A part's name as a genre's page shows it, where a part names an option of a choice, such as its level.</summary>
    public static string LabelOf(TrackRole role) => role switch
    {
        TrackRole.CounterMelody => "Counter-melody",
        TrackRole.Drum => "Drums",
        TrackRole.RiffTwin => "Riff's twin",
        TrackRole.Solo => "Soloist",
        _ => role.ToString()
    };
}
