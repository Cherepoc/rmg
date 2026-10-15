using System.Collections.Immutable;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     The choices a genre may speak of (<see cref="Genre" />), each one the songs draw already, with its options by stable
///     names, in the order a link writes them; a change to them takes a new settings format. The page shows them by their
///     names' families: the instruments ("instrument."), the drums' roles ("drumRole."), and every part's rhythm and feel
///     ("rhythm.", "feel.") as tabs of a part each.
/// </summary>
public static class GenreChoices
{

    /// <summary>
    ///     A tempo, as a multiple of <see cref="Composition.Meter.BaseTempo" />, by its amount from 0, 40 beats a minute, a
    ///     dirge's, to 1, 240, every like step a like ratio, as a tempo given is (the page's tempo's steps).
    /// </summary>
    public static double TempoOf(double amount) => Math.Pow(SlowestTempos, amount) * SlowestTempo;

    /// <summary>A tempo's amount, as a multiple of <see cref="Composition.Meter.BaseTempo" /> (<see cref="TempoOf" />).</summary>
    public static double AmountOfTempo(double tempo) => Math.Log(tempo / SlowestTempo, SlowestTempos);

    /// <summary>The slowest tempo a genre ranges, 40 beats a minute, as a multiple of the base tempo, and how many times it the fastest is.</summary>
    private const double SlowestTempo = 1 / 3.0, SlowestTempos = 6;

    /// <summary>The shortest and the longest a song's length is given or ranged, in seconds: half a minute and fifteen.</summary>
    public const double ShortestLength = 30;

    public const double LongestLength = 900;

    /// <summary>A length, in seconds, by its amount from 0, the shortest, to 1, the longest, every like step a like ratio.</summary>
    public static double LengthOf(double amount) => ShortestLength * Math.Pow(LongestLength / ShortestLength, amount);

    /// <summary>A length's amount, of its seconds (<see cref="LengthOf" />).</summary>
    public static double AmountOfLength(double seconds) => Math.Log(seconds / ShortestLength) / Math.Log(LongestLength / ShortestLength);

    /// <summary>
    ///     The song's tempo, a range of them from 60 to 240 beats a minute, as wide as a tempo given may be, starting from the
    ///     tempos the songs draw, from 90 to about 175, and how a song's leans within it, to the slow end or the fast.
    /// </summary>
    public static GenreChoice Tempo { get; } = new("tempo", "Tempo", GenreChoiceKind.Range,
    [
        new("from", "From", Even(AmountOfTempo(SongGenerator.TempoOptions[0]))),
        new("to", "To", Even(AmountOfTempo(SongGenerator.TempoOptions[^1]))),
        new("lean", "Lean", Even(0.5))
    ])
    {
        Scale = new RangeScale("bpm", 40, 240, x => TempoOf(x) * Composition.Meter.BaseTempo, x => AmountOfTempo(x / Composition.Meter.BaseTempo))
    };

    /// <summary>
    ///     How long the song plays, a range of lengths from half a minute to fifteen, which its form comes as near as it can
    ///     to (<see cref="SongLength" />), starting from how long the songs play as their forms make them, the shortest
    ///     tenth to the longest at each end (<c>SongLengthReportTest</c>), and how a song's leans within it.
    /// </summary>
    public static GenreChoice Length { get; } = new("length", "Length", GenreChoiceKind.Range,
    [
        new("from", "From", new ByConvention(AmountOfLength(93), AmountOfLength(104), AmountOfLength(152))),
        new("to", "To", new ByConvention(AmountOfLength(208), AmountOfLength(266), AmountOfLength(509))),
        new("lean", "Lean", Even(0.5))
    ])
    {
        Scale = new RangeScale("seconds", ShortestLength, LongestLength, LengthOf, AmountOfLength)
    };

    /// <summary>
    ///     How dark the genre's songs are, a range of their moods (<see cref="Composition.Mood" />) from -100, the brightest,
    ///     to 100, the darkest, the songs' own -60 to 60, and how a song's leans within it.
    /// </summary>
    public static GenreChoice Darkness { get; } = MoodRange("darkness", "Darkness");

    /// <summary>How intense the genre's songs are, a range of their moods from -100, the calmest, to 100, the fiercest, as <see cref="Darkness" /> is.</summary>
    public static GenreChoice Intensity { get; } = MoodRange("intensity", "Intensity");

    private static GenreChoice MoodRange(string name, string label) => new(name, label, GenreChoiceKind.Range,
    [
        new("from", "From", Even(Composition.Mood.SongsSpan.From)),
        new("to", "To", Even(Composition.Mood.SongsSpan.To)),
        new("lean", "Lean", Even(0.5))
    ])
    {
        Scale = new RangeScale("%", -100, 100, x => 100 * Composition.Mood.OfAmount(x), x => Composition.Mood.AmountOf(x / 100))
    };

    /// <summary>The meter the song's bars are in (<see cref="Composition.Meter.Options" />).</summary>
    public static GenreChoice Meter { get; } = Pool("meter", "Meter",
        Composition.Meter.Options.Select(x => (Composition.Meter.NameOf(x.Meter), Composition.Meter.NameOf(x.Meter), x.Weight)));

    /// <summary>
    ///     How a section's chord roots move, each motion leaned from as the songs have it, falling a fifth the most, to more
    ///     or less (<see cref="Progressions.Motions" />): a genre's ii–V or its ♭VII–IV–I.
    /// </summary>
    public static GenreChoice RootMotions { get; } = Values("rootMotion", "Root motions", Progressions.Motions, Facet.Progression);

    /// <summary>The degrees a chord's root leans to, by its steps above the section's home, as rock's ♭VII and ♭VI (<see cref="Progressions.Degrees" />).</summary>
    public static GenreChoice RootDegrees { get; } = Values("rootDegree", "Chord degrees", Progressions.Degrees, Facet.Progression);

    /// <summary>How a genre leans the chord that leads home, by its degree, a modal band's ♭II, ♭VI or ♭VII rather than V (<see cref="Progressions.Generate(IGenerationContext, Scale, int, double, int, int, Func{int, double}, Func{int, double}, Func{int, double})" />).</summary>
    public static GenreChoice CadenceDegrees { get; } = Values("cadenceDegree", "The chord into home", Progressions.Degrees, Facet.Progression);

    /// <summary>How closely a progression keeps to its rules, from 0, freer, through 50, as the songs keep them, to 100, every one, a blues' twelve bars (<see cref="Progressions.StrictnessOf" />).</summary>
    public static GenreChoice Strictness { get; } = Values("strictness", "Progression's strictness", [("rules", "Keeping to its rules")], Facet.Progression);

    /// <summary>The chance a section's cadence raises its seventh, a minor key's major V leading home (<see cref="Progressions.CadenceRaise" />).</summary>
    public static GenreChoice CadenceRaise { get; } = Chances("cadenceRaise", "The leading tone raised", [("sections", "Sections whose cadence raises it", Progressions.CadenceRaise)]);

    /// <summary>The homes a section's chords move around, the tonic, the relative key, the fourth or the fifth (<see cref="Progressions.Homes" />).</summary>
    public static GenreChoice SectionHome { get; } = Pool("sectionHome", "A section's home", Progressions.Homes.Select(x => (x.Name, x.Label, new ByConvention(x.Weight, x.Weight, x.Weight))));

    /// <summary>
    ///     The chords that leave the section's scale, each its chance as the songs have it (<see cref="Composition.ChordScales" />):
    ///     a chord before a root a fifth below its dominant, a chord in a mode borrowed from the home's parallels, a
    ///     section's every chord but its home its own dominant, the blues', which the songs never play, a chord in a scale of
    ///     its own, and a chromatic mediant, iii or vi turned major.
    /// </summary>
    public static GenreChoice ChordScales { get; } = Chances("chordScales", "Chords off the scale",
    [
        ("applied", "Applied dominants", Composition.ChordScales.Applied),
        ("borrowed", "Borrowed chords", Composition.ChordScales.Borrowed),
        ("dominant", "Every chord its dominant", Composition.ChordScales.Dominant),
        ("own", "A chord's own scale", Composition.ChordScales.Own),
        ("mediant", "Chromatic mediants, iii or vi turned major", Composition.ChordScales.Mediant),
        ("minorMediant", "Minor mediants, ♭III or ♭VI turned minor", Composition.ChordScales.MinorMediant)
    ]);

