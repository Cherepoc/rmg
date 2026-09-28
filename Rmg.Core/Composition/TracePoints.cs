namespace Rmg.Core.Composition;

/// <summary>The points of a song's generation that a <see cref="Events.StateTrace" /> records, by name.</summary>
public static class TracePoints
{
    /// <summary>A section's energy, with its pull (<see cref="SectionEnergyTrace" />).</summary>
    public const string SectionEnergy = "Section energy";

    /// <summary>A section's scale (<see cref="Scale" />).</summary>
    public const string SectionScale = "Section scale";

    /// <summary>The register a section's melody aims at in each bar of its pattern (an <c>ImmutableArray</c> of doubles).</summary>
    public const string MelodyContour = "Melody contour";

    /// <summary>Whether a section plays its percussion without the drum kit (a <c>bool</c>).</summary>
    public const string PercussionOnly = "Percussion only";

    /// <summary>The bars a section's optional drums sit out (an <c>ImmutableHashSet</c> of their tracks and letters).</summary>
    public const string DrumPresence = "Drum presence";

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
