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
    ///     A tempo, as a multiple of <see cref="Composition.Meter.BaseTempo" />, by its amount from 0, 60 beats a minute,
    ///     through 1/2, 120, to 1, 240, every like step a like ratio, as a tempo given is (the page's tempo's steps).
    /// </summary>
    public static double TempoOf(double amount) => Math.Pow(2, 2 * amount - 1);

    /// <summary>A tempo's amount, as a multiple of <see cref="Composition.Meter.BaseTempo" /> (<see cref="TempoOf" />).</summary>
    public static double AmountOfTempo(double tempo) => (Math.Log2(tempo) + 1) / 2;

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
        Scale = new RangeScale("bpm", 60, 240, x => TempoOf(x) * Composition.Meter.BaseTempo, x => AmountOfTempo(x / Composition.Meter.BaseTempo))
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

    /// <summary>The meter the song's bars are in (<see cref="Composition.Meter.Options" />).</summary>
    public static GenreChoice Meter { get; } = Pool("meter", "Meter",
        Composition.Meter.Options.Select(x => (Composition.Meter.NameOf(x.Meter), Composition.Meter.NameOf(x.Meter), x.Weight)));

    /// <summary>
    ///     How a section's chord roots move, each motion leaned from as the songs have it, falling a fifth the most, to more
    ///     or less (<see cref="Progressions.Motions" />): a genre's ii–V or its ♭VII–IV–I.
    /// </summary>
    public static GenreChoice RootMotions { get; } = Values("rootMotion", "Root motions", Progressions.Motions);

    /// <summary>
    ///     The chords that leave the section's scale, each its chance as the songs have it (<see cref="Composition.ChordScales" />):
    ///     a chord before a root a fifth below its dominant, a chord in a mode borrowed from the home's parallels, and a
    ///     section's every chord but its home its own dominant, the blues', which the songs never play.
    /// </summary>
    public static GenreChoice ChordScales { get; } = Chances("chordScales", "Chords off the scale",
    [
        ("applied", "Applied dominants", Composition.ChordScales.Applied),
        ("borrowed", "Borrowed chords", Composition.ChordScales.Borrowed),
        ("dominant", "Every chord its dominant", Composition.ChordScales.Dominant),
        ("own", "A chord's own scale", Composition.ChordScales.Own)
    ]);

    /// <summary>The chance a section plays the song's first section's chords, the arrangement telling them apart, as a beat's loop does; the songs never.</summary>
    public static GenreChoice Loop { get; } = Chances("loop", "Loop", [("sections", "Sections on the first's chords", SectionGenerator.Loop)]);

    /// <summary>The chance a later appearance is a solo, as the songs have it by its role (<see cref="Composition.Solos.Chance" />).</summary>
    public static GenreChoice Solos { get; } = Chances("solos", "Solos", [("sections", "A section soloed", Composition.Solos.Chance)]);

    /// <summary>Who solos (<see cref="Composition.Solos.Soloists" />).</summary>
    public static GenreChoice Soloists { get; } = Pool("soloists", "Soloists",
        Composition.Solos.Soloists.Select(x => (Name(x.Part.ToString()), Composition.Solos.LabelOf(x.Part), x.Weight)));

    /// <summary>How the song starts before its first section (<see cref="FormLayers.Intros" />): the whole band, a count-in, or the parts one by one.</summary>
    public static GenreChoice Intro { get; } = Pool("intro", "Intro",
        FormLayers.IntroWeights.Select(x => (Name(x.Intro.ToString()), FormLayers.LabelOf(x.Intro), x.Weight)));

    /// <summary>How the song ends (<see cref="FormLayers.Endings" />).</summary>
    public static GenreChoice Ending { get; } = Pool("ending", "Ending",
        FormLayers.EndingWeights.Select(x => (Name(x.Ending.ToString()), FormLayers.LabelOf(x.Ending), x.Weight)));

    /// <summary>The chance a section sits on its home chord, every chord of its pattern the home, a vamp (<see cref="SectionGenerator.Vamp" />); the songs never.</summary>
    public static GenreChoice Vamp { get; } = Chances("vamp", "Vamp", [("sections", "Sections on one chord", SectionGenerator.Vamp)]);

    /// <summary>The chance a section's riff plays in power chords, a perfect fifth over every note, as rock's riffs are (<see cref="SectionGenerator.PowerChordChance" />).</summary>
    public static GenreChoice Riff { get; } = Chances("riff", "Riff",
    [
        ("powerChords", "In power chords", SectionGenerator.PowerChordChance),
        ("chugs", "Palm-muted chugs", SectionGenerator.ChugChance)
    ]);

    /// <summary>The chance a section's pattern turns around in its last bar, a V or a ii–V into its start, a reharmonisation, which the songs never play at the plain end and a genre may (<see cref="Progressions.Turnaround" />).</summary>
    public static GenreChoice Turnaround { get; } = Chances("turnaround", "Turnaround", [("sections", "Sections turned around", Progressions.Turnaround)]);

    /// <summary>The chance a chord a bar long passes into the next by its dominant, a reharmonisation, which the songs never play at the plain end and a genre may (<see cref="Composition.PassingChords" />).</summary>
    public static GenreChoice PassingChords { get; } = Chances("passingChords", "Passing chords", [("chords", "Chords passing into the next", Composition.PassingChords.Chance)]);

    /// <summary>The chance a section plays its first phrase in stop time, the band hitting once a bar under the lines (<see cref="StopTime.Chance" />).</summary>
    public static GenreChoice StopTime { get; } = Chances("stopTime", "Stop time", [("sections", "Sections in stop time", Composition.StopTime.Chance)]);

    /// <summary>The chance a section planes, every chord the home's shape moved by semitones, a reharmonisation, which the songs never play at the plain end and a genre may (<see cref="Composition.Planing" />).</summary>
    public static GenreChoice Planing { get; } = Chances("planing", "Planing", [("sections", "Sections planed", Composition.Planing.Chance)]);

    /// <summary>The chances a song's last section that came back lifts its key, a section plays in a key of its own, and any pattern's start changes it (<see cref="KeyChange" />): a ballad's last chorus up, a blues in one key.</summary>
    public static GenreChoice KeyChanges { get; } = Chances("keyChanges", "Key changes",
        [
            ("lastSection", "A last section lifted", KeyChange.LastSectionChance), ("sections", "A section in a key of its own", KeyChange.SectionChance),
            ("patterns", "Any pattern's start", KeyChange.ElsewhereChance)
        ]);

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

    /// <summary>The chances a line's part slurs its steps, a piano's part rolls its held chords, and the chords are struck short (<see cref="Articulations.Apply" />).</summary>
    public static GenreChoice Articulation { get; } = Chances("articulation", "Articulation",
        [("legato", "Slurred steps", Articulations.LegatoChance), ("roll", "Rolled piano chords", Articulations.RollChance), ("stabs", "Chords as stabs", Articulations.StabChance)]);

    /// <summary>The chance a song pins a drum role to its figure, every bar alike: the ground, the backbeat and the time (<see cref="DrumRoles.Pins" />).</summary>
    public static GenreChoice Pins { get; } = Chances("pins", "Parts pinned", [..Composition.DrumRoles.PinnedParts.Select(x => (Name(x.ToString()), Composition.DrumRoles.PinLabelOf(x), Composition.DrumRoles.Pins))]);

    /// <summary>How long a section plays, its pattern played over and over: four bars to thirty-two, a dance track's the longest, which the songs never play (<see cref="Composition.SectionLength" />).</summary>
    public static GenreChoice SectionLength { get; } = Pool("sectionLength", "Section's length",
        Composition.SectionLength.Options.Select(x => (Composition.SectionLength.NameOf(x.Plays), $"{x.Plays * Composition.Meter.PhraseBarCount} bars", x.Weight)));

    /// <summary>How an appearance brings its parts in over its phrases: steady, building, thinning or one part alone (<see cref="Composition.Texture" />).</summary>
    public static GenreChoice Texture { get; } = Pool("texture", "Texture", Composition.Texture.Kinds.Select(x => (Name(x.Kind.ToString()), Composition.Texture.LabelOf(x.Kind), x.Weight)));

    /// <summary>How the chords and the rhythm part break their chords, or play them whole (<see cref="Arpeggios" />): an arpeggiator's up and down a synth genre's.</summary>
    public static GenreChoice Arpeggio { get; } = Pool("arpeggio", "Arpeggios", Arpeggios.Patterns.Select(x => (Name(x.Pattern.ToString()), Arpeggios.LabelOf(x.Pattern), x.Weight)));

    /// <summary>The chance a bar's hi-hat rolls its last beat in 32nds or 16th triplets, the trap's (<see cref="HatRolls.Chance" />).</summary>
    public static GenreChoice HatRolls { get; } = Chances("hatRolls", "Hi-hat rolls", [("bars", "Bars ending on a roll", Composition.HatRolls.Chance)]);

    /// <summary>
    ///     How high each pitched part sits, from 0, an octave lower, as a guitar tuned down, through 50, as the songs have it,
    ///     to 100, an octave higher, a quarter's way from either end moving it (<see cref="Registers" />).
    /// </summary>
    public static GenreChoice Register { get; } = Values("register", "Register",
        [("melody", "Melody"), ("chords", "Chords"), ("bass", "Bass"), ("pad", "Pad"), ("counterMelody", "Counter-melody"), ("riff", "Riff"), ("rhythm", "Rhythm"), ("solo", "Soloist")]);

    /// <summary>
    ///     How often each of a drum's own sounds plays, from 0, an eighth as often, through 50, as the songs have it, to 100,
    ///     eight times as often, as its steady stroke, an accent and in a fill, one a wild song's alone played at the plain end
    ///     where a genre leans it up: the cross-stick of a reggae or a ballad, a jazz drummer's hi-hat foot (<see cref="DrumSounds.OddsOf" />).
    /// </summary>
    public static GenreChoice DrumSounds { get; } = Values("drumSounds", "Drum sounds", Composition.DrumSounds.Leaned.Select(x => (x.Name, x.Label)));

    /// <summary>The chance a part's guitar turns driven in the song's louder appearances, a clean verse's guitar distorted in the chorus (<see cref="LouderForms" />).</summary>
    public static GenreChoice LouderForms { get; } = Chances("louderForms", "Louder forms", [("guitars", "A guitar driven in the louder sections", Composition.LouderForms.Chance)]);

    /// <summary>
    ///     The interval a line harmonised plays apart from it, the melody's by the counter-melody and the riff's by its twin,
    ///     with its inversion: thirds and sixths, the songs', fourths and fifths, an organum's or a riff's in parallel, and
    ///     seconds and sevenths, the wildest twins' (<see cref="LineDoubling.IntervalOf" />).
    /// </summary>
    public static GenreChoice HarmonyInterval { get; } = Pool("harmonyInterval", "Harmony's interval",
    [
        ("thirds", "Thirds and sixths", new ByConvention(1, 1, 1)), ("fourths", "Fourths and fifths", new ByConvention(0, 0.05, 0.4)),
        ("seconds", "Seconds and sevenths", new ByConvention(0, 0, 0.2))
    ]);

    /// <summary>What the drums do: keep time, as the songs' always do, or mark the form, as a calm piece's percussion does (<see cref="DrumMarks" />).</summary>
    public static GenreChoice DrumJob { get; } = Pool("drumJob", "Drums' job", DrumMarks.Jobs.Select(x => (DrumMarks.NameOf(x.Job), x.Job == Composition.DrumJob.Groove ? "Keeping time" : "Marking the form", x.Weight)));

    /// <summary>How often drums marking the form strike, from 0, seldom, through 50, as they do, to 100, often (<see cref="DrumMarks.GenreOdds" />).</summary>
    public static GenreChoice Marks { get; } = Values("marks", "Drums' marks", [("often", "How often")]);

    /// <summary>The chance a verse leaves its melody out, the room a rap's voice takes over the beat (<see cref="Arrangement.VoiceChance" />).</summary>
    public static GenreChoice Voice { get; } = Chances("voice", "Verses for a voice", [("verses", "Verses without a melody", Arrangement.VoiceChance)]);

    /// <summary>Which bars of a 4-bar pattern repeat which, AAAA to ABCD: a loop's one bar over and over (<see cref="PhraseSchemes" />).</summary>
    public static GenreChoice PhraseScheme { get; } = Pool("phraseScheme", "Bars repeating", PhraseSchemes.Weights.Select(x => (x.Scheme.ToLowerInvariant(), x.Scheme, x.Weight)));

    /// <summary>The chance a lift the drums announce is built to and dropped at, the band out for the bar before it; the songs never (<see cref="FormLayers.Drop" />).</summary>
    public static GenreChoice Drop { get; } = Chances("drop", "Builds and drops", [("lifts", "Lifts built to a drop", FormLayers.Drop)]);

    /// <summary>
    ///     The chance a section's melody is on a hook, a chorus's and another section's, as a beat's sample is; the songs' a
    ///     chorus's alone (<see cref="MelodyLayers.HookChanceOf" />).
    /// </summary>
    public static GenreChoice Hook { get; } = Chances("hook", "Hook",
        [("choruses", "Choruses on a hook", MelodyLayers.HookChanceOf(SectionRole.Chorus)), ("sections", "Other sections on a hook", MelodyLayers.HookChanceOf(SectionRole.Verse))]);

    /// <summary>How long a section's chords last (<see cref="Composition.HarmonicRhythm.Spans" />): half a bar, a bar or two bars, the faster the more energy.</summary>
    public static GenreChoice HarmonicRhythm { get; } = Pool("harmonicRhythm", "Chords' pace",
        Composition.HarmonicRhythm.Spans.Select(x => (Composition.HarmonicRhythm.NameOf(x.Span.Value), Composition.HarmonicRhythm.LabelOf(x.Span.Value),
            new ByConvention(x.Span.Weight, x.Span.Weight, x.Span.Weight))));

    /// <summary>How much the drums fill, from 0, seldom and short, through 50, as the songs have it, to 100, often and long (<see cref="FillLayers.GenreAmountOdds" />).</summary>
    public static GenreChoice Fills { get; } = Values("fills", "Fills", [("amount", "Amount")]);

    /// <summary>How far the parts play off the beat, from 0, tight, through 50, as the songs have it, to 100, laid back (<see cref="Pocket.GenreReach" />).</summary>
    public static GenreChoice Pocket { get; } = Values("pocket", "Pocket", [("laidBack", "Laid back")]);

    /// <summary>How steady the groove is, from 0, moving more, through 50, as the songs move it, to 100, held, its backbeat on 2 and 4 (<see cref="RhythmicUnconventionality.GrooveHold" />).</summary>
    public static GenreChoice GrooveHold { get; } = Values("grooveHold", "Groove held", [("held", "Held")]);

    /// <summary>
    ///     How loud each part plays against the others, from 0, quietest, through 50, as the songs have it, to 100, loudest
    ///     (<see cref="VelocityLayers.GenreLevel" />): a beat's drums leading it, a dub's bass; the mix's volumes over them.
    /// </summary>
    public static GenreChoice Levels { get; } = Values("levels", "Levels",
        [..new[] { TrackRole.Melody, TrackRole.Chords, TrackRole.Bass, TrackRole.Pad, TrackRole.CounterMelody, TrackRole.Riff, TrackRole.Rhythm, TrackRole.Solo, TrackRole.Drum }
            .Select(x => (Name(x.ToString()), TrackRoles.LabelOf(x)))]);

    /// <summary>
    ///     How much reverb each part plays with, from 0, dry, through 50, as the songs have it, to 100, as wet as can be
    ///     (<see cref="Expression.Reverb(TrackRole, Genre)" />): an ambient wash, a funk band's dry room.
    /// </summary>
    public static GenreChoice Space { get; } = Values("space", "Space",
        [..new[] { TrackRole.Melody, TrackRole.Chords, TrackRole.Bass, TrackRole.Pad, TrackRole.CounterMelody, TrackRole.Riff, TrackRole.Rhythm, TrackRole.Solo, TrackRole.Drum }
            .Select(x => (Name(x.ToString()), TrackRoles.LabelOf(x)))]);

    /// <summary>How much the bass leads into a new chord, from 0, never, through 50, as its instrument has it, to 100, walking (<see cref="BassLeadingLayers.GenreLeadOdds" />).</summary>
    public static GenreChoice Bass { get; } = Values("bass", "Bass", [("leading", "Leading into a chord")]);

    /// <summary>The chance a section's bass plays in octaves, its notes off the beat the note before an octave up (<see cref="SectionGenerator.OctaveBassChance" />).</summary>
    public static GenreChoice OctaveBass { get; } = Chances("octaveBass", "Octave bass", [("sections", "Sections in octaves", SectionGenerator.OctaveBassChance)]);

    /// <summary>How wide the melody's range is, from 0, narrow as a hook's, through 50, as the songs have it, to 100, wide as a jazz tune's (<see cref="MelodyLayers.GenreRangeReach" />).</summary>
    public static GenreChoice MelodyRange { get; } = Values("melodyRange", "Melody's range", [("wide", "Wide")]);

    /// <summary>The chance a song's bass glides into its notes from the note before, an 808's or an acid line's (<see cref="Expression.GlideChance" />).</summary>
    public static GenreChoice Glide { get; } = Chances("glide", "Glide", [("bass", "A gliding bass", Expression.GlideChance)]);

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

    /// <summary>
    ///     The gentle effects a song plays at all, each its chance as the songs have it by their sound (<see cref="Expression" />):
    ///     a vibrato, a chorus, a held chord's swell and a line's bends.
    /// </summary>
    public static GenreChoice Effects { get; } = Chances("effects", "Effects",
    [
        ("vibrato", "A vibrato", Expression.SongVibratoChance),
        ("chorus", "A chorus", Expression.SongChorusChance),
        ("swell", "A swell", Expression.SongSwellChance),
        ("bends", "Bends", Expression.SongBendChance)
    ]);

    /// <summary>A section's own tempo, one by a metric ratio and its own meter, beside the song's (<see cref="Composition.SectionTempo" />, <see cref="Composition.SectionMeter" />).</summary>
    public static GenreChoice SectionChanges { get; } = Chances("sectionChanges", "A section's own time",
        [
            ("tempo", "A section at a tempo of its own", Composition.SectionTempo.Chance), ("modulation", "A tempo by a metric ratio", Composition.SectionTempo.ModulationChance),
            ("meter", "A section in a meter of its own", Composition.SectionMeter.Chance)
        ]);

    /// <summary>A section's drums and bass in a cycle across the bar (<see cref="Composition.Polymeter" />).</summary>
    public static GenreChoice Polymeter { get; } = Chances("polymeter", "Polymeter", [("sections", "Sections in a polymeter", Composition.Polymeter.Chance)]);

    /// <summary>The solo part answering the melody in its gaps (<see cref="Composition.SoloAnswers" />).</summary>
    public static GenreChoice SoloAnswers { get; } = Chances("soloAnswers", "Solo answers", [("sections", "Sections the solo part answers", Composition.SoloAnswers.Chance)]);

    /// <summary>A last section that came back before played once more, as a last chorus (<see cref="SongForms.LastRepeatChance" />).</summary>
    public static GenreChoice LastRepeat { get; } = Chances("lastRepeat", "Last repeat", [("songs", "A last chorus played twice", SongForms.LastRepeatChance)]);

    /// <summary>A line doubled: in octaves by its own part, the melody harmonised by the counter-melody, the bass with the riff (<see cref="LineDoubling" />).</summary>
    public static GenreChoice Doubling { get; } = Chances("doubling", "Doubling",
    [
        ("octaves", "Lines in octaves", LineDoubling.OctaveChance), ("harmony", "The melody harmonised", LineDoubling.HarmonyChance),
        ("riff", "The bass with the riff", LineDoubling.RiffChance)
    ]);

    /// <summary>A part switching its instrument in a section (<see cref="InstrumentsOverTime" />).</summary>
    public static GenreChoice InstrumentSwitches { get; } = Chances("instrumentSwitches", "Instrument switches", [("sections", "Sections switching an instrument", InstrumentsOverTime.SwitchChance)]);

    /// <summary>The strong effect a song plays on one part, or none: an echo, a tremolo or a sweep, or a pump on its pads, chords and bass (<see cref="Expression.StrongEffects" />).</summary>
    public static GenreChoice StrongEffect { get; } = Pool("strongEffect", "Strong effect",
        Expression.StrongEffects.Select(x => (Name(x.Effect.ToString()), Expression.LabelOf(x.Effect), x.Weight)));

    /// <summary>
    ///     How far each pitched part sits from the middle, its role's spread moved wider or narrower (<see cref="Panning.SpreadOf" />),
    ///     as it is where a genre does not say.
    /// </summary>
    public static GenreChoice Pan { get; } = Values("pan", "Pan",
        [("melody", "Melody"), ("chords", "Chords"), ("bass", "Bass"), ("pad", "Pad"), ("counterMelody", "Counter-melody"), ("riff", "Riff"), ("rhythm", "Rhythm")]);

    /// <summary>
    ///     How loud and busy a song is, its energy leaning high or low, how much its sections differ from it
    ///     (<see cref="SectionEnergy" />), and how long the band swells into a louder section, a beat to a phrase
    ///     (<see cref="FormLayers.GenreLiftReach" />), as it is where a genre does not say.
    /// </summary>
    public static GenreChoice Dynamics { get; } = Values("dynamics", "Dynamics",
        [("energy", "How loud and busy"), ("contrast", "How much its sections differ"), ("lift", "How long a lift swells")]);

    /// <summary>How many phrases of four bars a section's pattern may have, its progression's cycle: one, as every song's has, to four.</summary>
    public static ImmutableArray<int> PhraseCounts { get; } = [1, 2, 3, 4];

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
        PhraseCounts.Select(x => (PhrasesName(x), $"{x * Composition.Meter.PhraseBarCount} bars", x == 1 ? new ByConvention(1, 1, 1) : default)));

    /// <summary>Whether a song takes one of the forms songs are written in, or one of its own (<see cref="SongForms" />).</summary>
    public static GenreChoice FormChances { get; } = Chances("formChances", "Form or its own", [("forms", "One of the forms", SongForms.OwnFormChance)]);

    /// <summary>The forms songs are written in, a verse and a chorus coming back, with a pre-chorus or a bridge (<see cref="SongForms.Forms" />).</summary>
    public static GenreChoice Form { get; } = Pool("form", "Form", SongForms.Forms.Select(x => (SongForms.NameOf(x.Value), SongForms.LabelOf(x.Value), new ByConvention(x.Weight, x.Weight, x.Weight))));

    /// <summary>The feel the song plays in, straight, a tuplet or a grouping (<see cref="Feels" />).</summary>
    public static GenreChoice Feel { get; } = Pool("feel", "Feel", Feels.Weights.Select(x => (Feels.NameOf(x.Feel), Capitalised(Feels.Label(x.Feel)), x.Weight)));

    /// <summary>The chances of the feel's: that the song swings, and that a section's drums play in half or double time.</summary>
    public static GenreChoice FeelChances { get; } = Chances("feelChances", "Swing and half time",
    [
        ("swing", "Swings", Groove.SwingChance), ("halfTime", "A section's drums in half time", Even(Groove.HalfTimeChance)),
        ("doubleTime", "A section's drums in double time", Even(Groove.DoubleTimeChance))
    ]);

    /// <summary>The values of the feel's: how much a song that swings swings, and how fast a tempo still swings its 16ths rather than its 8ths.</summary>
    public static GenreChoice FeelValues { get; } = Values("feelValues", "Swing", [("swing", "How much it swings"), ("sixteenths", "16ths swung at faster tempos")]);

    /// <summary>The scale the song starts in (<see cref="Scales" />).</summary>
    public static GenreChoice Scale { get; } = Described(Pool("scale", "Scale", Scales.All.Select(x => (Name(x.Name), x.Name, x.Weight))),
        Scales.All.Select(x => $"{x.Description} {NotesOnC(x.Offsets.Select(o => (double)o))}"));

    /// <summary>
    ///     The chords' shapes (<see cref="ChordShapes" />), as likely as the songs have them, their levels' weights times
    ///     their shares of them.
    /// </summary>
    public static GenreChoice ChordShape { get; } = Described(Pool("chordShape", "Chord shapes",
        ChordShapes.All.Select(x => (Name(x.Name), x.Name, ChordShapes.Levels.Single(l => l.Level == x.Unconventionality).Weight is var level
            && x.Weight / ChordShapes.All.Where(y => y.Unconventionality == x.Unconventionality).Sum(y => y.Weight) is var share
                ? new ByConvention(level.Plain * share, level.Tuned * share, level.Wild * share)
                : default))),
        ChordShapes.All.Select(x => $"{x.Description} {NotesOnC(x.Targets)}"));

    /// <summary>
    ///     How often each part rests in a section, from 0, never more than the songs' least, through 50, as the songs have it,
    ///     to 100, the most (<see cref="Arrangement.GenreRestOdds" />): a dub's drums and bass dropping out, a beat's pad
    ///     left out.
    /// </summary>
    public static GenreChoice Rests { get; } = Values("rests", "Parts resting",
        [..Arrangement.Rests.Select(x => (Name(x.Part.ToString()), TrackRoles.LabelOf(x.Part)))]);

    // the pitched parts' pools, in the order the page shows them
    private static readonly (TrackRole Role, string Label, InstrumentRole Pool)[] InstrumentPools =
    [
        (TrackRole.Melody, "Melody", InstrumentRoles.Melody),
        (TrackRole.Chords, "Chords", InstrumentRoles.Chords),
        (TrackRole.Bass, "Bass", InstrumentRoles.Bass),
        (TrackRole.Pad, "Pad", InstrumentRoles.Pad),
        (TrackRole.CounterMelody, "Counter-melody", InstrumentRoles.CounterMelody),
        (TrackRole.Riff, "Riff", InstrumentRoles.Riff),
        (TrackRole.Rhythm, "Rhythm", InstrumentRoles.Rhythm),
        (TrackRole.Solo, "Soloist", InstrumentRoles.Solo)
    ];

    /// <summary>Every pitched part's instruments, by its role, its pool's (<see cref="InstrumentRoles" />).</summary>
    public static ImmutableDictionary<TrackRole, GenreChoice> Instruments { get; } = InstrumentPools.ToImmutableDictionary(
        x => x.Role,
        x => Pool($"instrument.{Name(x.Role.ToString())}", x.Label, x.Pool.Instruments.Select(i => (Name(i.Name), i.Name, i.Weight)))
    );

    /// <summary>The drum kits' sounds, by their General MIDI programs, the standard one every song's with no genre.</summary>
    public static ImmutableArray<(int Program, string Name)> Kits { get; } =
        [(0, "standard"), (8, "room"), (16, "power"), (24, "electronic"), (25, "tr808"), (32, "jazz"), (40, "brush"), (48, "orchestra")];

    /// <summary>A kit's name, by its program.</summary>
    public static string KitNameOf(int program) => Kits.Single(x => x.Program == program).Name;

    /// <summary>The drum kit's sound (<see cref="GeneralMidi.KitName" />).</summary>
    public static GenreChoice Kit { get; } = Pool("kit", "Drum kit", Kits.Select(x => (x.Name, GeneralMidi.KitName(x.Program), Even(x.Program == 0 ? 1 : 0))));

    /// <summary>
    ///     Every part's rhythm, a pitched part's or the drums' of a role, as values: its speed, how far off the beat it
    ///     plays, how fine it goes, how much weight its weaker beats have, how full it is and how much it repeats its
    ///     cycles (<see cref="GenreRhythm" />), and the melody's busyness.
    /// </summary>
    public static ImmutableDictionary<RhythmPart, GenreChoice> Rhythms { get; } = Enum.GetValues<RhythmPart>().ToImmutableDictionary(
        x => x,
        x => Values($"rhythm.{Name(x.ToString())}", GenreRhythm.LabelOf(x),
            [..GenreRhythm.Settings, ..x == RhythmPart.Melody ? [("busyness", "Busyness")] : Array.Empty<(string, string)>()])
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
        ..new (TrackRole Role, string Label)[]
        {
            (TrackRole.Melody, "Melody"), (TrackRole.Chords, "Chords"), (TrackRole.Bass, "Bass"), (TrackRole.Pad, "Pad"),
            (TrackRole.CounterMelody, "Counter-melody"), (TrackRole.Drum, "Drums"), (TrackRole.Riff, "Riff"), (TrackRole.RiffTwin, "Riff's twin"),
            (TrackRole.Rhythm, "Rhythm"), (TrackRole.Solo, "Soloist")
        }.Select(x => (Name(x.Role.ToString()), x.Label, SongParts.ChanceOf(x.Role)))
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
    ///     A choice of drums, every one tagged with its families, "kit" and "percussion", so that the page offers the setups
    ///     the drums chosen make.
    /// </summary>
    private static GenreChoice WithFamilies(GenreChoice choice) => choice with
    {
        Options = [..choice.Options.Select(option => option with { Tags = FamiliesOf(DrumGroups.AllDrums.Single(x => Composition.DrumPieces.NameOf(x) == option.Name)) })]
    };

    private static ImmutableArray<string> FamiliesOf(PercussionInstrumentDefinition drum) =>
        [..new[] { (DrumFamily.Kit, "kit"), (DrumFamily.Percussion, "percussion") }.Where(x => drum.Family.HasFlag(x.Item1)).Select(x => x.Item2)];

    private static string Capitalised(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>Every choice, in the order a link writes them.</summary>
    public static ImmutableArray<GenreChoice> All { get; } =
    [
        Tempo, Length, Meter, Feel, FeelChances, FeelValues, Scale, ChordShape, Arpeggio, RootMotions, ChordScales, HarmonicRhythm, Turnaround, PassingChords, Planing, HarmonyDevices, Loop, LineScale, MelodyRange, Bass, OctaveBass, Glide, BassLanding, Effects, Articulation, Levels, Rests, Voice, GrooveHold, Pins, HatRolls, Pocket, Fills, Drop, StopTime, Push, KeyChanges, Hook, Riff, Solos, Soloists, Intro, Ending, StrongEffect, Pan, FormChances, Form, SectionLength, PhraseScheme, Texture, Dynamics, Phrases,
        SectionChanges, Polymeter, SoloAnswers, LastRepeat, Doubling, InstrumentSwitches, Space, DrumJob, Marks, Register, DrumSounds, LouderForms, HarmonyInterval, Vamp,
        ..InstrumentPools.Select(x => Instruments[x.Role]),
        Kit,
        Parts, DrumSetup, DrumPieces,
        ..Enum.GetValues<RhythmPart>().Select(x => Rhythms[x]),
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

    /// <summary>A choice of values, every one's own as it is.</summary>
    private static GenreChoice Values(string name, string label, IEnumerable<(string Name, string Label)> options) =>
        new(name, label, GenreChoiceKind.Values, [..options.Select(x => new GenreOption(x.Name, x.Label, Even(0.5)))]);

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
        _ => "percussion"
    };
}
