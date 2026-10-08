namespace Rmg.Core.Composition;

/// <summary>The points of a song's generation that a <see cref="Events.StateTrace" /> records, by name.</summary>
internal static class TracePoints
{
    /// <summary>The effects a song plays at all (a <see cref="SongEffects" />).</summary>
    public const string SongEffects = "Song effects";

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

    /// <summary>A line the drums announce a bigger section at, opening the hi-hat and striking the snare's head (a <c>double</c>, the line's place).</summary>
    public const string LiftCue = "Lift cue";

    /// <summary>A section's planed chords' moves from its home, in semitones, none where it does not plane (an <c>ImmutableArray&lt;int&gt;?</c>).</summary>
    public const string Planing = "Planing";

    /// <summary>The song's later appearance reharmonised and where its substitutes start, none where it reharmonises none (a <c>(int, ImmutableArray&lt;double&gt;)?</c>).</summary>
    public const string Reharmonisation = "Reharmonisation";

    /// <summary>The parts a song pins to their figures (an <c>ImmutableHashSet</c> of <see cref="RhythmPart" />s).</summary>
    public const string PinnedDrums = "Pinned drums";

    /// <summary>Whether a section's bass plays in octaves (a <c>bool</c>).</summary>
    public const string OctaveBass = "Octave bass";

    /// <summary>Whether a section's bass alternates the root and the fifth (a <c>bool</c>; <see cref="Composition.SectionGenerator.AlternatingBassChance" />).</summary>
    public const string AlternatingBass = "Alternating bass";

    /// <summary>A section's pedal's root, in steps above its home, none where it holds none (an <c>int?</c>).</summary>
    public const string SectionPedal = "Section pedal";

    /// <summary>Whether a section moves a chromatic line inside its held chords (a <c>bool</c>).</summary>
    public const string InnerLine = "Inner line";

    /// <summary>The figures a section's players play in place of their own rhythms, by their players (an <c>ImmutableDictionary</c> of <see cref="FigurePlayer" /> and the figure's name, <see cref="Composition.Figures" />).</summary>
    public const string Figures = "Figures";

    /// <summary>Whether a section pushes its chord changes (a <c>bool</c>).</summary>
    public const string Push = "Push";

    /// <summary>The louder form a part plays in the song's louder appearances, its program, where it plays one (an <c>int</c>).</summary>
    public const string LouderForm = "Louder form";

    /// <summary>What the song's drums do, keep time or mark its form (a <see cref="Composition.DrumJob" />).</summary>
    public const string DrumJob = "Drum job";

    /// <summary>The drums marking the song's form, by their tracks, where its drums mark it (an <c>ImmutableArray</c> of <c>int</c>).</summary>
    public const string DrumMarks = "Drum marks";

    /// <summary>The ornaments the song's lines play (an <c>ImmutableArray</c> of <see cref="Composition.Ornament" />).</summary>
    public const string Ornaments = "Ornaments";

    /// <summary>Whether a section turns its pattern around, its last bar a V or a ii–V into its start (a <c>bool</c>).</summary>
    public const string Turnaround = "Turnaround";

    /// <summary>Whether a section plays its appearances' first phrase in stop time (a <c>bool</c>).</summary>
    public const string StopTime = "Stop time";

    /// <summary>A line a section builds to and drops at, the band out for the bar before it (a <c>double</c>, the line's place).</summary>
    public const string Drop = "Drop";

    /// <summary>Where a lift drops with the band playing on, filtered, over the bar before it (a <c>double</c>, its position).</summary>
    public const string FilteredDrop = "Filtered drop";

    /// <summary>Whether a song plays its last section once more (a <c>bool</c>).</summary>
    public const string LastRepeat = "Last repeat";

    /// <summary>
    ///     A line's walk into a change of section with the drums' run (<see cref="Composition.LineFills" />): the line the
    ///     section changes at and the walk's notes' places (a tuple of a <c>double</c> and an array of <c>double</c>s).
    /// </summary>
    public const string LineFill = "Line fill";

    /// <summary>
    ///     The parts the song holds back at its start, and the appearance each enters at, by its place among the song's
    ///     sections (an <c>ImmutableDictionary</c> of <see cref="Songs.TrackRole" />s and <c>int</c>s).
    /// </summary>
    public const string LateEntries = "Late entries";

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
    ///     The bass playing a driven riff's phrase it would rest in: the section's place in the song and the phrase's start
    ///     (a tuple of an <c>int</c> and a <c>double</c>).
    /// </summary>
    public const string RiffBass = "Riff bass";

    /// <summary>
    ///     Whether a section the solo part may answer the melody in does, outside its solos and where the counter-melody
    ///     rests: the section's place and whether it answers (a tuple of an <c>int</c> and a <c>bool</c>).
    /// </summary>
    public const string SoloAnswers = "Solo answers";

    /// <summary>A part that solos in the song, and the soloist it is (a tuple of a <see cref="Songs.TrackRole" /> and a <see cref="Soloist" />).</summary>
    public const string Soloist = "Soloist";

