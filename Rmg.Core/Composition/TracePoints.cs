namespace Rmg.Core.Composition;

/// <summary>The points of a song's generation that a <see cref="Events.StateTrace" /> records, by name.</summary>
internal static class TracePoints
{
    /// <summary>A section's energy, with its pull (<see cref="SectionEnergyTrace" />).</summary>
    public const string SectionEnergy = "Section energy";

    /// <summary>How a section's drums keep time: 1 half time, -1 double time, 0 the song's (an <c>int</c>).</summary>
    public const string TimeFeel = "Time feel";

    /// <summary>A section's melody's or riff's line scale, by its track (a <see cref="Composition.LineScale" />).</summary>
    public const string LineScale = "LineScale";

    /// <summary>How often a section's chords change (a <see cref="HarmonicRhythm" />).</summary>
    public const string HarmonicRhythm = "Harmonic rhythm";

    /// <summary>
    ///     The parts a section leaves out as it plays, once for every section the song plays, in its order (an
    ///     <c>ImmutableHashSet</c> of <see cref="Songs.TrackRole" />s).
    /// </summary>
    public const string Arrangement = "Arrangement";

    /// <summary>
    ///     An appearance's texture: its place among the song's sections, its kind and the parts each phrase leaves out (a
    ///     tuple of an <c>int</c>, a <see cref="Composition.TextureKind" /> and an <c>ImmutableArray</c> of
    ///     <c>ImmutableHashSet</c>s of <see cref="Songs.TrackRole" />s).
    /// </summary>
    public const string Texture = "Texture";

    /// <summary>
    ///     A line doubled in a section: the section's place in the song, the part doubled, the part doubling and the scale
    ///     steps between them (a tuple of an <c>int</c>, two <see cref="Songs.TrackRole" />s and an <c>int</c>).
    /// </summary>
    public const string LineDoubling = "Line doubling";

    /// <summary>
    ///     A solo: its appearance's place among the song's sections, and its plan (a tuple of an <c>int</c> and a
    ///     <see cref="Composition.SoloPlan" />).
    /// </summary>
    public const string Solo = "Solo";

    /// <summary>How a section's chords are broken (an <see cref="Composition.ArpeggioPattern" />).</summary>
    public const string Arpeggio = "Arpeggio";

    /// <summary>
    ///     A part's instrument in a section, where it switches or articulates: the section's place among the song's, the
    ///     part, its program, how it articulates and the variant (a tuple of an <c>int</c>, a <see cref="Songs.TrackRole" />,
    ///     an <c>int</c>, an <see cref="Composition.ArticulationMode" /> and an <c>int</c>).
    /// </summary>
    public const string InstrumentOverTime = "Instrument over time";

    /// <summary>How many times a section plays its 4-bar pattern (an <c>int</c>).</summary>
    public const string SectionLength = "Section length";

    /// <summary>A section's scale (<see cref="Scale" />).</summary>
    public const string SectionScale = "Section scale";

    /// <summary>The register a section's melody aims at in each bar of its pattern (an <c>ImmutableArray</c> of doubles).</summary>
    public const string MelodyContour = "Melody contour";

    /// <summary>The feel a song plays in, or a section changes it to (an <c>int</c>, a step of the rhythm's period, see <see cref="Feels" />).</summary>
    public const string Feel = "Feel";

    /// <summary>How far a song strays from convention, its base and every facet (an <see cref="Unconventionality" />).</summary>
    public const string SongUnconventionality = "Song unconventionality";

    /// <summary>How far a section strays from convention, every facet (an <see cref="Unconventionality" />).</summary>
    public const string SectionUnconventionality = "Section unconventionality";

    /// <summary>A section's progression, its chords' roots in steps above its home (an <c>ImmutableArray</c> of <c>int</c>).</summary>
    public const string Progression = "Progression";

    /// <summary>
    ///     The step a section's cadence could raise, none where the scale allows none, and whether it does (an
    ///     <c>(int?, bool)</c>).
    /// </summary>
    public const string CadenceRaise = "Cadence raise";

    /// <summary>How far a song's harmony strays from convention (a <see cref="HarmonicUnconventionality" />), and its scale.</summary>
    public const string SongHarmony = "Song harmony";

    /// <summary>How far a section's harmony strays from convention (a <see cref="HarmonicUnconventionality" />).</summary>
    public const string SectionHarmony = "Section harmony";

    /// <summary>How far a song's rhythm strays from convention (a <see cref="RhythmicUnconventionality" />).</summary>
    public const string SongRhythm = "Song rhythm";

    /// <summary>A song's sections in its order and their roles (a <see cref="SongStructure" />).</summary>
    public const string SongForm = "Song form";

    /// <summary>The parts a song leaves out (an <c>ImmutableHashSet</c> of <see cref="Songs.TrackRole" />).</summary>
    public const string SongParts = "Song parts";

    /// <summary>Where a song goes up a key, and by how many semitones (a <see cref="Composition.KeyChange" />, or null for none).</summary>
    public const string KeyChange = "Key change";

    /// <summary>Every section's tempo as a multiple of the song's, by its id (an <c>ImmutableDictionary&lt;int, double&gt;</c>).</summary>
    public const string SectionTempo = "Section tempo";

    /// <summary>Every section's meter, by its id (an <c>ImmutableDictionary&lt;int, Meter&gt;</c>).</summary>
    public const string SectionMeter = "Section meter";

    /// <summary>How a song swings (a <see cref="Swing" />).</summary>
    public const string Swing = "Swing";

    /// <summary>Where a song's pitched tracks sit from left to right (an <c>ImmutableDictionary</c> of tracks and pans).</summary>
    public const string Panning = "Panning";

    /// <summary>The meter a song's bars are in (a <see cref="Meter" />).</summary>
    public const string Meter = "Meter";

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