    /// <summary>The parts a trance gate chops, held chords' and, a genre's, the riff (<see cref="Expression.GateParts" />).</summary>
    public static GenreChoice GateParts { get; } = Pool("gateParts", "Parts the gate chops",
        Expression.GateParts.Select(x => (Name(x.Role.ToString()), x.Role switch
        {
            TrackRole.Pad => "The pad",
            TrackRole.Chords => "The chords",
            TrackRole.Rhythm => "The rhythm part",
            TrackRole.Riff => "The riff, a stutter",
            _ => throw new ArgumentOutOfRangeException(nameof(x.Role), x.Role, "No label for the gate's part.")
        }, x.Weight)));

    /// <summary>The chance a drum bar adds an edit to a mutating groove, an IDM or a drum and bass track's (<see cref="Composition.Mutations" />).</summary>
    public static GenreChoice Mutation { get; } = Chances("mutation", "A groove that mutates", [("bars", "Bars adding an edit", Composition.Mutations.Chance)]);

    /// <summary>What a drum solo's feet keep time with: the groove's kick, the hi-hat's pedal on 2 and 4, a double kick, a bossa's kick (<see cref="DrumSolos.Feet" />).</summary>
    public static GenreChoice DrumSoloFeet { get; } = Pool("drumSoloFeet", "A drum solo's feet",
        DrumSolos.Feet.Select(x => (Name(x.Feet.ToString()), x.Feet switch
        {
            SoloFeet.Groove => "The groove's kick",
            SoloFeet.HatFoot => "The hi-hat's pedal on 2 and 4",
            SoloFeet.DoubleKick => "A double kick in 16ths",
            SoloFeet.Bossa => "The bossa's kick",
            _ => throw new ArgumentOutOfRangeException(nameof(x.Feet), x.Feet, "No label for the feet.")
        }, x.Weight)));

    /// <summary>The stickings a drum solo's runs play (<see cref="DrumSolos.Stickings" />).</summary>
    public static GenreChoice Stickings { get; } = Pool("sticking", "A drum solo's stickings",
        DrumSolos.Stickings.Select(x => (Name(x.Sticking.ToString()), x.Sticking switch
        {
            Sticking.Singles => "Single strokes",
            Sticking.Doubles => "Double strokes",
            Sticking.Paradiddle => "Paradiddles",
            Sticking.SixStroke => "Six-stroke rolls",
            Sticking.HandFoot => "Hands and foot in triplets",
            Sticking.Cascade => "Tom cascades",
            _ => throw new ArgumentOutOfRangeException(nameof(x.Sticking), x.Sticking, "No label for the sticking.")
        }, x.Weight)));

    /// <summary>The chances the whole mix is filtered: opening up a song, dipping before a section, closing as it ends (<see cref="Composition.MixFilter" />).</summary>
    public static GenreChoice MixFilter { get; } = Chances("mixFilter", "The mix filtered",
    [
        ("opening", "Opening up a song", Composition.MixFilter.OpeningChance),
        ("dips", "Dipping before a section", Composition.MixFilter.DipChance),
        ("closing", "Closing as it ends", Composition.MixFilter.ClosingChance)
    ]);

    /// <summary>The chance a section plays the song's first section's chords, the arrangement telling them apart, as a beat's loop does; the songs never.</summary>
    public static GenreChoice Loop { get; } = Chances("loop", "Loop", [("sections", "Sections on the first's chords", SectionGenerator.Loop)]);

    /// <summary>The chance a later appearance is a solo, as the songs have it by its role (<see cref="Composition.Solos.Chance" />).</summary>
    public static GenreChoice Solos { get; } = Chances("solos", "Solos",
        [("sections", "A section soloed", Composition.Solos.Chance), ("trades", "A solo trading with the drums", Composition.Solos.Trade),
            ("twins", "A solo harmonised by a second part", Composition.Solos.Twin)]);

    /// <summary>Who solos (<see cref="Composition.Solos.Soloists" />).</summary>
    public static GenreChoice Soloists { get; } = Pool("soloists", "Soloists",
        Composition.Solos.Soloists.Select(x => (Name(x.Part.ToString()), Composition.Solos.LabelOf(x.Part), x.Weight)));

    /// <summary>How the song starts before its first section (<see cref="FormLayers.Intros" />): the whole band, a count-in, or the parts one by one.</summary>
    public static GenreChoice Intro { get; } = Pool("intro", "Intro",
        FormLayers.IntroWeights.Select(x => (Name(x.Intro.ToString()), FormLayers.LabelOf(x.Intro), x.Weight)));

    /// <summary>The chance a song opens on one part alone (<see cref="FormLayers.OpeningAlone" />).</summary>
    public static GenreChoice OpeningAlone { get; } = Chances("openingAlone", "A part opening the song", [("songs", "Songs opening on one part alone", FormLayers.OpeningAlone)]);

    /// <summary>The part a song opening on one alone opens on (<see cref="FormLayers.OpeningParts" />): its riff, or a genre's drums, bass, chords or pad.</summary>
    public static GenreChoice OpeningPart { get; } = Pool("openingPart", "The part opening the song",
        FormLayers.OpeningParts.Select(x => (Name(x.Part.ToString()), TrackRoles.LabelOf(x.Part), x.Weight)));

    /// <summary>How the song ends (<see cref="FormLayers.Endings" />).</summary>
    public static GenreChoice Ending { get; } = Pool("ending", "Ending",
        FormLayers.EndingWeights.Select(x => (Name(x.Ending.ToString()), FormLayers.LabelOf(x.Ending), x.Weight)));

    /// <summary>The chance a section sits on its home chord, every chord of its pattern the home, a vamp (<see cref="SectionGenerator.Vamp" />); the songs never.</summary>
    public static GenreChoice Vamp { get; } = Chances("vamp", "Vamp", [("sections", "Sections on one chord", SectionGenerator.Vamp)]);

    /// <summary>
    ///     The chances of a section's riff: in power chords, a perfect fifth over every note, as rock's riffs are, in
    ///     palm-muted chugs, with its weak notes on a drone (<see cref="SectionGenerator.PowerChordChance" />), and a figure,
    ///     its first bar's notes kept over the chords that follow (<see cref="SectionGenerator.RiffHookChance" />).
    /// </summary>
    public static GenreChoice Riff { get; } = Chances("riff", "Riff",
    [
        ("powerChords", "In power chords", SectionGenerator.PowerChordChance),
        ("chugs", "Palm-muted chugs", SectionGenerator.ChugChance),
        ("drone", "Its weak notes on a drone", SectionGenerator.DroneChance),
        ("hook", "A figure kept over the chords", SectionGenerator.RiffHookChance)
    ]);

    /// <summary>The chance a section's pattern turns around in its last bar, a V or a ii–V into its start, a reharmonisation, which the songs never play at the plain end and a genre may (<see cref="Progressions.Turnaround" />).</summary>
    public static GenreChoice Turnaround { get; } = Chances("turnaround", "Turnaround", [("sections", "Sections turned around", Progressions.Turnaround)]);

    /// <summary>The chance a chord a bar long passes into the next by its dominant, a reharmonisation, which the songs never play at the plain end and a genre may (<see cref="Composition.PassingChords" />).</summary>
    public static GenreChoice PassingChords { get; } = Chances("passingChords", "Passing chords", [("chords", "Chords passing into the next", Composition.PassingChords.Chance)]);

    /// <summary>How a passing chord leads into the next (<see cref="Composition.PassingChords.Kinds" />).</summary>
    public static GenreChoice PassingKinds { get; } = Pool("passingKind", "Passing chords' kinds",
        Composition.PassingChords.Kinds.Select(x => (Name(x.Kind.ToString()), Composition.PassingChords.LabelOf(x.Kind), x.Weight)));

    /// <summary>The chance a bridge or a section of a free form is a heavy band's breakdown (<see cref="Breakdowns.Chance" />).</summary>
    public static GenreChoice Breakdown { get; } = Chances("breakdown", "Breakdowns", [("sections", "Bridges broken down", Breakdowns.Chance)]);

    /// <summary>The chance a section plays its first phrase in stop time, the band hitting once a bar under the lines (<see cref="StopTime.Chance" />).</summary>
    public static GenreChoice StopTime { get; } = Chances("stopTime", "Stop time", [("sections", "Sections in stop time", Composition.StopTime.Chance)]);

    /// <summary>The chance a section strikes its last bar before a louder one in ensemble hits, the band on the kick's notes (<see cref="StopTime.HitsChance" />).</summary>
    public static GenreChoice EnsembleHits { get; } = Chances("ensembleHits", "Ensemble hits", [("sections", "Sections hitting into a louder one", Composition.StopTime.HitsChance)]);

