namespace Rmg.Core.Composition;

/// <summary>The points of a song's generation that a <see cref="Events.StateTrace" /> records, by name.</summary>
public static class TracePoints
{
    /// <summary>A section's energy, with its pull (<see cref="SectionEnergyTrace" />).</summary>
    public const string SectionEnergy = "Section energy";

    /// <summary>A section's scale (<see cref="Scale" />).</summary>
    public const string SectionScale = "Section scale";

    /// <summary>A bar pattern's state, with its phrase scheme.</summary>
    public const string BarPattern = "Bar pattern";

    /// <summary>The chord a note plays over.</summary>
    public const string Chord = "Chord";

    /// <summary>What a line's fill and landing are (<see cref="FillDecision" />).</summary>
    public const string FillDecision = "Fill decision";

    /// <summary>A fill's hit, or a landing's, by its kind in words.</summary>
    public const string Fill = "Fill";

    /// <summary>How the song starts, in words.</summary>
    public const string SongIntro = "Song intro";

    /// <summary>How the song ends, in words.</summary>
    public const string SongEnding = "Song ending";
}
