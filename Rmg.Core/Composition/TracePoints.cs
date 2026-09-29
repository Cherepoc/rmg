namespace Rmg.Core.Composition;

/// <summary>The points of a song's generation that a <see cref="Events.StateTrace" /> records, by name.</summary>
internal static class TracePoints
{
    /// <summary>A section's energy, with its pull (<see cref="SectionEnergyTrace" />).</summary>
    public const string SectionEnergy = "Section energy";

    /// <summary>A section's scale (<see cref="Scale" />).</summary>
    public const string SectionScale = "Section scale";

    /// <summary>The register a section's melody aims at in each bar of its pattern (an <c>ImmutableArray</c> of doubles).</summary>
    public const string MelodyContour = "Melody contour";

    /// <summary>How far a song's rhythm strays from convention (a <see cref="RhythmicUnconventionality" />).</summary>
    public const string SongRhythm = "Song rhythm";

    /// <summary>How a song swings (a <see cref="Swing" />).</summary>
    public const string Swing = "Swing";

    /// <summary>Where a song's pitched tracks sit from left to right (an <c>ImmutableDictionary</c> of tracks and pans).</summary>
    public const string Panning = "Panning";

    /// <summary>What a song's drums are (a <see cref="DrumSetup" />).</summary>
    public const string DrumSetup = "Drum setup";

    /// <summary>The drums a section plays, its leads and the drums that double them (a <see cref="SectionKit" />).</summary>
    public const string Kit = "Kit";

    /// <summary>The drums a section has double a lead (an <c>ImmutableDictionary</c> of drum tracks and their <see cref="Doubling" />s).</summary>
    public const string Doubles = "Doubles";

    /// <summary>
    ///     The drums a section has play on the feel of a lead, bound to it or colouring its role (an
    ///     <c>ImmutableDictionary</c> of drum tracks and their leads' tracks).
    /// </summary>
    public const string FeelLeads = "FeelLeads";

    /// <summary>The roles a section draws again for the song's drums (an <c>ImmutableDictionary</c> of drum tracks and <see cref="DrumRole" />s).</summary>
    public const string DrumRoles = "Drum roles";

    /// <summary>The strokes a section changes from the song's (an <c>ImmutableDictionary</c> of drum tracks and sound indices).</summary>
    public const string DrumStrokes = "Drum strokes";

    /// <summary>Whether a section plays its percussion without the drum kit (a <c>bool</c>).</summary>
    public const string PercussionOnly = "Percussion only";

    /// <summary>How a section's drums play the bars of its later letters (a <see cref="BarDrums" />).</summary>
    public const string DrumPresence = "Drum presence";

    /// <summary>A section's chance of its melody leading into a chord change within a phrase (a <c>double</c>).</summary>
    public const string MelodyLeading = "Melody leading";

    /// <summary>The chance a note of a section's melody's answer is mutated from the question's (a <c>double</c>).</summary>
    public const string MelodyAnswer = "Melody answer";

    /// <summary>How much a song improvises its melody as its sections recur (a <c>double</c>, <see cref="Composition.MelodyLayers.Improvisation" />).</summary>
    public const string MelodyImprovisation = "Melody improvisation";

    /// <summary>How freely a section's melody changes register where a phrase starts (a <c>double</c>, <see cref="Composition.SectionLine.RegisterFreedom" />).</summary>
    public const string LineRegisterFreedom = "Line register freedom";

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