    /// <summary>The chance a section planes, every chord the home's shape moved by semitones, a reharmonisation, which the songs never play at the plain end and a genre may (<see cref="Composition.Planing" />).</summary>
    public static GenreChoice Planing { get; } = Chances("planing", "Planing",
        [("sections", "Sections planed", Composition.Planing.Chance), ("modes", "Planed chords each in a mode of its own", Composition.Planing.ModesChance)]);

    /// <summary>The chances a song's last section that came back lifts its key, a section plays in a key of its own, and any pattern's start changes it (<see cref="KeyChange" />): a ballad's last chorus up, a blues in one key.</summary>
    public static GenreChoice KeyChanges { get; } = Chances("keyChanges", "Key changes",
        [
            ("lastSection", "A last section lifted", KeyChange.LastSectionChance), ("sections", "A section in a key of its own", KeyChange.SectionChance),
            ("patterns", "Any pattern's start", KeyChange.ElsewhereChance)
        ]);

    /// <summary>The steps a key is lifted by, in semitones (<see cref="KeyChange.Steps" />): a tone or a half at the plain end, a third a symphonic band's.</summary>
    public static GenreChoice KeyLift { get; } = Pool("keyLift", "A lift's step", KeyChange.Steps.Select(x => (KeyChange.NameOf(x.Step), $"{x.Step} semitones", x.Weight)));

    /// <summary>The steps a section's own key is from the song's, in semitones (<see cref="KeyChange.SectionSteps" />): a fourth or a fifth the most often.</summary>
    public static GenreChoice SectionKey { get; } = Pool("sectionKey", "A section's own key", KeyChange.SectionSteps.Select(x => (KeyChange.NameOf(x.Step), $"{x.Step} semitones", x.Weight)));

    /// <summary>The chance a section pushes its chord changes, the band an eighth early (<see cref="Pushes.Chance" />).</summary>
    public static GenreChoice Push { get; } = Chances("push", "Pushes", [("sections", "Sections pushing their changes", Pushes.Chance)]);

    /// <summary>
    ///     The chances of harmony's devices that no choice above reaches: a later appearance reharmonised, a chromatic line
    ///     inside a held chord and a pedal under a section; reharmonisations, which the songs never play at the plain end and a genre may.
    /// </summary>
    public static GenreChoice HarmonyDevices { get; } = Chances("harmonyDevices", "Harmony's devices",
    [
        ("reharmonised", "A later appearance reharmonised", Reharmonisation.Chance),
        ("innerLine", "A chromatic inner line", InnerLines.Chance),
        ("pedal", "A pedal under a section", SectionPedals.Chance)
    ]);

    /// <summary>Where a section holds its pedal (<see cref="SectionPedals.Places" />): its home under its first phrase, its dominant under its last, or a drone under the whole.</summary>
    public static GenreChoice SectionPedal { get; } = Pool("sectionPedal", "Section's pedal", SectionPedals.Places.Select(x => (Name(x.Place.ToString()), SectionPedals.LabelOf(x.Place), x.Weight)));

    /// <summary>The chances a song's lines play each ornament, a grace note, a turn and a trill (<see cref="Composition.Ornaments" />).</summary>
    public static GenreChoice Ornaments { get; } = Chances("ornaments", "Ornaments",
        [..Composition.Ornaments.All.Select(x => (Composition.Ornaments.NameOf(x.Ornament), Composition.Ornaments.LabelOf(x.Ornament), x.Chance))]);

    /// <summary>The chance a line's note on a strong beat leans on a tension the next resolves, the melody's and the counter-melody's (<see cref="LinePattern.Appoggiaturas" />).</summary>
    public static GenreChoice Appoggiatura { get; } = Chances("appoggiatura", "Strong beats leaning",
        [..LinePattern.Appoggiaturas.Select(x => (Name(x.Role.ToString()), $"The {TrackRoles.LabelOf(x.Role).ToLowerInvariant()}'s", x.Chance))]);

    /// <summary>The chance a line's note on a strong beat lands on a tension, the melody's and the counter-melody's (<see cref="LinePattern.Tensions" />).</summary>
    public static GenreChoice Tension { get; } = Chances("tension", "Strong beats on a tension",
        [..LinePattern.Tensions.Select(x => (Name(x.Role.ToString()), $"The {TrackRoles.LabelOf(x.Role).ToLowerInvariant()}'s", x.Chance))]);

    /// <summary>The chances a line's part slurs its steps, a piano's part rolls its held chords, and the chords are struck short (<see cref="Articulations.Apply" />).</summary>
    public static GenreChoice Articulation { get; } = Chances("articulation", "Articulation",
        [("legato", "Slurred steps", Articulations.LegatoChance), ("roll", "Rolled piano chords", Articulations.RollChance), ("stabs", "Chords as stabs", Articulations.StabChance),
            ("letRing", "Broken chords let ring", Articulations.LetRingChance), ("frames", "Chords cycled at a chip's frame rate", Articulations.FrameChance)]);

    /// <summary>The chance a song pins a part to its rhythm, every bar alike: each pitched part, and each drum role but the colour (<see cref="DrumRoles.Pins" />).</summary>
    public static GenreChoice Pins { get; } = Chances("pins", "Parts pinned", [..Composition.DrumRoles.PinnedParts.Select(x => (Name(x.ToString()), Composition.DrumRoles.PinLabelOf(x), Composition.DrumRoles.Pins))]);

    /// <summary>How long a section plays, its pattern played over and over: four bars to thirty-two, a dance track's the longest, which the songs never play (<see cref="Composition.SectionLength" />).</summary>
    public static GenreChoice SectionLength { get; } = Pool("sectionLength", "Section's length",
        Composition.SectionLength.Options.Select(x => (Composition.SectionLength.NameOf(x.Plays), $"{x.Plays * Composition.Meter.DefaultPhraseBars} bars", x.Weight)));

    /// <summary>How an appearance brings its parts in over its phrases: steady, building, thinning or one part alone (<see cref="Composition.Texture" />).</summary>
    public static GenreChoice Texture { get; } = Pool("texture", "Texture", Composition.Texture.Kinds.Select(x => (Name(x.Kind.ToString()), Composition.Texture.LabelOf(x.Kind), x.Weight)));

    /// <summary>Which part plays a section alone, where its texture leaves one, each as likely in the songs (<see cref="Composition.Texture" />).</summary>
    public static GenreChoice Alone { get; } = Pool("alone", "The part alone",
        PartsOf(TrackRole.Melody, TrackRole.Chords, TrackRole.Bass, TrackRole.Pad, TrackRole.CounterMelody, TrackRole.Drum, TrackRole.Riff, TrackRole.Rhythm)
            .Select(x => (x.Name, x.Label, Even(1))));

    /// <summary>How the chords and the rhythm part break their chords, or play them whole (<see cref="Arpeggios" />): an arpeggiator's up and down a synth genre's.</summary>
    public static GenreChoice Arpeggio { get; } = Pool("arpeggio", "Arpeggios", Arpeggios.Patterns.Select(x => (Name(x.Pattern.ToString()), Arpeggios.LabelOf(x.Pattern), x.Weight)));

    /// <summary>The tone a part breaking its chord lays on it, its own, the band's chord as it was (<see cref="Arpeggios.Colours" />).</summary>
    public static GenreChoice Colour { get; } = Pool("colour", "A broken chord's colour", Arpeggios.Colours.Select(x => (Name(x.Colour.ToString()), Arpeggios.LabelOf(x.Colour), x.Weight)));

    /// <summary>The chance a section's snare plays ghost notes around its backbeat, a funk drummer's (<see cref="Composition.Ghosts.Chance" />).</summary>
    public static GenreChoice Ghosts { get; } = Chances("ghosts", "Ghost notes", [("sections", "Sections ghosting on the snare", Composition.Ghosts.Chance)]);

    /// <summary>
    ///     How many 16ths a ghosting section's snare ghosts, from 0, few, through 50, as it does, to 100, many
    ///     (<see cref="Composition.Ghosts.GenreOdds" />).
    /// </summary>
    public static GenreChoice GhostNotes { get; } = Values("ghostNotes", "Ghost notes a bar", [("many", "How many")], Facet.Groove);

    /// <summary>The chances a song's snare flams its beats in a fill and drags into its backbeat (<see cref="Composition.DrumOrnaments" />).</summary>
    public static GenreChoice Rudiments { get; } = Chances("rudiments", "Rudiments",
        [("flams", "Flams on the snare's beats in a fill", Composition.DrumOrnaments.FlamChance), ("drags", "Drags into the snare's backbeat", Composition.DrumOrnaments.DragChance)]);