    /// <summary>
    ///     A pitched solo's segments: the appearance's place, whether the solo is of its own rather than on the melody, and
    ///     its plan (a tuple of an <c>int</c>, a <c>bool</c> and an array of <see cref="SoloLines.Segment" />s).
    /// </summary>
    public const string SoloGestures = "Solo gestures";

    /// <summary>A drum solo's segments: the appearance's place and its plan (a tuple of an <c>int</c> and an array of <see cref="SoloLines.Segment" />s).</summary>
    public const string DrumSolo = "Drum solo";

    /// <summary>
    ///     A solo's run or lick: its start, its shape and the sizes of its cells in turn (a tuple of a <c>double</c>, a
    ///     <see cref="Composition.SoloShape" /> and an array of <c>int</c>s).
    /// </summary>
    public const string SoloCells = "Solo cells";

    /// <summary>
    ///     A pitched solo's line as its soloists take it: the appearance's place and how many of the melody's notes there are
    ///     to solo on (a tuple of two <c>int</c>s), none leaving the parts to play as they would.
    /// </summary>
    public const string SoloLine = "Solo line";

    /// <summary>
    ///     Whether a part that may play its line in octaves in a section does: the section's place, the part, the
    ///     section's energy and whether it does (a tuple of an <c>int</c>, a <see cref="Songs.TrackRole" />, a
    ///     <c>double</c> and a <c>bool</c>).
    /// </summary>
    public const string Octave = "Octave";

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

    /// <summary>
    ///     A stage of the song after it is put together, as it ends: its name and how many events or notes every track has
    ///     (a tuple of a <c>string</c> and an <c>ImmutableSortedDictionary</c> of tracks and counts; <see cref="SongStages" />).
    /// </summary>
    public const string SongStage = "Song stage";

    /// <summary>Whether a section's guitars and picked bass chug, their repeats palm-muted (a <c>bool</c>).</summary>
    public const string Chugs = "Chugs";

    /// <summary>Whether a section's riff plays in power chords (a <c>bool</c>).</summary>
    public const string RiffPowerChords = "Riff power chords";

    /// <summary>Whether a section's melody is on a hook, its repeated bars singing their first's notes (a <c>bool</c>).</summary>
    public const string MelodyHook = "Melody hook";

    /// <summary>A section's phrase scheme, which bars repeat which (a <see cref="Composition.PhraseScheme" />).</summary>
    public const string PhraseScheme = "Phrase scheme";

    /// <summary>A section's chords' own scales, in the progression's order (<see cref="ChordScale" />).</summary>
    public const string ChordScales = "Chord scales";

    /// <summary>How far a song's harmony strays from convention (a <see cref="HarmonicUnconventionality" />), and its scale.</summary>
    public const string SongHarmony = "Song harmony";

    /// <summary>How far a section's harmony strays from convention (a <see cref="HarmonicUnconventionality" />).</summary>
    public const string SectionHarmony = "Section harmony";

    /// <summary>How far a song's rhythm strays from convention (a <see cref="RhythmicUnconventionality" />).</summary>
    public const string SongRhythm = "Song rhythm";

    /// <summary>
    ///     The key changes led into by a chain of chords: each change's place and its chain's chords' starts (an
    ///     <c>ImmutableArray</c> of tuples of a <c>double</c> and an <c>ImmutableArray</c> of <c>double</c>s).
    /// </summary>
    public const string KeyPreparation = "Key preparation";

    /// <summary>A song's sections in its order and their roles (a <see cref="SongStructure" />).</summary>
    public const string SongForm = "Song form";

    /// <summary>The parts a song leaves out (an <c>ImmutableHashSet</c> of <see cref="Songs.TrackRole" />).</summary>
    public const string SongParts = "Song parts";

    /// <summary>Where a song goes up a key, and by how many semitones (a <see cref="Composition.KeyChange" />, or null for none).</summary>
    public const string KeyChange = "Key change";

    /// <summary>Every appearance's tempo as a multiple of the song's, by its place among the song's sections (an <c>ImmutableArray&lt;double&gt;</c>).</summary>
    public const string SectionTempo = "Section tempo";

    /// <summary>Every section's meter, by its id (an <c>ImmutableDictionary&lt;int, Meter&gt;</c>).</summary>
    public const string SectionMeter = "Section meter";

    /// <summary>Every section's polymeter, by its id, for those that play one (an <c>ImmutableDictionary&lt;int, Polymeter.Plan&gt;</c>).</summary>
    public const string Polymeter = "Polymeter";

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

    /// <summary>A section's appearance's drum edits, its place and how many (a <c>(int, int)</c>; <see cref="Composition.Mutations" />).</summary>
    public const string Mutations = "Mutations";

    /// <summary>Where the song's whole mix is filtered (<see cref="Composition.MixFilterPlan" />).</summary>
    public const string MixFilter = "Mix filter";

    /// <summary>Whether a section is a heavy band's breakdown (a <c>bool</c>; <see cref="Composition.Breakdowns" />).</summary>
    public const string Breakdown = "Breakdown";

    /// <summary>Whether a section's snare plays ghost notes (a <c>bool</c>; <see cref="Composition.Ghosts" />).</summary>
    public const string Ghosts = "Ghosts";

    /// <summary>How the song ends, in words.</summary>
    public const string SongEnding = "Song ending";
}