    /// <summary>The chance a bar's hi-hat rolls its last beat in 32nds or 16th triplets, the trap's (<see cref="HatRolls.Chance" />).</summary>
    public static GenreChoice HatRolls { get; } = Chances("hatRolls", "Hi-hat rolls", [("bars", "Bars ending on a roll", Composition.HatRolls.Chance)]);

    /// <summary>
    ///     How high each pitched part sits, from 0, an octave lower, as a guitar tuned down, through 50, as the songs have it,
    ///     to 100, an octave higher, a quarter's way from either end moving it (<see cref="Registers" />).
    /// </summary>
    public static GenreChoice Register { get; } = Values("register", "Register",
        PartsOf(TrackRole.Melody, TrackRole.Chords, TrackRole.Bass, TrackRole.Pad, TrackRole.CounterMelody, TrackRole.Riff, TrackRole.Rhythm, TrackRole.Solo), Facet.Sound);

    /// <summary>
    ///     How often each of a drum's own sounds plays as an accent and in a fill, from 0, an eighth as often, through 50, as
    ///     the songs have it, to 100, eight times as often, one a wild song's alone played at the plain end where a genre leans
    ///     it up: a metal china, a disco's open hi-hat off the beat (<see cref="DrumSounds.OddsOf" />).
    /// </summary>
    public static GenreChoice DrumSounds { get; } = Values("drumSounds", "Drum sounds", Composition.DrumSounds.Leaned.Select(x => (x.Name, x.Label)), Facet.Sound);

    /// <summary>
    ///     How often each of a drum's own sounds is its steady stroke, as the song, a section or a bar draws it, from 0, an
    ///     eighth as often, through 50, as the songs have it, to 100, eight times as often: the cross-stick of a reggae or a
    ///     ballad, a jazz drummer's hi-hat foot, a loud rock chorus's open hi-hat (<see cref="DrumSounds.OddsOf" />).
    /// </summary>
    public static GenreChoice DrumStrokes { get; } = Values("drumStrokes", "Drum strokes", Composition.DrumSounds.Leaned.Select(x => (x.Name, x.Label)), Facet.Sound);

    /// <summary>
    ///     The chances a louder section turns heavy, its clean guitars driven, as a band kicks in the distortion for the
    ///     chorus, and a quieter one calm, its driven guitars acoustic or clean, breaking the chords (<see cref="SectionForm" />).
    /// </summary>
    public static GenreChoice LouderForms { get; } = Chances("louderForms", "Louder forms",
        [("guitars", "A louder section heavy, its guitars driven", SectionForm.HeavyChance), ("quieter", "A quieter section calm, its guitars acoustic or clean", SectionForm.CalmChance)]);

    /// <summary>
    ///     The interval a line harmonised plays apart from it, the melody's by the counter-melody and the riff's by its twin,
    ///     with its inversion: thirds and sixths, the songs', fourths and fifths, an organum's or a riff's in parallel, and
    ///     seconds and sevenths, the wildest twins' (<see cref="LineDoubling.IntervalOf" />); the songs' amounts across the
    ///     pairs, which a genre's lean each pair's own steps by (<see cref="Genre.Lean{T}" />).
    /// </summary>
    public static GenreChoice HarmonyInterval { get; } = Pool("harmonyInterval", "Harmony's interval",
    [
        ("thirds", "Thirds and sixths", new ByConvention(1, 1, 1)), ("fourths", "Fourths and fifths", new ByConvention(0, 0.05, 0.4)),
        ("seconds", "Seconds and sevenths", new ByConvention(0, 0, 0.2))
    ]);

    /// <summary>What the drums do: keep time, as the songs' always do, or mark the form, as a calm piece's percussion does (<see cref="DrumMarks" />).</summary>
    public static GenreChoice DrumJob { get; } = Pool("drumJob", "Drums' job", DrumMarks.Jobs.Select(x => (DrumMarks.NameOf(x.Job), x.Job == Composition.DrumJob.Groove ? "Keeping time" : "Marking the form", x.Weight)));

    /// <summary>
    ///     How often drums marking the form strike, and how often a section's start swells, from 0, seldom, through 50, as
    ///     they do, to 100, often (<see cref="DrumMarks.GenreOdds" />).
    /// </summary>
    public static GenreChoice Marks { get; } = Values("marks", "Drums' marks", [("often", "How often"), ("swells", "How often swelling")], Facet.Fills);

    /// <summary>The chance a verse leaves its melody out, the room a rap's voice takes over the beat (<see cref="Arrangement.VoiceChance" />).</summary>
    public static GenreChoice Voice { get; } = Chances("voice", "Verses for a voice", [("verses", "Verses without a melody", Arrangement.VoiceChance)]);

    /// <summary>Which bars of a pattern repeat which, AAAA to ABCD: a loop's one bar over and over (<see cref="PhraseSchemes" />).</summary>
    public static GenreChoice PhraseScheme { get; } = Pool("phraseScheme", "Bars repeating", PhraseSchemes.Weights.Select(x => (x.Scheme.ToLowerInvariant(), x.Scheme, x.Weight)));

    /// <summary>The chance a lift the drums announce is built to and dropped at, the band out for the bar before it; the songs never (<see cref="FormLayers.Drop" />).</summary>
    public static GenreChoice Drop { get; } = Chances("drop", "Builds and drops", [("lifts", "Lifts built to a drop", FormLayers.Drop)]);

    /// <summary>How a drop takes the bar before it, the band silent or filtered (<see cref="FormLayers.DropKinds" />).</summary>
    public static GenreChoice DropKind { get; } = Pool("dropKind", "A drop's bar before it",
        FormLayers.DropKinds.Select(x => (Name(x.Kind.ToString()), x.Kind == Composition.DropKind.Silent ? "The band silent" : "The band filtered", x.Weight)));

    /// <summary>
    ///     The chance a section's melody is on a hook, a chorus's and another section's, as a beat's sample is; the songs' a
    ///     chorus's alone (<see cref="MelodyLayers.HookChanceOf" />).
    /// </summary>
    public static GenreChoice Hook { get; } = Chances("hook", "Hook",
        [("choruses", "Choruses on a hook", MelodyLayers.HookChanceOf(SectionRole.Chorus)), ("sections", "Other sections on a hook", MelodyLayers.HookChanceOf(SectionRole.Verse))]);

    /// <summary>
    ///     The chance a section's melody is sung in lines, resting at half its phrase as at its end; the songs' every
    ///     section's at the plain end and none at the wild (<see cref="MelodyLayers.HalfRests" />).
    /// </summary>
    public static GenreChoice Sung { get; } = Chances("sung", "Sung lines",
        [("halves", "Resting at half a phrase", MelodyLayers.HalfRests), ("eighths", "In 8ths, no note shorter", MelodyLayers.SungEighths)]);

    /// <summary>How long a section's chords last (<see cref="Composition.HarmonicRhythm.Spans" />): half a bar, a bar or two bars, the faster the more energy.</summary>
    public static GenreChoice HarmonicRhythm { get; } = Pool("harmonicRhythm", "Chords' pace",
        Composition.HarmonicRhythm.Spans.Select(x => (Composition.HarmonicRhythm.NameOf(x.Span.Value), Composition.HarmonicRhythm.LabelOf(x.Span.Value),
            new ByConvention(x.Span.Weight, x.Span.Weight, x.Span.Weight))));

    /// <summary>How much the drums fill, from 0, seldom and short, through 50, as the songs have it, to 100, often and long (<see cref="FillLayers.GenreAmountOdds" />).</summary>
    public static GenreChoice Fills { get; } = Values("fills", "Fills", [("amount", "Amount")], Facet.Fills);

    /// <summary>The chance a fill's line takes the kick out instead, the groove playing on without it into the downbeat (<see cref="FillLayers.KickOutChance" />).</summary>
    public static GenreChoice KickOut { get; } = Chances("kickOut", "Kick-out", [("lines", "Lines the kick drops out for", FillLayers.KickOutChance)]);

    /// <summary>How far the parts play off the beat, from 0, tight, through 50, as the songs have it, to 100, laid back (<see cref="Pocket.GenreReach" />).</summary>
    public static GenreChoice Pocket { get; } = Values("pocket", "Pocket", [("laidBack", "Laid back")], Facet.Feel);

    /// <summary>How steady the groove is, from 0, moving more, through 50, as the songs move it, to 100, held, its backbeat on 2 and 4 (<see cref="RhythmicUnconventionality.GrooveHold" />).</summary>
    public static GenreChoice GrooveHold { get; } = Values("grooveHold", "Groove held", [("held", "Held")], Facet.Groove);

    /// <summary>
    ///     How loud each part plays against the others, from 0, quietest, through 50, as the songs have it, to 100, loudest
    ///     (<see cref="VelocityLayers.GenreLevel" />): a beat's drums leading it, a dub's bass; the mix's volumes over them.
    /// </summary>
    public static GenreChoice Levels { get; } = Values("levels", "Levels",
        PartsOf(TrackRole.Melody, TrackRole.Chords, TrackRole.Bass, TrackRole.Pad, TrackRole.CounterMelody, TrackRole.Riff, TrackRole.Rhythm, TrackRole.Drum), Facet.Sound);

    /// <summary>
    ///     How much reverb each part plays with, from 0, dry, through 50, as the songs have it, to 100, as wet as can be
    ///     (<see cref="Expression.Reverb(TrackRole, Genre)" />): an ambient wash, a funk band's dry room.
    /// </summary>
    public static GenreChoice Space { get; } = Values("space", "Space",
        PartsOf(TrackRole.Melody, TrackRole.Chords, TrackRole.Bass, TrackRole.Pad, TrackRole.CounterMelody, TrackRole.Riff, TrackRole.Rhythm, TrackRole.Solo, TrackRole.Drum), Facet.Sound);

    /// <summary>How much the bass leads into a new chord, from 0, never, through 50, as its instrument has it, to 100, walking (<see cref="BassLeadingLayers.GenreLeadOdds" />).</summary>
    public static GenreChoice Bass { get; } = Values("bass", "Bass", [("leading", "Leading into a chord")], Facet.Melody);

    /// <summary>The chance a section's bass plays in octaves, its notes off the beat the note before an octave up (<see cref="SectionGenerator.OctaveBassChance" />).</summary>
    public static GenreChoice OctaveBass { get; } = Chances("octaveBass", "Octave bass", [("sections", "Sections in octaves", SectionGenerator.OctaveBassChance)]);

    /// <summary>
    ///     The chance a section's bass alternates the root and the fifth on its strong beats, and walks a boogie
    ///     (<see cref="SectionGenerator.AlternatingBassChance" />, <see cref="SectionGenerator.BoogieChance" />).
    /// </summary>
    public static GenreChoice AlternatingBass { get; } = Chances("alternatingBass", "The bass's cycle",
        [("sections", "Sections alternating root and fifth", SectionGenerator.AlternatingBassChance), ("boogie", "Sections walking a boogie", SectionGenerator.BoogieChance)]);

    /// <summary>How wide the melody's range is, from 0, narrow as a hook's, through 50, as the songs have it, to 100, wide as a jazz tune's (<see cref="MelodyLayers.GenreRangeReach" />).</summary>
    public static GenreChoice MelodyRange { get; } = Values("melodyRange", "Melody's range", [("wide", "Wide")], Facet.Melody);

    /// <summary>The chance a song's bass glides into its notes from the note before, an 808's or an acid line's (<see cref="Expression.GlideChance" />).</summary>
    public static GenreChoice Glide { get; } = Chances("glide", "Glide", [("bass", "A gliding bass", Expression.GlideChance), ("lead", "A gliding lead", Expression.GlideChance)]);

    /// <summary>How long a role's section leans to, from 0, a quarter of the songs', through 50, theirs, to 100, four times (<see cref="Composition.SectionLength.LengthRoles" />).</summary>
    public static GenreChoice RoleLength { get; } = Values("roleLength", "A role's length",
        [..Composition.SectionLength.LengthRoles.Select(x => (Name(x.ToString()), $"The {x.ToString().ToLowerInvariant()}'s"))], Facet.Form);

    /// <summary>How many of its notes a gliding part slides into, from 0, none, through 50, half, as the songs' do, to 100, every one it may (<see cref="Expression.GlideRate" />).</summary>
    public static GenreChoice GlideRate { get; } = Values("glideRate", "Glide's share", [("share", "Notes slid into")], Facet.Sound);

    /// <summary>What the bass lands on in a new chord (<see cref="BassLeadingLayers.Arrivals" />): its root, its third, its fifth, or its figure's note.</summary>
    public static GenreChoice BassLanding { get; } = Pool("bassLanding", "Bass's landing",
        BassLeadingLayers.ArrivalWeights.Select(x => (Name(x.Arrival.ToString()), BassLeadingLayers.LabelOf(x.Arrival), x.Weight)));

    /// <summary>
    ///     The scales a melody's and a riff's passing notes take, each weighed as the songs weigh them
    ///     (<see cref="Composition.LineScales.Weights" />): the section's, its pentatonic, the blues', and the wilder ones on each
    ///     chord's root.
    /// </summary>
    public static GenreChoice LineScale { get; } = Pool("lineScale", "Lines' scale",
        Composition.LineScales.Weights.Select(x => (Name(x.Scale.ToString()), Composition.LineScales.LabelOf(x.Scale), x.Weight)));

    /// <summary>The mode a chord takes on its own root, a colour of its own or a planed chord's quality (<see cref="Composition.ChordScales.OwnModes" />).</summary>
    public static GenreChoice OwnMode { get; } = Pool("ownMode", "A chord's own mode",
        Composition.ChordScales.OwnModes.Select(x => (Name(x.Mode.Name), x.Mode.Name, x.Weight)));

    /// <summary>The scale a riff's passing notes take where a genre gives it one of its own, the lines' otherwise (<see cref="LineScale" />).</summary>
    public static GenreChoice RiffScale { get; } = Pool("riffScale", "Riff's scale",
        Composition.LineScales.Weights.Select(x => (Name(x.Scale.ToString()), Composition.LineScales.LabelOf(x.Scale), x.Weight)));

    /// <summary>
    ///     The gentle effects a song plays at all, each its chance as the songs have it by their sound (<see cref="Expression" />):
    ///     a vibrato, a chorus, a held chord's swell, a line's bends, a part's darker tone, a tape's wow, a guitar's pinch
    ///     harmonics and a riff's dive into a louder section.
    /// </summary>
    public static GenreChoice Effects { get; } = Chances("effects", "Effects",
    [
        ("vibrato", "A vibrato", Expression.SongVibratoChance),
        ("chorus", "A chorus", Expression.SongChorusChance),
        ("swell", "A swell", Expression.SongSwellChance),
        ("bends", "Bends", Expression.SongBendChance),
        ("tone", "A part's darker tone", Expression.SongToneChance),
        ("tape", "A tape's wow", Expression.TapeChance),
        ("pinch", "A guitar's pinch harmonics", Expression.SongPinchChance),
        ("dive", "A riff's dive into a louder section", Expression.SongDiveChance)
    ]);

    /// <summary>A section's own tempo, one by a metric ratio, its own meter beside the song's and its last bar cut short (<see cref="Composition.SectionTempo" />, <see cref="Composition.SectionMeter" />, <see cref="Composition.ShortBars" />).</summary>
    public static GenreChoice SectionChanges { get; } = Chances("sectionChanges", "A section's own time",
        [
            ("tempo", "A section at a tempo of its own", Composition.SectionTempo.Chance), ("modulation", "A tempo by a metric ratio", Composition.SectionTempo.ModulationChance),
            ("meter", "A section in a meter of its own", Composition.SectionMeter.Chance), ("shortBar", "A section's last bar cut short", Composition.ShortBars.Chance),
            ("eased", "A change of tempo eased into, not taken at once", Composition.SectionTempo.RampChance)
        ]);

    /// <summary>How many parts share a solo, trading its phrases or playing it together (<see cref="Composition.Solos.Counts" />).</summary>
    public static GenreChoice SoloCount { get; } = Pool("soloCount", "Soloists at once",
        Composition.Solos.Counts.Select(x => (Composition.Solos.CountName(x.Count), x.Count == 1 ? "One" : x.Count == 2 ? "Two, a duel" : "Three", x.Weight)));

    /// <summary>How several soloists share a solo: taking its phrases in turn, a duel or a jazz band's trades, or playing it together (<see cref="Composition.Solos.Modes" />).</summary>
    public static GenreChoice SoloShare { get; } = Pool("soloShare", "A shared solo",
        Composition.Solos.Modes.Select(x => (Name(x.Mode.ToString()), x.Mode == SoloMode.Trading ? "Trading phrases" : "Together", x.Weight)));

    /// <summary>A section's drums and bass in a cycle across the bar (<see cref="Composition.Polymeter" />).</summary>
    public static GenreChoice Polymeter { get; } = Chances("polymeter", "Polymeter", [("sections", "Sections in a polymeter", Composition.Polymeter.Chance)]);

    /// <summary>The chance a line runs into a section, an octave of the scale in 16ths, the counter-melody's (<see cref="LineFills.RunChance" />).</summary>
    public static GenreChoice LineRun { get; } = Chances("lineRun", "Runs into a section", [("counterMelody", "The counter-melody's", LineFills.RunChance)]);

    /// <summary>A polymeter's cells' lengths in 16ths (<see cref="Composition.Polymeter.Lengths" />): three against four and a dotted quarter the plainest, the odd ones wilder, 17 a genre's alone.</summary>
    public static GenreChoice PolymeterCell { get; } = Pool("polymeterCell", "Polymeter's cell",
        Composition.Polymeter.Lengths.Select(x => (Composition.Polymeter.NameOf(x.Sixteenths), $"{x.Sixteenths} 16ths", x.Weight)));

    /// <summary>The figures a player plays in place of its own rhythm, by the player, a genre's alone (<see cref="Composition.Figures" />).</summary>
    public static ImmutableDictionary<FigurePlayer, GenreChoice> Figures { get; } = Enum.GetValues<FigurePlayer>().ToImmutableDictionary(
        x => x,
        x => Pool($"figure.{Name(x.ToString())}", Composition.Figures.LabelOf(x), Composition.Figures.OptionsOf(x))
    );

    /// <summary>The solo part answering the melody in its gaps (<see cref="Composition.SoloAnswers" />).</summary>
    public static GenreChoice SoloAnswers { get; } = Chances("soloAnswers", "Solo answers", [("sections", "Sections the solo part answers", Composition.SoloAnswers.Chance)]);

    /// <summary>The chance a verse calls and its band answers, the melody singing each phrase's first half and the riff the second (<see cref="Composition.CallAndResponse" />).</summary>
    public static GenreChoice CallAndResponse { get; } = Chances("callAndResponse", "Call and response", [("sections", "Verses calling and answered", Composition.CallAndResponse.Chance)]);

    /// <summary>A last section that came back before played once more, as a last chorus (<see cref="SongForms.LastRepeatChance" />).</summary>
    public static GenreChoice LastRepeat { get; } = Chances("lastRepeat", "Last repeat",
        [("songs", "A last chorus played twice", SongForms.LastRepeatChance), ("breakdown", "Its first pass built up from the voice alone", SongForms.BreakdownChorusChance)]);

    /// <summary>A line doubled: in octaves by its own part, the melody harmonised by the counter-melody, in three voices now and then, the bass with the riff, the riff's twin leading a section (<see cref="LineDoubling" />).</summary>
    public static GenreChoice Doubling { get; } = Chances("doubling", "Doubling",
    [
        ("octaves", "Lines in octaves", LineDoubling.OctaveChance), ("harmony", "The melody harmonised", LineDoubling.HarmonyChance),
        ("riff", "The bass with the riff", LineDoubling.RiffChance),
        ("block", "The melody harmonised in three voices", LineDoubling.BlockChance),
        ("twin", "Sections the riff's twin leads", LineDoubling.TwinLeadChance)
    ]);

    /// <summary>A part switching its instrument in a section (<see cref="InstrumentsOverTime" />).</summary>
    public static GenreChoice InstrumentSwitches { get; } = Chances("instrumentSwitches", "Instrument switches", [("sections", "Sections switching an instrument", InstrumentsOverTime.SwitchChance)]);

    /// <summary>
    ///     The strong effect a song plays on one part, or none: an echo, a tremolo, a sweep, a gate, a filter's wobble, squelch
    ///     or opening, or a pump on its pads, chords and bass (<see cref="Expression.StrongEffects" />).
    /// </summary>
    public static GenreChoice StrongEffect { get; } = Pool("strongEffect", "Strong effect",
        Expression.StrongEffects.Select(x => (Name(x.Effect.ToString()), Expression.LabelOf(x.Effect), x.Weight)));

    /// <summary>How an echo repeats: after a dotted 8th, a quarter or an 8th, dying away, or a dotted 8th once, a cascade, a genre's alone (<see cref="Expression.EchoKinds" />).</summary>
    public static GenreChoice Echo { get; } = Pool("echo", "Echo",
        Expression.EchoKinds.Select(x => (Name(x.Kind.ToString()), Expression.LabelOf(x.Kind), x.Weight)));

    /// <summary>
    ///     How far each pitched part sits from the middle, its role's spread moved wider or narrower (<see cref="Panning.SpreadOf" />),
    ///     as it is where a genre does not say.
    /// </summary>
    public static GenreChoice Pan { get; } = Values("pan", "Pan",
        PartsOf(TrackRole.Melody, TrackRole.Chords, TrackRole.Bass, TrackRole.Pad, TrackRole.CounterMelody, TrackRole.Riff, TrackRole.Rhythm), Facet.Sound);

    /// <summary>
    ///     How loud and busy a song is, its energy leaning high or low, how much its sections differ from it
    ///     (<see cref="SectionEnergy" />), how long the band swells into a louder section, a beat to a phrase
    ///     (<see cref="FormLayers.GenreLiftReach" />), and how evenly its parts are played (<see cref="VelocityLayers.SpreadOf" />),
    ///     as it is where a genre does not say.
    /// </summary>
    public static GenreChoice Dynamics { get; } = Values("dynamics", "Dynamics",
        [("energy", "How loud and busy"), ("contrast", "How much its sections differ"), ("lift", "How long a lift swells"), ("evenness", "How evenly it is played")], Facet.Form);

    /// <summary>How many phrases of four bars a section's pattern may have, its progression's cycle: one, as every song's has, to four.</summary>
    public static ImmutableArray<int> PhraseCounts { get; } = [1, 2, 3, 4];

    /// <summary>How many phrases a section's pattern has and how likely each is as the songs have it: one, four bars, alone, the longer a genre's.</summary>
    public static ImmutableArray<(int Phrases, ByConvention Weight)> PhraseWeights { get; } = [..PhraseCounts.Select(x => (x, x == 1 ? new ByConvention(1, 1, 1) : default))];

    /// <summary>A count of phrases as a genre names it: by the bars they make, "eightBars".</summary>
    public static string PhrasesName(int phrases) => phrases switch
    {
        1 => "fourBars",
        2 => "eightBars",
        3 => "twelveBars",
        4 => "sixteenBars",
        _ => throw new ArgumentOutOfRangeException(nameof(phrases), phrases, "A pattern has one to four phrases.")
    };

    /// <summary>
    ///     How long a section's pattern is, its progression's cycle, in phrases of four bars: four bars, as the songs' are,
    ///     eight, twelve, as the blues has, or sixteen (<see cref="Composition.Meter.Phrases" />).
    /// </summary>
    public static GenreChoice Phrases { get; } = Pool("phrases", "Section's pattern",
        PhraseWeights.Select(x => (PhrasesName(x.Phrases), $"{x.Phrases * Composition.Meter.DefaultPhraseBars} bars", x.Weight)));

    /// <summary>How many bars a section's phrase has: four, three, or four and two, its last two again (<see cref="PhraseLengths" />).</summary>
    public static GenreChoice PhraseBars { get; } = Pool("phraseBars", "Phrase length",
        PhraseLengths.Options.Select(x => (PhraseLengths.NameOf(x.Bars), PhraseLengths.LabelOf(x.Bars), x.Weight)));

    /// <summary>Whether a song takes one of the forms songs are written in, or one of its own (<see cref="SongForms" />).</summary>
    public static GenreChoice FormChances { get; } = Chances("formChances", "Form or its own", [("forms", "One of the forms", SongForms.FormsChance)]);

    /// <summary>The forms songs are written in, a verse and a chorus coming back, with a pre-chorus or a bridge (<see cref="SongForms.Forms" />).</summary>
    public static GenreChoice Form { get; } = Shortened(Pool("form", "Form", SongForms.Forms.Select(x => (SongForms.NameOf(x.Value), SongForms.LabelOf(x.Value), new ByConvention(x.Weight, x.Weight, x.Weight)))),
        SongForms.Forms.Select(x => SongForms.ShortOf(x.Value)));

    /// <summary>The feel the song plays in, straight, a tuplet or a grouping (<see cref="Feels" />).</summary>
    public static GenreChoice Feel { get; } = Pool("feel", "Feel", Feels.Weights.Select(x => (Feels.NameOf(x.Feel), Capitalised(Feels.Label(x.Feel)), x.Weight)));

    /// <summary>The chances of the feel's: that the song swings, and that a section's drums play in half or double time.</summary>
    public static GenreChoice FeelChances { get; } = Chances("feelChances", "Swing and half time",
    [
        ("swing", "Swings", Groove.SwingChance), ("halfTime", "A section's drums in half time", Groove.HalfTimeChance),
        ("doubleTime", "A section's drums in double time", Groove.DoubleTimeChance)
    ]);

    /// <summary>The values of the feel's: how much a song that swings swings, and how fast a tempo still swings its 16ths rather than its 8ths.</summary>
    public static GenreChoice FeelValues { get; } = Values("feelValues", "Swing", [("swing", "How much it swings"), ("sixteenths", "16ths swung at faster tempos")], Facet.Feel);

    /// <summary>The scale the song starts in (<see cref="Scales" />).</summary>
    public static GenreChoice Scale { get; } = Described(Pool("scale", "Scale", Scales.All.Select(x => (Name(x.Name), x.Name, x.Weight))),
        Scales.All.Select(x => $"{x.Description} {NotesOnC(x.Offsets.Select(o => (double)o))}"));

    /// <summary>
    ///     The chords' shapes (<see cref="ChordShapes" />), as likely as the songs have them, their levels' weights times
    ///     their shares of them.
    /// </summary>
    public static GenreChoice ChordShape { get; } = Described(Pool("chordShape", "Chord shapes",
        ChordShapes.All.Select(x =>
        {
            var level = ChordShapes.Levels.Single(l => l.Level == x.Unconventionality).Weight;
            var share = x.Weight / ChordShapes.All.Where(y => y.Unconventionality == x.Unconventionality).Sum(y => y.Weight);
            return (Name(x.Name), x.Name, new ByConvention(level.Plain * share, level.Tuned * share, level.Wild * share));
        })),
        ChordShapes.All.Select(x => $"{x.Description} {NotesOnC(x.Targets)}"));

    /// <summary>
    ///     How often each part rests in a section, from 0, never more than the songs' least, through 50, as the songs have it,
    ///     to 100, the most (<see cref="Arrangement.GenreRestOdds" />): a dub's drums and bass dropping out, a beat's pad
    ///     left out.
    /// </summary>
    public static GenreChoice Rests { get; } = Values("rests", "Parts resting",
        PartsOf([..Arrangement.Rests.Select(x => x.Part)]), Facet.Form);

    // the pitched parts' pools, in the order the page shows them
    internal static readonly (TrackRole Role, InstrumentRole Pool)[] InstrumentPools =
    [
        (TrackRole.Melody, InstrumentRoles.Melody),
        (TrackRole.Chords, InstrumentRoles.Chords),
        (TrackRole.Bass, InstrumentRoles.Bass),
        (TrackRole.Pad, InstrumentRoles.Pad),
        (TrackRole.CounterMelody, InstrumentRoles.CounterMelody),
        (TrackRole.Riff, InstrumentRoles.Riff),
        (TrackRole.Rhythm, InstrumentRoles.Rhythm),
        (TrackRole.Solo, InstrumentRoles.Solo)
    ];

    /// <summary>Every pitched part's instruments, by its role, its pool's (<see cref="InstrumentRoles" />).</summary>
    public static ImmutableDictionary<TrackRole, GenreChoice> Instruments { get; } = InstrumentPools.ToImmutableDictionary(
        x => x.Role,
        x => Pool($"instrument.{Name(x.Role.ToString())}", TrackRoles.LabelOf(x.Role), x.Pool.Everything.Select(i => (Name(i.Name), i.Name, i.Weight)))
    );

    /// <summary>The drum kits' sounds, by their General MIDI programs, the standard one every song's with no genre.</summary>
    public static ImmutableArray<(int Program, string Name)> Kits { get; } =
        [(0, "standard"), (8, "room"), (16, "power"), (24, "electronic"), (25, "tr808"), (32, "jazz"), (40, "brush"), (48, "orchestra")];

    /// <summary>A kit's name, by its program.</summary>
    public static string KitNameOf(int program) => Kits.Single(x => x.Program == program).Name;

    /// <summary>The drum kit's sound (<see cref="GeneralMidi.KitName" />).</summary>
    public static GenreChoice Kit { get; } = Pool("kit", "Drum kit", Kits.Select(x => (x.Name, GeneralMidi.KitName(x.Program), Even(x.Program == 0 ? 1 : 0))));

    /// <summary>The rooms a song's reverb may be, each as the Roland GS reverb macro that sets it, by its name.</summary>
    public static ImmutableArray<(int Macro, string Name, string Label)> Rooms { get; } = [(1, "room", "A room"), (4, "hall", "A hall"), (5, "plate", "A plate")];

    /// <summary>A room's name, by its macro.</summary>
    public static string RoomNameOf(int macro) => Rooms.Single(x => x.Macro == macro).Name;

    /// <summary>The room the song's reverb is, the player's own where a genre names none: a small room, a hall or a plate.</summary>
    public static GenreChoice Room { get; } = Pool("room", "Reverb room", Rooms.Select(x => (x.Name, x.Label, Even(0))));

    /// <summary>The effect a song plays one part through, if any: a wah or a phaser (<see cref="Insertions" />).</summary>
    public static GenreChoice Insertion { get; } = Pool("insertion", "Insertion effect", Insertions.Kinds.Select(x => (Name(x.Kind.ToString()), Insertions.LabelOf(x.Kind), x.Weight)));

    /// <summary>
    ///     Every part's rhythm, a pitched part's or the drums' of a role, as values: its speed, how far off the beat it
    ///     plays, how fine it goes, how much weight its weaker beats have, how full it is and how much it repeats its
    ///     cycles (<see cref="GenreRhythm" />), and the melody's busyness.
    /// </summary>
    public static ImmutableDictionary<RhythmPart, GenreChoice> Rhythms { get; } = Enum.GetValues<RhythmPart>().ToImmutableDictionary(
        x => x,
        x => Values($"rhythm.{Name(x.ToString())}", GenreRhythm.LabelOf(x),
            [..GenreRhythm.Settings, ..x == RhythmPart.Melody ? [("busyness", "Busyness")] : Array.Empty<(string, string)>()], Facet.Groove)
    );

    /// <summary>
    ///     Every part's own feel, a pitched part's or the drums' of a role, such as a time keeper's grouping of the
    ///     tresillo's 3+3+2, over the song's, drawn only where a genre names it.
    /// </summary>
    public static ImmutableDictionary<RhythmPart, GenreChoice> PartFeels { get; } = Enum.GetValues<RhythmPart>().ToImmutableDictionary(
        x => x,
        x => Pool($"feel.{Name(x.ToString())}", Rhythms[x].Label, Feels.Weights.Select(f => (Feels.NameOf(f.Feel), Capitalised(Feels.Label(f.Feel)), f.Weight)))
    );

    // the drums' roles in the order the page shows them
    private static readonly DrumRole[] RolesInOrder = [DrumRole.Ground, DrumRole.Backbeat, DrumRole.Time, DrumRole.Colour];

    /// <summary>Which drums take a role in the groove, each by its affinity for it as the songs have it (<see cref="DrumRoles" />).</summary>
    public static ImmutableDictionary<DrumRole, GenreChoice> DrumRoles { get; } = RolesInOrder.ToImmutableDictionary(
            x => x,
            x => WithFamilies(Pool($"drumRole.{Name(x.ToString())}", Rhythms[Composition.DrumRoles.PartOf(x)].Label,
                DrumGroups.AllDrums.Select(d => (Composition.DrumPieces.NameOf(d), d.Name, Even(d.AffinityFor(x))))))
        );

    /// <summary>The chances of the parts, every one a song may have or leave out, each as a middling song has it.</summary>
    public static GenreChoice Parts { get; } = WithTwinsRiff(Chances("parts", "Lineup",
    [
        ..new[] { TrackRole.Melody, TrackRole.Chords, TrackRole.Bass, TrackRole.Pad, TrackRole.CounterMelody, TrackRole.Drum, TrackRole.Riff, TrackRole.RiffTwin, TrackRole.Rhythm, TrackRole.Solo }
            .Select(x => (Name(x.ToString()), TrackRoles.LabelOf(x), SongParts.ChanceOf(x)))
    ]));

    // the riff's twin tagged as playing only with the riff
    private static GenreChoice WithTwinsRiff(GenreChoice parts) =>
        parts with { Options = [..parts.Options.Select(x => x.Name == Name(nameof(TrackRole.RiffTwin)) ? x with { Tags = [$"with:{Name(nameof(TrackRole.Riff))}"] } : x)] };

    /// <summary>The drums the song plays: the kit, the kit and percussion, or percussion (<see cref="DrumSetups" />).</summary>
    public static GenreChoice DrumSetup { get; } = Pool("drumSetup", "Drum setup",
        DrumSetups.All.Select(x => (NameOf(x.Setup), DrumSetups.LabelOf(x.Setup), RhythmicUnconventionality.WeightEnds(x.Weight, x.Lean))));

    /// <summary>The drum pieces the song may draw (<see cref="DrumPieces" />).</summary>
    public static GenreChoice DrumPieces { get; } = WithFamilies(Pool("drumPieces", "Drum pieces",
        DrumGroups.AllDrums.Select(x => (Composition.DrumPieces.NameOf(x), x.Name, Even(x.Weight)))));

    /// <summary>
    ///     A choice of drums, every one tagged by the setup that draws it (<see cref="DrumSetups.SelectSongDrums" />): "kit"
    ///     for a kit's drum, a tambourine's or a shaker's among them, and "percussion" for the percussion group's, which a
    ///     song of the kit and the percussion draws, so that the page offers the setups the drums chosen make.
    /// </summary>
    private static GenreChoice WithFamilies(GenreChoice choice) => choice with
    {
        Options = [..choice.Options.Select(option => option with { Tags = FamiliesOf(DrumGroups.AllDrums.Single(x => Composition.DrumPieces.NameOf(x) == option.Name)) })]
    };

    private static ImmutableArray<string> FamiliesOf(PercussionInstrumentDefinition drum) =>
        [DrumGroups.Percussion.Drums.Contains(drum) ? "percussion" : "kit"];

    private static string Capitalised(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>Every choice, in the order a link writes them.</summary>
    public static ImmutableArray<GenreChoice> All { get; } =
    [
        Tempo, Length, Darkness, Intensity, Meter, Feel, FeelChances, FeelValues, Scale, ChordShape, Arpeggio, Colour, RootMotions, RootDegrees, Strictness, SectionHome, CadenceDegrees, CadenceRaise, ChordScales, OwnMode, HarmonicRhythm, Turnaround, PassingChords, PassingKinds, Planing, HarmonyDevices, SectionPedal, Loop, LineScale, MelodyRange, Sung, Ornaments, Tension, Appoggiatura, Bass, OctaveBass, AlternatingBass, Glide, GlideRate, BassLanding, Effects, GateParts, Articulation, Levels, Rests, Voice, GrooveHold, Pins, HatRolls, Ghosts, GhostNotes, Rudiments, Pocket, Fills, KickOut, Drop, DropKind, Mutation, MixFilter, Breakdown, StopTime, EnsembleHits, Push, KeyChanges, KeyLift, SectionKey, Hook, Riff, Solos, SoloCount, SoloShare, Soloists, DrumSoloFeet, Stickings, Intro, OpeningAlone, OpeningPart, Ending, StrongEffect, Echo, Room, Insertion, Pan, FormChances, Form, SectionLength, RoleLength, PhraseScheme, Texture, Alone, Dynamics, Phrases, PhraseBars,
        SectionChanges, Polymeter, PolymeterCell, LineRun, SoloAnswers, CallAndResponse, LastRepeat, Doubling, InstrumentSwitches, Space, DrumJob, Marks, Register, DrumSounds, DrumStrokes, LouderForms, HarmonyInterval, Vamp, RiffScale,
        ..InstrumentPools.Select(x => Instruments[x.Role]),
        Kit,
        Parts, DrumSetup, DrumPieces,
        ..Enum.GetValues<RhythmPart>().Select(x => Rhythms[x]),
        ..Enum.GetValues<FigurePlayer>().Select(x => Figures[x]),
        ..Enum.GetValues<RhythmPart>().Select(x => PartFeels[x]),
        ..RolesInOrder.Select(x => DrumRoles[x])
    ];

    /// <summary>
    ///     The choices of a device a song plays or not: every choice of chances, and every pool with an option of none, such as
    ///     the arpeggios; a genre that leaves one out plays it as the songs do, as a preset does where the songs' amounts are
    ///     its own, following them as they are tuned.
    /// </summary>
    public static ImmutableArray<GenreChoice> Devices { get; } =
        [..All.Where(x => x.Kind == GenreChoiceKind.Chances || x.Kind == GenreChoiceKind.Pool && x.Options.Any(o => o.Name == "none"))];

    /// <summary>A pool's choice, every option's own weights against every end's heaviest, 0 at an end that allows none.</summary>
    private static GenreChoice Pool(string name, string label, IEnumerable<(string Name, string Label, ByConvention Weight)> options)
    {
        var all = options.ToArray();
        var (plain, tuned, wild) = (all.Max(x => x.Weight.Plain), all.Max(x => x.Weight.Tuned), all.Max(x => x.Weight.Wild));
        static double Of(double weight, double heaviest) => heaviest > 0 ? weight / heaviest : 0;
        return new GenreChoice(name, label, GenreChoiceKind.Pool,
            [..all.Select(x => new GenreOption(x.Name, x.Label, new ByConvention(Of(x.Weight.Plain, plain), Of(x.Weight.Tuned, tuned), Of(x.Weight.Wild, wild))))]);
    }

    // a weight drawn alike at every conventionality
    private static ByConvention Even(double weight) => new(weight, weight, weight);

    /// <summary>A choice with its options described, in their order.</summary>
    private static GenreChoice Described(GenreChoice choice, IEnumerable<string> descriptions) =>
        choice with { Options = [..choice.Options.Zip(descriptions, (option, description) => option with { Description = description })] };

    /// <summary>A choice with its options' short labels, in their order.</summary>
    private static GenreChoice Shortened(GenreChoice choice, IEnumerable<string> shorts) =>
        choice with { Options = [..choice.Options.Zip(shorts, (option, @short) => option with { Short = @short })] };

    /// <summary>
    ///     Heights in semitones as notes on C, "On C: C E♭ G.", a height between two notes both, "E♭/E", as the scale
    ///     chooses between them.
    /// </summary>
    public static string NotesOnC(IEnumerable<double> heights)
    {
        string[] names = ["C", "D♭", "D", "E♭", "E", "F", "F♯", "G", "A♭", "A", "B♭", "B"];
        string Note(double height) => names[(int)height % 12];
        return $"On C: {string.Join(" ", heights.Select(x => x == Math.Floor(x) ? Note(x) : $"{Note(Math.Floor(x))}/{Note(Math.Ceiling(x))}"))}.";
    }

    /// <summary>A choice of chances, every one's own its chances as the songs have them.</summary>
    private static GenreChoice Chances(string name, string label, IEnumerable<(string Name, string Label, ByConvention Chance)> options) =>
        new(name, label, GenreChoiceKind.Chances, [..options.Select(x => new GenreOption(x.Name, x.Label, x.Chance))]);

    /// <summary>Parts as options of a choice, by their names and their labels.</summary>
    private static IEnumerable<(string Name, string Label)> PartsOf(params TrackRole[] roles) => roles.Select(x => (Name(x.ToString()), TrackRoles.LabelOf(x)));

    /// <summary>A choice of values, each a lean from the songs' own, read at the song's facet it follows (<see cref="Genre.AtSong" />).</summary>
    private static GenreChoice Values(string name, string label, IEnumerable<(string Name, string Label)> options, Facet follows) =>
        new(name, label, GenreChoiceKind.Values, [..options.Select(x => new GenreOption(x.Name, x.Label, Even(0.5)))]) { Follows = follows };

    /// <summary>
    ///     A name as a genre gives it: in camel case, its words run together, "harmonicMinor", "electricBassFinger",
    ///     "counterMelody".
    /// </summary>
    public static string Name(string text)
    {
        var words = text.Split([' ', '-', '(', ')', '.', '/'], StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(words.Select((word, i) => (i == 0 ? char.ToLowerInvariant(word[0]) : char.ToUpperInvariant(word[0])) + word[1..]));
    }

    /// <summary>The name of a drum setup's option.</summary>
    public static string NameOf(Composition.DrumSetup setup) => setup switch
    {
        Composition.DrumSetup.Kit => "kit",
        Composition.DrumSetup.KitAndPercussion => "kitAndPercussion",
        Composition.DrumSetup.Percussion => "percussion",
        _ => throw new ArgumentOutOfRangeException(nameof(setup), setup, "No name for the drum setup.")
    };
}
