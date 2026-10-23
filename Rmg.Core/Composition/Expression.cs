using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>How a line's note bends, as a guitar's string or a voice does.</summary>
public enum BendKind
{
    None,

    /// <summary>From a step below up into the note.</summary>
    Scoop,

    /// <summary>Up a step and back.</summary>
    BendAndRelease,

    /// <summary>From the note before, sliding into it.</summary>
    Slide,

    /// <summary>Falling away at its end.</summary>
    FallOff,

    /// <summary>Struck bent, from a step below, so that it sounds as it is, then released down that step and bent back.</summary>
    ReleaseAndBend,

    /// <summary>Struck bent up a step, sounding it, and released down onto the note.</summary>
    PrebendRelease,

    /// <summary>A whammy bar's dive, the note falling away as it rings, fast and then slowing at its bottom, an octave.</summary>
    Dive,

    /// <summary>Struck on the note and held a beat, then bent slowly up a step and held there, as a blues player lets a note hang before pushing it.</summary>
    LateBend
}

/// <summary>
///     A vibrato's width, in cents either side of the note or in scale steps up from it, the wide sort, how fast it goes, in
///     cycles a second, and whether it needs a sound that sustains to be shaken, a voice's, a bowed string's, a wind's, a
///     driven guitar's or a synth lead's, not a clean string's dying away (<see cref="InstrumentTraits.Rings" />).
/// </summary>
public readonly record struct Vibrato(int Cents, int Steps, double Rate, bool NeedsSustain);

/// <summary>The strong effects, which move a part's sound about, a song playing one of them first and others beside it now and then.</summary>
public enum StrongEffect
{
    None,
    Tremolo,
    Echo,
    Sweep,

    /// <summary>The pads, the chords and the bass ducking on every beat and swelling back, as a kick four on the floor pumps them, in a bar the kick strikes in.</summary>
    Pump,

    /// <summary>A synth's filter closing and opening again and again, in time, as a wobbling bass's.</summary>
    Wobble,

    /// <summary>A synth's filter opening on every note, ringing, and closing over it, as an acid line's squelches.</summary>
    Squelch,

    /// <summary>A synth's filter opening over the first pattern of every section the part plays, as a track builds in.</summary>
    Opening,

    /// <summary>A held chord chopped in 16ths, sounding in some and silent between, as a trance gate chops a pad.</summary>
    Gate
}

/// <summary>
///     How the parts are played, beyond their notes: a line's vibrato and bends, as pitch bend, where its instrument can
///     bend, a single line at a time; a held chord's swell and an instrument's tremolo, as its expression; a pad's sweep
///     from side to side; a synth's filter wobbling, squelching or opening; a lead's echo, the synthesizer's delay, as a
///     delay pedal plays; and every part's reverb and chorus. Decided with the song, by its sound facet: first which
///     effects the song plays at all, the gentle ones, a vibrato, a chorus, a swell and bends, each of its own, and of the
///     strong ones, a tremolo, an echo, a sweep, a pump, a filter's or a gate, one first and each other beside it now and
///     then by its share, as a trance pumps, gates and opens its filter at once, each on a part of its own, the likelier
///     one it suits, a filter's only one it suits and no part two filters, an echo's playing no vibrato or bends (<see cref="DrawSound" />);
///     then which of the gentle ones each part plays, together as they come, from a sequence of the part's; and a note's
///     bend from its section's, by the section's. The plainest songs play a reverb alone, and the wildest every gentle
///     effect on some of their parts, and a strong one at least.
/// </summary>
internal static class Expression
{
    /// <summary>The program a note plays on: its own where a section switched the part's instrument, the part's otherwise.</summary>
    private static int ProgramOf(RealizedNote note, int program) => InstrumentsOverTime.ProgramOf(note.State, program);

    /// <summary>Whether an instrument bends and sings a vibrato: guitars, strings, voices, brass, reeds, pipes, synth leads, a fretless or synth bass.</summary>
    public static bool Bends(int program) => InstrumentTraits.Has(program, InstrumentTrait.Bends);

    /// <summary>Whether an instrument sounds with a chorus the classic way: an electric piano, a clean guitar, a synth pad.</summary>
    private static bool ChorusesClassically(int program) => InstrumentTraits.Has(program, InstrumentTrait.ChorusesClassically);

    /// <summary>Whether an instrument may take a chorus: pianos, guitars, strings, leads and pads.</summary>
    private static bool Choruses(int program) => InstrumentTraits.Has(program, InstrumentTrait.Choruses);

    /// <summary>
    ///     Whether a strong effect suits a part, which plays it the likelier: an echo a lead's or a comping part's, a dub's
    ///     skank's, a tremolo a held chord's, a pad's or the chords' on a guitar, an electric piano or an organ, a sweep a pad's, a
    ///     synth's or an organ's.
    /// </summary>
    private static bool Suits(StrongEffect effect, TrackRole role, int program) => effect switch
    {
        StrongEffect.Echo => role is TrackRole.Melody or TrackRole.Rhythm,
        StrongEffect.Tremolo => role == TrackRole.Pad || role == TrackRole.Chords && InstrumentTraits.Has(program, InstrumentTrait.Tremolo),
        StrongEffect.Sweep => role == TrackRole.Pad || InstrumentTraits.Has(program, InstrumentTrait.Sweeps),
        StrongEffect.Pump => role is TrackRole.Pad or TrackRole.Chords or TrackRole.Bass or TrackRole.Rhythm,
        StrongEffect.Wobble or StrongEffect.Squelch or StrongEffect.Opening => InstrumentTraits.Has(program, InstrumentTrait.Filters),
        _ => false
    };

    /// <summary>
    ///     The parts a gate chops, held chords', and the riff a genre's, its held chord stuttering; each its
    ///     weight, above 0 where the gate suits it.
    /// </summary>
    public static ImmutableArray<(TrackRole Role, ByConvention Weight)> GateParts { get; } =
    [
        (TrackRole.Pad, new ByConvention(1, 1, 1)),
        (TrackRole.Chords, new ByConvention(1, 1, 1)),
        (TrackRole.Rhythm, new ByConvention(1, 1, 1)),
        (TrackRole.Riff, new ByConvention(0, 0, 0))
    ];

    /// <summary>Whether a strong effect is a filter's, which only a part it suits plays, a synth's.</summary>
    private static bool IsFilter(StrongEffect effect) => effect is StrongEffect.Wobble or StrongEffect.Squelch or StrongEffect.Opening;

    /// <summary>
    ///     The states a part plays steadily, not a note's gestures, which an intro's notes take from the section it is made of
    ///     and the ending's from the last.
    /// </summary>
    private static readonly IStateKind[] SteadyKinds =
    [
        CompositionStateKinds.Reverb, CompositionStateKinds.Chorus, CompositionStateKinds.Tone, CompositionStateKinds.Wow, CompositionStateKinds.Detune,
        CompositionStateKinds.AutoPan, CompositionStateKinds.Tremolo, CompositionStateKinds.Echo, CompositionStateKinds.EchoOnce, CompositionStateKinds.Filter, CompositionStateKinds.FilterCycle,
        CompositionStateKinds.FilterDepth
    ];

    /// <summary>
    ///     A note in the steady sound of another's state, its tremolo among it, the section's it copies.
    /// </summary>
    private static StateMap Steady(RealizedNote note, StateMap source) => note.State.MergeWith(source.Subset(SteadyKinds));

    /// <summary>Whether a strong effect is played only by a part it suits: a filter's, a synth's, and a gate's and a tremolo's, held chords'.</summary>
    private static bool OnlySuited(StrongEffect effect) => IsFilter(effect) || effect is StrongEffect.Gate or StrongEffect.Tremolo;

    /// <summary>Whether a part may echo: one that plays lines, or the rhythm part, whose stabs a dub echoes.</summary>
    private static bool EchoesFrom(TrackRole role) => role.PlaysLine() || role == TrackRole.Rhythm;

    /// <summary>The chances a song plays each gentle effect at all, on whichever of its parts take it.</summary>
    public static ByConvention SongVibratoChance { get; } = new(0, 0.7, 1);

    public static ByConvention SongChorusChance { get; } = new(0, 0.5, 1);

    public static ByConvention SongSwellChance { get; } = new(0, 0.35, 1);

    public static ByConvention SongBendChance { get; } = new(0, 0.5, 1);

    /// <summary>The chance a song gives some of its parts a darker tone (<see cref="ToneChance" />), no likelier in a wild song than a middling one.</summary>
    public static ByConvention SongToneChance { get; } = new(0, 0.5, 0.5);

    /// <summary>
    ///     The chance a part plays in a darker tone of its own where the song's do, a dark piano, a dull bass, a mellow
    ///     guitar, its brightness below its sound's own for the whole song, as far as its depth, drawn, takes it, and
    ///     the further the quieter its section, as a player rolls back the tone knob in a verse and opens it in the
    ///     chorus; never a drum's, whose kit shares the brightness.
    /// </summary>
    public static ByConvention ToneChance { get; } = new(0, 0.35, 0.5);

    /// <summary>
    ///     The chance a song plays as off a worn tape, its pitched parts' pitch drifting together by a few cents, slowly
    ///     and never quite in a cycle, and some of them held a few cents off the rest, a lo-fi record's warmth; no likelier
    ///     in a wild song than a middling one.
    /// </summary>
    public static ByConvention TapeChance { get; } = new(0, 0.05, 0.05);

    /// <summary>
    ///     The chance a line or the bass of a song off a tape is held off the rest (<see cref="TapeChance" />); a part holding
    ///     chords, the pad's, the chords' or the rhythm part's, never, as a held chord off the band's pitch sounds out of
    ///     tune rather than worn.
    /// </summary>
    private const double DetuneChance = 0.35;

    /// <summary>
    ///     How far a tape's pitch drifts either way at the middle of its range, in cents, by the sound facet: a plain song's
    ///     barely, a wild one's a worn cassette's; drawn from half again under it to half again over it.
    /// </summary>
    public static ByConvention Wow { get; } = new(5, 12, 26);

    /// <summary>How far a part of a song off a tape is held off the rest at most, in cents.</summary>
    private const int DetuneMost = 8;

    /// <summary>
    ///     The chance a song's driven guitars pinch now and then, a held note's harmonic squealing over it, its highlight: a
    ///     metal band's, seldom the songs'.
    /// </summary>
    public static ByConvention SongPinchChance { get; } = new(0, 0.1, 0.1);

    /// <summary>
    ///     Whether a note may be pinched, and how likely: a driven guitar's single note in a line, the riff's, the melody's,
    ///     the counter-melody's or a solo's but its release, which bends its own way, held a while by the clock and the
    ///     longest of its bar, never within a run, one a
    ///     part a phrase at most; a riff's the likelier after palm-muted chugs, its contrast, and at its phrase's end, a
    ///     solo's the more intense it is. Its sound General MIDI's guitar harmonics, always wavering, often bent.
    /// </summary>
    public const double PinchChance = 0.18;

    /// <summary>How finely a note's place keys its pinch's and its dive's draws: a 96th of a beat.</summary>
    private const int NotePlacesPerBeat = 96;

    /// <summary>The shortest note a pinch takes, in seconds, long enough to be heard, and the most notes a beat around it.</summary>
    public const double PinchShortest = 0.6, PinchBusiest = 2.5;

    /// <summary>How much louder a pinched note plays, added to its velocity, as a player digs in to make it squeal.</summary>
    public const double PinchAccent = 0.1;

    /// <summary>How much likelier a riff's pinch is after a palm-muted note, and on its phrase's last bar, as odds.</summary>
    public const double PinchAfterChugOdds = 4, PinchPhraseEndOdds = 2;

    /// <summary>How wide a pinch's vibrato is, in cents, and the chance it is bent up to the next scale note and back.</summary>
    public const int PinchVibrato = 40;

    /// <summary>How fast a pinch's vibrato goes, in cycles a second, as a squeal is shaken.</summary>
    public const double PinchRate = 6.5;

    public const double PinchBendChance = 0.5;

    /// <summary>
    ///     The chance a song's riff dives on the whammy bar into a louder section, its last held note there falling away,
    ///     a metal band's, seldom the songs' (<see cref="BendKind.Dive" />); and the chance a section it may dive into does.
    /// </summary>
    public static ByConvention SongDiveChance { get; } = new(0, 0.05, 0.05);

    public const double DiveChance = 0.5;

    /// <summary>How far a dive falls, in semitones: an octave.</summary>
    public const int DiveDepth = 12;

    /// <summary>General MIDI's guitar harmonics, which a pinch sounds on where it rings as a bell.</summary>
    internal const int GuitarHarmonics = 31;

    /// <summary>Whether a note is the longest of its bar, a held one among the part's, which a pinch takes.</summary>
    private static bool LongestOfBar(EventTimeline<RealizedNote> notes, int index, SectionSpan span)
    {
        var (start, end) = span.Bar(span.BarOf(notes[index].Position));
        return notes.Where(x => x.IsWithin(start, end)).All(x => x.Value.Duration <= notes[index].Value.Duration + 1e-9);
    }

    /// <summary>Whether a program is a driven guitar, which pinches.</summary>
    private static bool Pinches(int program) => InstrumentTraits.Has(program, InstrumentTrait.Driven);

    /// <summary>How far a part's tone closes at its deepest, in its quietest section, in the brightness controller's steps, short of a filter's reach.</summary>
    internal const int ToneReach = 16;

    /// <summary>The strong effect a song plays, if any, an echo the likeliest: none in a plain song, one in the wildest.</summary>
    public static ImmutableArray<(StrongEffect Effect, ByConvention Weight)> StrongEffects { get; } =
    [
        (StrongEffect.None, new ByConvention(1, 0.75, 0)),
        (StrongEffect.Echo, new ByConvention(0, 0.12, 1)),
        (StrongEffect.Tremolo, new ByConvention(0, 0.08, 0.8)),
        (StrongEffect.Sweep, new ByConvention(0, 0.05, 0.6)),
        // a genre's, an electronic one's, which the songs never play
        (StrongEffect.Pump, new ByConvention(0, 0, 0)),
        // a synth's filter moving, an electronic genre's, and a wild song's
        (StrongEffect.Wobble, new ByConvention(0, 0, 0.4)),
        (StrongEffect.Squelch, new ByConvention(0, 0, 0.3)),
        (StrongEffect.Opening, new ByConvention(0, 0, 0.4)),
        // a trance's, and a wild song's
        (StrongEffect.Gate, new ByConvention(0, 0, 0.3))
    ];

    /// <summary>A strong effect as a genre's page shows it.</summary>
    public static string LabelOf(StrongEffect effect) => effect switch
    {
        StrongEffect.Tremolo => "A tremolo",
        StrongEffect.Echo => "An echo",
        StrongEffect.Sweep => "A sweep",
        StrongEffect.Pump => "A pump",
        StrongEffect.Wobble => "A filter's wobble",
        StrongEffect.Squelch => "A filter's squelch",
        StrongEffect.Opening => "A filter opening",
        StrongEffect.Gate => "A trance gate",
        _ => "None"
    };

    /// <summary>How deep a pump ducks a part on the beat, in percent of its expression, swelling back over the beat.</summary>
    public const int PumpDepth = 60;

    /// <summary>
    ///     The chance a gate plays a pattern of its own, some of a bar's 16ths silent, drawn for the song, rather than every
    ///     16th, by the sound facet; and the chance each 16th sounds in one.
    /// </summary>
    public static ByConvention GatePatternChance { get; } = new(0, 0.5, 1);

    public const double GateStepChance = 0.6;

    /// <summary>How long a wobble's cycle is, in beats, as likely each: a quarter, an 8th, an 8th triplet, a 16th.</summary>
    public static ImmutableArray<double> WobbleCycles { get; } = [1, 0.5, 1 / 3.0, 0.25];

    /// <summary>
    ///     How much likelier a part the song's strong effect suits is to be the one that plays it than any other that may:
    ///     an echo goes to a line, and a sweep to any pitched part.
    /// </summary>
    public const double SuitedStrongWeight = 4;

    /// <summary>The chance a part that can sings a vibrato where the song does, on its long notes.</summary>
    public static ByConvention VibratoChance { get; } = new(0, 0.6, 0.9);

    /// <summary>
    ///     How wide a held note's vibrato is, and how fast: narrow, in cents either side of the note, as a voice or a violin
    ///     sings, the narrowest the quickest, or wide, in scale steps up from the note and back, as a guitar's does, up to
    ///     four semitones, both its ends on scale notes, a step's slow as a blues or a rock player shakes it or fast as a
    ///     metal player does, the broad, the fast and the two steps' only on a sound that sustains, a clean or an acoustic
    ///     guitar's dying note warbling under them; the wide the wilder, and only an instrument it suits plays it. Each leans
    ///     as a part's strength and its section's energy lean, the wider the more.
    /// </summary>
    public static ImmutableArray<(Vibrato Width, ByConvention Weight)> Vibratos { get; } =
    [
        (new Vibrato(15, 0, 6, false), new ByConvention(1, 0.5, 0.2)),
        (new Vibrato(35, 0, 5.5, false), new ByConvention(0, 0.35, 0.2)),
        (new Vibrato(60, 0, 5.5, true), new ByConvention(0, 0.15, 0.2)),
        (new Vibrato(0, 1, 4.5, false), new ByConvention(0, 0.01, 0.125)),
        (new Vibrato(0, 1, 6.5, true), new ByConvention(0, 0.01, 0.125)),
        (new Vibrato(0, 2, 5, true), new ByConvention(0, 0, 0.15)),
        // a whammy bar's dip, a step down and back, a surf guitar's, a genre's alone
        (new Vibrato(0, -1, 4.5, false), new ByConvention(0, 0, 0))
    ];

    /// <summary>
    ///     How long a vibrato waits after a note's attack, or after its bend lands, before it starts: a share of the time the
    ///     note's pitch holds, counted in the vibrato's cycles, so that a fast one starts sooner than a slow one and a long
    ///     note waits longer than a short one, within its instrument's bounds: a guitarist lets a note ring a while, a
    ///     synth lead's, a reed's or a brass player's likewise, where a bowed string, a voice or a flute sings it soon as
    ///     part of its tone (<see cref="InstrumentTrait.SingsVibrato" />), as a pinch's squeal is shaken at once.
    /// </summary>
    public const double VibratoWaitShare = 0.25;

    public const double FewestWaitCycles = 1, MostWaitCycles = 3, FewestSungWaitCycles = 0.5, MostSungWaitCycles = 1.5;

    /// <summary>How many of its cycles a vibrato grows in over, and how many more it must sound for at its full width to be heard, a note too short for them all singing none.</summary>
    public const double VibratoGrowthCycles = 1, VibratoHeardCycles = 2.5;

    /// <summary>A vibrato's name, as a genre names it: narrow, medium or broad by its cents, a step's slow or fast, or two steps'.</summary>
    public static string NameOf(Vibrato width) => width switch
    {
        { Steps: 2 } => "twoSteps",
        { Steps: -1 } => "dip",
        { Steps: 1, Rate: < 5.5 } => "slowStep",
        { Steps: 1 } => "fastStep",
        { Cents: <= 15 } => "narrow",
        { Cents: <= 35 } => "medium",
        _ => "broad"
    };

    public static string LabelOf(Vibrato width) =>
        $"{(width.Steps switch { 0 => $"{width.Cents} cents", 1 => "A step", -1 => "A whammy's dip, a step down", _ => $"{width.Steps} steps" })} at {width.Rate} a second";

    /// <summary>How far a width leans to the wide side, for a strength or an energy to lean it.</summary>
    private static double Lean(Vibrato width) => width.Steps != 0 ? 0.5 * Math.Abs(width.Steps) : width.Cents / 60.0 - 1;

    /// <summary>The narrow vibrato a note plays in place of a wide one it cannot, a note off the scale or an instrument it does not suit: broad on a sound that sustains, medium on one that rings.</summary>
    private static Vibrato NarrowFor(int program) => Vibratos.Select(x => x.Width).Last(x => x.Steps == 0 && Suits(x, program));

    /// <summary>Whether a width suits an instrument: a wide one an electric guitar's or a synth lead's, and one needing a sound that sustains any but one that rings.</summary>
    public static bool Suits(Vibrato width, int program) =>
        (width.Steps == 0 || InstrumentTraits.Has(program, InstrumentTrait.VibratesWide)) && (!width.NeedsSustain || !InstrumentTraits.Rings(program));

    /// <summary>
    ///     The chance a part slides from note to note at all, never in a plain song and not every part in a wild one, and
    ///     how often one that does slides into a note it may, its own rate spread about this.
    /// </summary>
    public static ByConvention SlideChance { get; } = new(0, 0.25, 0.6);

    public static ByConvention SlideRate { get; } = new(0, 0.1, 0.3);

    /// <summary>The chance a song's bass glides from note to note, a genre's that the songs never play, and the share of its notes it slides into (<see cref="GenreChoices.Glide" />).</summary>
    public static ByConvention GlideChance { get; } = new(0, 0, 0);

    /// <summary>The share of its notes a gliding part slides into, half, a genre's moving it to none or every one it may (<see cref="GenreChoices.GlideRate" />).</summary>
    public const double GlideRate = 0.5;

    /// <summary>How far after the note before, in beats, a gliding bass still slides from it, and the shortest note it slides into.</summary>
    public const double GlideGap = 1;

    public const double GlideShortest = 0.25;

    /// <summary>
    ///     How much likelier a gliding part slides into a note for each thing that draws a slide, the odds' factor: a leap
    ///     of <see cref="GlideLeap" /> semitones or more, and the phrase's last note, as a whining lead slides into where it lands.
    /// </summary>
    public const double GlideOdds = 3;

    public const int GlideLeap = 5;

    /// <summary>
    ///     How long a bend takes to rise, in seconds, as a player bends quickly to land it: a soloist's from the slowest at
    ///     the least intense to the quickest at the most, a part's line at the middle of them; a slow bend, as a blues
    ///     player's crying one or a country player's in time, takes an 8th, or the quick one where an 8th is quicker.
    /// </summary>
    public const double SlowestQuickRise = 0.12, QuickestRise = 0.07, PartRise = 0.1;

    /// <summary>The chance a bend or a slide goes slowly, in time, by the sound facet: some at the plain end, as slow bends are a convention of their own.</summary>
    public static ByConvention SlowBendChance { get; } = new(0.1, 0.15, 0.25);

    /// <summary>
    ///     The chance a bend up a step, on a note long enough, waits a beat before it rises, slowly, and stays bent, by the sound
    ///     facet: a blues player's, which the songs play seldom and a genre's where it names it (<see cref="GenreChoices.BendTiming" />).
    /// </summary>
    public static ByConvention LateBendChance { get; } = new(0, 0.03, 0.05);

    /// <summary>The place a note's draw of whether its bend waits is derived from its draws' by, a sequence of its own.</summary>
    private const long LateBendDecision = 1;

    /// <summary>How long a fall-off takes to fall, in seconds, at the very end of its note.</summary>
    public const double FallSeconds = 0.15;

    /// <summary>
    ///     When a shaped note's pitch is held, in seconds from its start: from where its bend lands, onto the note or its
    ///     bent top, to where it moves again, a release's or a fall's start, or the note's end. A prebend holds its bent
    ///     note for its rise before it releases, slowly, over an 8th, or its rise where that is longer; a late bend lands a
    ///     beat and its rise after the note's start.
    /// </summary>
    /// <param name="kind">How the note bends.</param>
    /// <param name="rise">How long its bend takes to rise, in seconds.</param>
    /// <param name="seconds">How long the note lasts, in seconds.</param>
    /// <param name="eighth">How long an 8th lasts there, in seconds.</param>
    public static (double From, double To) Held(BendKind kind, double rise, double seconds, double eighth) => kind switch
    {
        BendKind.Scoop or BendKind.Slide => (rise, seconds),
        BendKind.BendAndRelease or BendKind.ReleaseAndBend => (rise, seconds - rise),
        BendKind.PrebendRelease => (rise + Math.Max(eighth, rise), seconds),
        BendKind.LateBend => (2 * eighth + rise, seconds),
        BendKind.FallOff => (0, seconds - FallSeconds),
        _ => (0, seconds)
    };

    /// <summary>Whether a note is long enough for its bend: held, between its moves, at least as long as it moves.</summary>
    public static bool Fits(BendKind kind, double rise, double seconds, double eighth) =>
        Held(kind, rise, seconds, eighth) is var (from, to) && to - from >= (kind == BendKind.FallOff ? FallSeconds : rise) - 1e-9;

    /// <summary>
    ///     How many of its cycles a vibrato of a rate waits where a note's pitch holds, sung soon or let wait
    ///     (<see cref="VibratoWaitShare" />), but never so long that it leaves less than its growth and its heard cycles.
    /// </summary>
    public static double VibratoWait(BendKind kind, double rise, double seconds, double eighth, double rate, bool sings) =>
        Held(kind, rise, seconds, eighth) is var (from, to) && (to - from) * rate is var cycles
            ? Math.Clamp(Math.Min(VibratoWaitShare * cycles, cycles - VibratoGrowthCycles - VibratoHeardCycles), sings ? FewestSungWaitCycles : FewestWaitCycles,
                sings ? MostSungWaitCycles : MostWaitCycles)
            : 0;

    /// <summary>Whether a note holds long enough for a vibrato of a rate, its wait, its growth and its heard cycles, where its pitch is held.</summary>
    public static bool HoldsVibrato(BendKind kind, double rise, double seconds, double eighth, double rate, bool sings) =>
        Held(kind, rise, seconds, eighth) is var (from, to)
        && (to - from) * rate >= VibratoWait(kind, rise, seconds, eighth, rate, sings) + VibratoGrowthCycles + VibratoHeardCycles - 1e-9;

    /// <summary>How soon before a note the one before it must have ended for the note to slide from it, in beats: a slide starts from a note heard.</summary>
    public const double SlideGap = 0.25;

    /// <summary>The chance a line that can bends where the song does, and then a note's chance to, three times it in a solo.</summary>
    public static ByConvention PartBendChance { get; } = new(0, 0.6, 0.9);

    public static ByConvention BendChance { get; } = new(0, 0.03, 0.5);

    /// <summary>
    ///     A bend's size, in scale steps, so that it starts and ends on scale notes: a step the plainest, two the wilder, at
    ///     most four semitones, beyond which it would be a slide.
    /// </summary>
    public static ImmutableArray<(int Steps, ByConvention Weight)> BendSizes { get; } =
    [
        (1, new ByConvention(1, 0.85, 0.5)),
        (2, new ByConvention(0, 0.15, 0.5))
    ];

    /// <summary>
    ///     How far a part's strength, its lean to the bigger sizes, strays from the middle, by the song's sound facet: parts
    ///     alike in a plain song, and some bending far and some barely in a wild one.
    /// </summary>
    public static ByConvention StrengthSpread { get; } = new(0, 0.5, 1);

    /// <summary>The odds of the bigger size at a strength of 1, and how far a section's energy leans the sizes the same way.</summary>
    public const double StrengthOdds = 8;

    public const double SizeCoupling = 0.3;

    /// <summary>
    ///     A release's bend, the note a solo's phrase ends on: into it, from below or from a prebent step above, never away
    ///     from it, by a chance of its own, the likelier the more intense.
    /// </summary>
    public static ImmutableArray<Weighted<BendKind>> ReleaseBends { get; } = [new(0.6, BendKind.Scoop), new(0.4, BendKind.PrebendRelease)];

    public const double ReleaseBendChance = 0.4;

    /// <summary>How far a solo's intensity leans its notes, as a strength does: the bigger bends and vibratos, and the oftener bends and slides, the more intense.</summary>
    public const double SoloOdds = 8;

    /// <summary>
    ///     How a line's note bends, and how often: the plain end's mostly a scoop into the note; a fall-off on a phrase's
    ///     last note alone and a prebend on a note of a beat or more (<see cref="Placed" />).
    /// </summary>
    public static ImmutableArray<(BendKind Kind, ByConvention Weight)> BendKinds { get; } =
    [
        (BendKind.Scoop, new ByConvention(1, 0.4, 1)),
        (BendKind.FallOff, new ByConvention(0.25, 0.25, 1)),
        (BendKind.BendAndRelease, new ByConvention(0.5, 0.15, 1)),
        (BendKind.ReleaseAndBend, new ByConvention(0, 0.06, 1)),
        (BendKind.PrebendRelease, new ByConvention(0, 0.08, 1))
    ];

    /// <summary>The bends a note may take where it plays: a fall-off on its phrase's last note alone, a prebend on a note of a beat or more, held bent and let down.</summary>
    private static ImmutableArray<Weighted<BendKind>> Placed(IEnumerable<Weighted<BendKind>> kinds, bool ends, double beats) =>
        [..kinds.Where(x => (x.Value != BendKind.FallOff || ends) && (x.Value != BendKind.PrebendRelease || beats >= 1 - 1e-9))];

    /// <summary>How much likelier a bend up goes to a note of its chord than to another scale note, as odds.</summary>
    public const double ChordTopOdds = 4;

    /// <summary>The chance a part's held notes swell in where the song's do, a pad's likely, any other's now and then.</summary>
    public static ByConvention PadSwellChance { get; } = new(0, 0.5, 0.9);

    public static ByConvention SwellChance { get; } = new(0, 0.05, 0.3);

    /// <summary>The chance a melody's or a counter-melody's phrases swell into their held last notes where the song's parts swell, by the sound facet.</summary>
    public static ByConvention LineSwellChance { get; } = new(0, 0.3, 0.8);

    /// <summary>How long a line's held last note is at least, in beats, to swell into.</summary>
    public const double LineSwellBeats = 2;

    /// <summary>The widest a bend goes, in semitones: a step and a half or two whole steps, never a fourth.</summary>
    public const int WidestBend = 4;

    /// <summary>
    ///     A bend's semitones to the scale note the given steps away, one step where two would bend past
    ///     <see cref="WidestBend" />, as over an altered step's gap of three semitones beside a whole step.
    /// </summary>
    internal static int BendDistance(RealizedNote note, int steps) =>
        ScaleDistance(note, steps) is var distance && Math.Abs(distance) > WidestBend && Math.Abs(steps) > 1 ? ScaleDistance(note, Math.Sign(steps)) : distance;

    /// <summary>
    ///     The semitones from a note to the scale note the given scale steps above it, or below for fewer than none, by
    ///     the scale at the note, as its line was placed in: a note off the scale counts the nearest scale note beyond it as
    ///     its first step.
    /// </summary>
    internal static int ScaleDistance(RealizedNote note, int steps)
    {
        var (offsets, _, chord) = Realizer.GetChord(note.State);
        var classes = Enumerable.Range(0, offsets.Length).Select(i => chord.GetPitch(i).Mod(Realizer.OctaveNoteCount)).ToHashSet();
        var (direction, found, distance) = (Math.Sign(steps), 0, 0);
        while (found < Math.Abs(steps))
        {
            distance += direction;
            if (classes.Contains((note.Pitches[0] + distance).Mod(Realizer.OctaveNoteCount)))
                found++;
        }

        return distance;
    }

    /// <summary>Whether a note is on the scale at it, as its line placed it, rather than a chromatic step.</summary>
    internal static bool IsOnScale(RealizedNote note)
    {
        var (offsets, _, chord) = Realizer.GetChord(note.State);
        return Enumerable.Range(0, offsets.Length).Any(i => chord.GetPitch(i).Mod(Realizer.OctaveNoteCount) == note.Pitches[0].Mod(Realizer.OctaveNoteCount));
    }

    /// <summary>
    ///     How an echo repeats, its delay in beats, whether it repeats once, almost as loud, rather than dying away over
    ///     several, and how likely each is: a dotted 8th, a quarter or an 8th as likely as each other, and the cascade, a
    ///     dotted 8th once, whose repeats fill an 8ths' broken chords into a run of 16ths, a genre's alone
    ///     (<see cref="GenreChoices.Echo" />).
    /// </summary>
    public static ImmutableArray<(EchoKind Kind, double Delay, bool Once, ByConvention Weight)> EchoKinds { get; } =
    [
        (EchoKind.DottedEighth, 0.75, false, new ByConvention(1, 1, 1)),
        (EchoKind.Quarter, 1, false, new ByConvention(1, 1, 1)),
        (EchoKind.Eighth, 0.5, false, new ByConvention(1, 1, 1)),
        (EchoKind.Cascade, 0.75, true, new ByConvention(0, 0, 0))
    ];

    /// <summary>An echo's kind as a genre's page shows it.</summary>
    public static string LabelOf(EchoKind kind) => kind switch
    {
        EchoKind.DottedEighth => "A dotted 8th",
        EchoKind.Quarter => "A quarter",
        EchoKind.Eighth => "An 8th",
        EchoKind.Cascade => "A dotted 8th once, a cascade",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    /// <summary>A part's reverb, from 0 to 127, as a mix sets it by role, which a part strays from once for the song, by the sound facet (<see cref="ReverbSpread" />).</summary>
    public static int Reverb(TrackRole role) => role switch
    {
        TrackRole.Drum => 40,
        TrackRole.Bass => 25,
        TrackRole.Pad => 80,
        TrackRole.Chords => 55,
        TrackRole.Riff or TrackRole.RiffTwin => 45,
        TrackRole.Rhythm => 50,
        _ => 60
    };

    /// <summary>
    ///     A part's reverb as a genre has it (<see cref="GenreChoices.Space" />): the role's moved towards none or the most by
    ///     the genre's lean, a dub's wet and a funk band's dry; the role's where the genre does not name it. A twin's its
    ///     riff's.
    /// </summary>
    public static double Reverb(TrackRole role, Genre genre)
    {
        var lean = genre.Shift(GenreChoices.Space, GenreChoices.Name(role.Source().ToString()));
        return Reverb(role) + lean * (lean > 0 ? 127 - Reverb(role) : Reverb(role));
    }

    public static ByConvention ReverbSpread { get; } = new(0, 0.1, 1);

    /// <summary>The chance a part plays with a chorus where the song does, the classic ones the likelier.</summary>
    public static ByConvention ClassicChorusChance { get; } = new(0, 0.6, 0.9);

    public static ByConvention ChorusChance { get; } = new(0, 0.2, 0.5);

    /// <summary>
    ///     The effects the song plays at all, as a genre has them where it names them, and the one part that plays its strong
    ///     one, the likelier one it suits, of the parts the song plays, if any may: decided before the notes, so that a stage
    ///     before them knows, as the gate's held chords do.
    /// </summary>
    /// <param name="songStream">The song's sequence, which the effects are drawn from.</param>
    /// <param name="songSound">The song's sound facet, which they lean by.</param>
    /// <param name="playing">The parts the song plays, by their tracks.</param>
    /// <param name="others">A strong effect's sequence of its own, which an effect beside the first is drawn from.</param>
    public static SongSound DrawSound(IGenerationContext songStream, double songSound, Genre genre, ImmutableSortedDictionary<int, IInstrumentTrack> definitions, IReadOnlySet<int> playing,
        Func<StrongEffect, IGenerationContext> others)
    {
        double Chance(ByConvention own, string option) => own.At(songSound, genre.Chance(GenreChoices.Effects, option));
        var songVibrato = songStream.TestProbability(Chance(SongVibratoChance, "vibrato"));
        var songChorus = songStream.TestProbability(Chance(SongChorusChance, "chorus"));
        var songSwell = songStream.TestProbability(Chance(SongSwellChance, "swell"));
        var songBends = songStream.TestProbability(Chance(SongBendChance, "bends"));
        var weights = ByConvention.Weigh(StrongEffects, songSound, genre.Pool<StrongEffect>(GenreChoices.StrongEffect));
        var strong = songStream.Pick(weights);

        // the parts that may play an effect, the likelier one it suits, but one that plays another filter's already; a gate
        // the parts the genre has it chop
        var gates = ByConvention.Weigh(GateParts, songSound, genre.Pool<TrackRole>(GenreChoices.GateParts))
            .Where(x => x.Weight > 0).Select(x => x.Value).ToHashSet();
        bool Fits(StrongEffect effect, TrackRole role, int program) => effect == StrongEffect.Gate ? gates.Contains(role) : Suits(effect, role, program);
        var carriers = ImmutableDictionary.CreateBuilder<StrongEffect, int>();
        ImmutableArray<Weighted<int>> MayCarry(StrongEffect effect) =>
        [
            ..definitions
                // a riff's twin carrying what its riff does, never alone
                .Where(x => playing.Contains(x.Key) && x.Value is PitchInstrumentTrack && x.Value.Role != TrackRole.RiffTwin && (effect != StrongEffect.Echo || EchoesFrom(x.Value.Role))
                            && (!OnlySuited(effect) || Fits(effect, x.Value.Role, ((PitchInstrumentTrack)x.Value).InstrumentCode))
                            && !(IsFilter(effect) && carriers.Any(c => IsFilter(c.Key) && c.Value == x.Key)))
                .Select(x => new Weighted<int>(Fits(effect, x.Value.Role, ((PitchInstrumentTrack)x.Value).InstrumentCode) ? SuitedStrongWeight : 1, x.Key))
        ];
        // none where no part may carry it
        var first = MayCarry(strong);
        if (strong != StrongEffect.None && !first.IsEmpty)
            carriers[strong] = songStream.Pick(first);
        else
            strong = StrongEffect.None;
        // and each other effect beside it now and then, by its share of the effects times the song's chance of any, from a
        // sequence of its own, as a trance pumps, gates and opens its filter at once and a song that seldom plays one
        // seldom plays two
        var total = weights.Sum(x => x.Weight);
        var any = total > 0 ? 1 - weights.Where(x => x.Value == StrongEffect.None).Sum(x => x.Weight) / total : 0;
        foreach (var (effect, weight) in weights.Where(x => x.Value != StrongEffect.None && x.Value != strong).Select(x => (x.Value, x.Weight)))
        {
            // drawn whatever the first, so that the draws are as many, and played beside one alone
            var stream = others(effect);
            if (!stream.TestProbability(total > 0 ? any * weight / total : 0) || strong == StrongEffect.None)
                continue;
            var may = MayCarry(effect);
            if (!may.IsEmpty)
                carriers[effect] = stream.Pick(may);
        }

        var effects = new SongEffects(songVibrato, songChorus, songSwell, songBends, strong, [..carriers.Keys.Where(x => x != strong).Order()]);
        StateTrace.RecordSong(TracePoints.SongEffects, string.Join(", ", carriers.Keys.Prepend(strong).Distinct()), effects);
        return new SongSound(effects, carriers.ToImmutable());
    }

    /// <summary>
    ///     The song's notes with every part's expression on them (<see cref="CompositionStateKinds.Vibrato" /> and the
    ///     rest), each part in a section drawing from a sequence of its own, by the section, so that a section plays as it
    ///     did before.
    /// </summary>
    /// <param name="songSound">The song's sound facet, which a part's effects lean by.</param>
    /// <param name="songEffects">The effects the song plays at all, and the part that plays its strong one, decided before its notes (<see cref="DrawSound" />).</param>
    /// <param name="sound">Every section's sound facet, by its place among the song's sections.</param>
    /// <param name="energies">Every section's energy, by its place, which leans its bends' sizes.</param>
    /// <param name="soloists">Every part's soloist, whose leans its solos' notes play by, as intense as the solo is there.</param>
    /// <param name="partStreams">A part's sequence for the whole song, by its track.</param>
    /// <param name="streams">A part's sequence in a section, by the section and the part's track.</param>
    /// <param name="toneStream">The song's sequence of its parts' tones, whether the song plays them, and each part's derived from it by its track.</param>
    /// <param name="pinchStream">The song's sequence of whether its guitars pinch and dive.</param>
    /// <param name="pinchStreams">A part's sequence of its pinches, by its track.</param>
    /// <param name="beatSeconds">How long a beat lasts at a position, in seconds, which a pinched note's length is held to, and a bend's and a vibrato's are timed in.</param>
    /// <param name="tapeStream">The song's sequence of its tape's wow, whether it plays one, how deep, and each part's detune derived from it by its track.</param>
    /// <param name="genre">The genre, whose chances of the gentle effects and pool of the strong ones are the song's where it names them.</param>
    /// <param name="changes">Where the chords change, in order, which a gated part's held chords change at.</param>
    /// <param name="cuts">Where a part's notes end as the band stops, by its track, which a gated part's held chords end by.</param>
    /// <param name="holds">What the song holds: its stop time, whose hits a gated part plays as they are and the riff's dive keeps out of.</param>
    /// <param name="drawsAs">The track a part draws as, the riff's for its twin, whose bends, vibrato, pinches, tone and dive are the riff's as it plays the riff's line.</param>
    public static ImmutableSortedDictionary<int, EventTimeline<RealizedNote>> Apply(
        ImmutableSortedDictionary<int, EventTimeline<RealizedNote>> notes,
        ImmutableSortedDictionary<int, IInstrumentTrack> definitions,
        SongMap map,
        double songSound,
        SongSound songEffects,
        IReadOnlyList<double> sound,
        IReadOnlyList<double> energies,
        IReadOnlyDictionary<TrackRole, Soloist> soloists,
        Func<int, IGenerationContext> partStreams,
        Func<int, int, IGenerationContext> streams,
        IGenerationContext toneStream,
        IGenerationContext tapeStream,
        IGenerationContext pinchStream,
        Func<int, IGenerationContext> pinchStreams,
        Func<double, double> beatSeconds,
        Genre genre,
        IReadOnlyList<double> changes,
        Func<int, IReadOnlyList<double>> cuts,
        Holds holds,
        SectionSpan introSource,
        Func<int, int> drawsAs
    )
    {
        var ((songVibrato, songChorus, songSwell, songBends, _, _), carriers) = songEffects;
        // a gated part holding its chords, which the gate chops in time, where it plays at all, a riff's twin with it
        if (carriers.TryGetValue(StrongEffect.Gate, out var gateCarrier))
            foreach (var gated in notes.Keys.Where(x => drawsAs(x) == gateCarrier).ToArray())
                notes = notes.SetItem(gated, GatedChords.Hold(notes[gated], map, changes, cuts(gated), [..holds.Of(Hold.Stop).Select(x => (x.Start, x.End))],
                    note => InstrumentTraits.Has(ProgramOf(note, ((PitchInstrumentTrack)definitions[gated]).InstrumentCode), InstrumentTrait.Decays)));

        // the snares, which echo with a comping part's echo, at its delay, the drums' tracks coming after the pitched parts'
        var backbeat = DrumGroups.Snare.Tracks.ToHashSet();
        (double Delay, bool Once)? carrierEcho = null;
        // whether the song gives its parts tones at all, the genre's where it names it
        var songTone = toneStream.TestProbability(SongToneChance.At(songSound, genre.Chance(GenreChoices.Effects, "tone")));
        // and whether it plays off a tape, and how far its pitch drifts
        var tape = tapeStream.TestProbability(TapeChance.At(songSound, genre.Chance(GenreChoices.Effects, "tape")));
        var wow = (int)Math.Round(Wow.At(songSound) * (0.5 + tapeStream.GenerateDouble()));
        // each part's tone and detune derived from these by its track, so that whether another part plays leaves them
        var (toneSeed, tapeSeed) = (toneStream.GenerateSeed(), tapeStream.GenerateSeed());
        // and whether its riff dives into a louder section
        var songDives = pinchStream.TestProbability(SongDiveChance.At(songSound, genre.Chance(GenreChoices.Effects, "dive")));
        // and whether its guitars pinch, on General MIDI's guitar harmonics, which listening preferred to the driven guitar's squeal
        var songPinches = pinchStream.TestProbability(SongPinchChance.At(songSound, genre.Chance(GenreChoices.Effects, "pinch")));

        // the bars a kick strikes in, which a pump ducks the band in, by where each starts, so that a pad alone or the
        // band where the drums rest or the kick is out swells free
        double BarStart(double position) => Math.Round(position - map.PlaceOf(position).Beat, 6);
        var kicked = DrumGroups.Kick.Tracks.Where(notes.ContainsKey).SelectMany(x => notes[x]).Select(x => BarStart(x.Position)).ToHashSet();

        var result = notes.ToBuilder();
        foreach (var (track, timeline) in notes)
        {
            var definition = definitions[track];
            var role = definition.Role;
            var program = definition is PitchInstrumentTrack pitched ? pitched.InstrumentCode : -1;
            var states = new StateMap?[timeline.Count];
            // a pinched note's accent
            var accents = new double[timeline.Count];
            // a dived note's length, held to the section it dives into, and the notes it lets go of after it
            var held = new double?[timeline.Count];
            var dropped = new bool[timeline.Count];
            var trackCuts = cuts(track);
            // the first note at or after a position
            int first(double position)
            {
                var (low, high) = (0, timeline.Count);
                while (low < high)
                {
                    var middle = (low + high) / 2;
                    if (timeline[middle].Position < position - 1e-9)
                        low = middle + 1;
                    else
                        high = middle;
                }

                return low;
            }

            // the part's sound for the whole song, of the effects the song plays: a part makes every draw whatever the song
            // plays, so that what one effect decides leaves the others' draws as they are
            var part = partStreams(track);
            var vibrates = part.TestProbability(VibratoChance.At(songSound)) & songVibrato;
            var isSung = role is TrackRole.Melody or TrackRole.CounterMelody;
            var swells = part.TestProbability((role == TrackRole.Pad ? PadSwellChance : isSung ? LineSwellChance : SwellChance).At(songSound)) & songSwell;
            var bendsHere = part.TestProbability(PartBendChance.At(songSound)) & songBends;
            // a riff's twin as its riff
            bool Carries(StrongEffect effect) => carriers.TryGetValue(effect, out var carrier) && carrier == drawsAs(track);
            var tremoloDepth = 30 + part.GenerateInt(0, 40);
            var sweepDepth = 40 + part.GenerateInt(0, 60);
            var tremolo = Carries(StrongEffect.Tremolo) ? tremoloDepth : 0;
            var sweeps = Carries(StrongEffect.Sweep) ? sweepDepth : 0;
            var echoes = Carries(StrongEffect.Echo);
            // a pump every part's it suits, not one's, as a kick ducks the whole of a mix under it
            var pumps = carriers.ContainsKey(StrongEffect.Pump) && Suits(StrongEffect.Pump, role, program);
            // an echo repeats the notes as they are played, so a bent or wavering one would smear: the part that echoes plays
            // them straight
            vibrates &= !echoes;
            bendsHere &= !echoes;
            var echoKinds = ByConvention.Weigh(EchoKinds.Select(x => (x, x.Weight)), songSound, genre.Pool<EchoKind>(GenreChoices.Echo) is { } pool ? x => pool(x.Kind) : null);
            var (_, delay, once, _) = echoKinds[Generators.WeightedIndex(echoKinds)(part)].Value;
            if (echoes)
                carrierEcho = (delay, once);
            var room = Reverb(role, genre);
            var reverb = (int)Math.Round(room + ReverbSpread.At(songSound) * (part.GenerateDouble() * 127 - room));
            var choruses = part.TestProbability((ChorusesClassically(program) ? ClassicChorusChance : ChorusChance).At(songSound)) & songChorus;
            var chorus = 40 + part.GenerateInt(0, 60);
            // how much the part leans to the bigger sizes, around 0
            var strength = StrengthSpread.At(songSound) * Generators.SplineValue()(part);
            // and whether it slides from note to note, and how often, but where it echoes, playing straight
            var slides = part.TestProbability(SlideChance.At(songSound)) & !echoes;
            var slideRate = SlideRate.At(songSound) * 2 * part.GenerateDouble();
            // a bass that glides, a genre's, into half the notes it may from the note before, as an 808's or an acid line's
            // does, on an instrument that bends; drawn whatever the genre, so that the part's other draws stay
            // and a lead that glides, a G-funk's or a trance's whining line, its own chance of the same draw, the share of
            // notes either slides into a genre's (GenreChoices.GlideRate)
            var glideOption = role switch { TrackRole.Bass => "bass", TrackRole.Melody => "lead", _ => null };
            var glides = part.TestProbability(glideOption is null ? 0 : GlideChance.At(songSound, genre.Chance(GenreChoices.Glide, glideOption))) && glideOption is not null && !echoes && Bends(program);
            if (glides)
                (slides, slideRate) = (true, GlideRate * (1 + genre.Shift(GenreChoices.GlideRate, "share")));
            // and from across a gap, into a short note too
            var (slideGap, shortestSlide) = glides ? (GlideGap, GlideShortest) : (SlideGap, 0.0);
            // and how far its filter closes, and a wobble's cycle, drawn last, so that the part's other draws stay
            var filterDepth = 40 + part.GenerateInt(0, 60);
            var wobbleCycle = WobbleCycles[part.GenerateInt(0, WobbleCycles.Length)];
            // the filter this part plays, one at most
            var filter = carriers.Where(x => IsFilter(x.Key) && x.Value == drawsAs(track)).Select(x => x.Key).FirstOrDefault();
            // and a gate's 16ths, every one or a pattern's, its first always sounding, drawn whatever the song plays
            var patterned = part.TestProbability(GatePatternChance.At(songSound));
            var steps = Enumerable.Range(0, 16).Select(_ => part.TestProbability(GateStepChance)).ToArray();
            var gate = Carries(StrongEffect.Gate) ? steps.Select((x, i) => i == 0 || !patterned || x ? 1 << i : 0).Sum() : 0;
            // and its tone, from a sequence of its own
            var partTone = new GenerationContext(Seeds.Derive(toneSeed, drawsAs(track)));
            var toned = partTone.TestProbability(ToneChance.At(songSound)) & songTone && role != TrackRole.Drum;
            var toneDepth = 0.3 + 0.7 * partTone.GenerateDouble();
            // and its detune, either way, from a sequence of its own; a drum's kit and a part holding chords left in tune
            var partTape = new GenerationContext(Seeds.Derive(tapeSeed, drawsAs(track)));
            var detuned = partTape.TestProbability(DetuneChance) && tape && role is not (TrackRole.Drum or TrackRole.Pad) && !role.PlaysChords();
            var detune = (partTape.TestProbability(0.5) ? 1 : -1) * partTape.GenerateInt(2, DetuneMost + 1);
            // and its pinches and its dive, from a sequence of its own, each note's by its place, so that a note let pass
            // leaves the others' draws as they were, one a phrase at most
            var pinchSeed = pinchStreams(track).GenerateSeed();
            IGenerationContext Pinching(double position, int decision) =>
                new GenerationContext(Seeds.Derive(Seeds.Derive(pinchSeed, map.PlaceKey(position, NotePlacesPerBeat)), decision));
            var pinchedPhrases = new HashSet<(int, int)>();
            // the widths the genre's vibratos take
            var widths = genre.Pool<Vibrato>(GenreChoices.VibratoWidth, NameOf);
            foreach (var (span, index) in map.Sections.Select((x, i) => (x, i)))
            {
                var context = streams(span.SectionId, track);
                var u = sound[index];
                var bendChance = BendChance.At(u);
                var bendKinds = ByConvention.Weigh(BendKinds, u);
                // the part's strength and the section's energy, as one lean to the bigger sizes and the wider vibrato, the wide
                // only where its instrument suits it
                var lean = new Tilt(Tilt.Of(StrengthOdds, strength).LogOdds + SectionEnergy.Tilt(energies[index], SizeCoupling).LogOdds);
                var sizes = lean.Weigh(ByConvention.Weigh(BendSizes, u), steps => steps > 1 ? 1 : -1);
                var vibrato = context.Pick(lean.Weigh(ByConvention.Weigh(Vibratos.Where(x => Suits(x.Width, program)), u, widths), Lean));
                // the part's tone here, darker the quieter the section, as dark as it goes at an energy of -1 and below, half as
                // dark at 1 and above
                var tone = toned ? (int)Math.Round(ToneReach * toneDepth * (3 - Math.Clamp(energies[index], -1, 1)) / 4) : 0;
                // every note's draws by its place, so that a note a stage before took out leaves the others' as they were
                var noteSeed = context.GenerateSeed();
                // a tremolo the whole section's where the part holds every note there a beat or longer, as a held chord trembles,
                // and none where it plays anything faster, a riff's or a run's, so that it never turns on and off within it
                var trembles = tremolo > 0 && Enumerable.Range(first(span.Start), timeline.Count - first(span.Start)).TakeWhile(i => timeline[i].Position < span.End - 1e-9)
                    .Where(i => !dropped[i]).All(i => timeline[i].Value.Duration >= 1 - 1e-9);

                // the notes in the section, in order, the timeline's being
                for (var i = first(span.Start); i < timeline.Count && timeline[i].Position < span.End - 1e-9; i++)
                {
                    // a note the riff let go of for a dive plays no more
                    if (dropped[i])
                        continue;
                    var note = timeline[i];

                    var drawing = new GenerationContext(Seeds.Derive(noteSeed, map.PlaceKey(note.Position, NotePlacesPerBeat)));
                    var noteProgram = ProgramOf(note.Value, program);
                    // one note at a time, or in octaves, which bend and echo together
                    var isLine = (note.Value.Pitches.Length == 1 || note.Value.State.GetStateValue(CompositionStateKinds.Octave) > 0)
                                 && (role.PlaysLine() || note.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 1);
                    var isSolo = note.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 1;
                    // a solo's note plays as its soloist leans, its strength in place of the part's, and the more intense the
                    // solo there, the bigger and the oftener; whether it bends at all is the soloist's choice, not the song's,
                    // which is the band's, but where it echoes; and a twin lead's as the riff's soloist, its twin's alike, as it
                    // draws as the riff does, at a middling intensity
                    var leads = note.Value.State.GetStateValue(CompositionStateKinds.TwinLead) == 1;
                    var soloist = isSolo ? soloists.GetValueOrDefault(role) : leads ? soloists[TrackRole.Riff] : null;
                    var bends = (soloist is null ? bendsHere : soloist.Bends && !echoes) && isLine && Bends(noteProgram);
                    var intensity = leads ? new Tilt(0) : Tilt.Of(SoloOdds, 2 * note.Value.State.GetStateValue(CompositionStateKinds.SoloIntensity) / 100.0 - 1);
                    var soloLean = soloist is null ? lean : new Tilt(Tilt.Of(StrengthOdds, soloist.BendLean).LogOdds + SectionEnergy.Tilt(energies[index], SizeCoupling).LogOdds + intensity.LogOdds);
                    // every note draws whether it bends, and how, so that the draws stay as they are
                    // a release bends into it now and then, the likelier the more intense, and slides into it never
                    var isRelease = note.Value.State.GetStateValue(CompositionStateKinds.SoloRelease) == 1;
                    // a soloist's note bends as often as it bends a note, leaned by its lean to bends as well as by the intensity
                    if (isSolo && soloist is null)
                        throw new InvalidOperationException($"Track {track} solos with no soloist.");
                    var bendsNow = drawing.TestProbability(soloist is null ? bendChance
                        : isRelease ? intensity.Chance(ReleaseBendChance, 1)
                        : new Tilt(intensity.LogOdds + Tilt.Of(StrengthOdds, soloist.BendLean - Soloist.ExpressionLean).LogOdds).Chance(soloist.BendRate, 1));
                    // whether it ends its phrase, the line resting a beat after it or starting a new phrase
                    var following = i + 1 < timeline.Count && timeline[i + 1].Position < span.End - 1e-9 ? timeline[i + 1] : (TimelineItem<RealizedNote>?)null;
                    var ends = following is not { } after || after.Position >= note.Position + note.Value.Duration + 1 - 1e-9 || after.Value.State.GetStateValue(CompositionStateKinds.LinePhraseStart) > 0;
                    var kind = drawing.Pick(Placed(bendKinds, ends, note.Value.Duration));
                    if (isRelease)
                        kind = drawing.Pick(Placed(ReleaseBends, ends, note.Value.Duration));
                    var eighth = 0.5 * beatSeconds(note.Position);
                    var quick = soloist is null ? PartRise : SlowestQuickRise - (SlowestQuickRise - QuickestRise) * note.Value.State.GetStateValue(CompositionStateKinds.SoloIntensity) / 100.0;
                    var seconds = note.Value.Duration * beatSeconds(note.Position);
                    // a held note's bend made to wait a beat and rise slowly up, staying bent, where the note is long enough, whatever
                    // the bend drawn but a fall-off, which ends its phrase, and a release's, which bends into its note; from a
                    // sequence of its own, drawn for every note, before its size, which a bend up leans to the chord's notes by
                    var late = new GenerationContext(Seeds.Derive(Seeds.Derive(noteSeed, map.PlaceKey(note.Position, NotePlacesPerBeat)), LateBendDecision))
                        .TestProbability(LateBendChance.At(u, genre.Chance(GenreChoices.BendTiming, "late")));
                    if (late && !isRelease && kind != BendKind.FallOff && Fits(BendKind.LateBend, Math.Round(Math.Max(eighth, quick), 3), seconds, eighth))
                        kind = BendKind.LateBend;
                    // a bend up to a scale note, the likelier its chord's, as a player bends a fourth to a fifth
                    var tops = Realizer.GetChordNotes(note.Value.State).Classes;
                    var size = drawing.Pick(Tilt.Of(ChordTopOdds, 1).Weigh(soloist is null ? sizes : soloLean.Weigh(ByConvention.Weigh(BendSizes, u), steps => steps > 1 ? 1 : -1),
                        steps => kind is BendKind.BendAndRelease or BendKind.PrebendRelease or BendKind.LateBend && tops.Contains((note.Value.Pitches[0] + BendDistance(note.Value, steps)).Mod(Realizer.OctaveNoteCount)) ? 1 : 0));
                    // from the note before, where it was heard up to a moment before, and no further than a pitch bend reaches;
                    // the note before that a dive let go of unheard, and none from a dive, fallen away
                    var before = i - 1;
                    while (before >= 0 && dropped[before])
                        before--;
                    var from = before >= 0 && held[before] is null && timeline[before].Position + timeline[before].Value.Duration >= note.Position - slideGap
                        ? timeline[before].Value.Pitches[0] - note.Value.Pitches[0]
                        : 0;
                    // a gliding part sliding the likelier into a leap and into its phrase's last note
                    var glideLean = glides ? Tilt.Of(GlideOdds, (Math.Abs(from) >= GlideLeap ? 1 : 0) + (ends ? 1 : 0)) : new Tilt(0);
                    var slidesNow = drawing.TestProbability(soloist is null ? glideLean.Chance(slideRate, 1) : new Tilt(intensity.LogOdds + Tilt.Of(StrengthOdds, soloist.SlideLean - Soloist.ExpressionLean).LogOdds).Chance(SlideRate.At(songSound), 1));
                    // and its vibrato, a note at a time, the wide likelier the more intense
                    var noteVibrato = soloist is null ? vibrato : drawing.Pick(soloLean.Weigh(ByConvention.Weigh(Vibratos.Where(x => Suits(x.Width, program)), u, widths), Lean));
                    // how fast it bends or slides, quickly, a soloist the quicker the more intense, or slowly, in time
                    var slow = drawing.TestProbability(SlowBendChance.At(u, genre.Chance(GenreChoices.BendTiming, "slow")));
                    // in whole milliseconds, as the note keeps it
                    var rise = Math.Round(slow || kind == BendKind.LateBend ? Math.Max(eighth, quick) : quick, 3);
                    var state = note.Value.State
                        .With(CompositionStateKinds.Reverb, reverb + 1)
                        .With(CompositionStateKinds.Chorus, choruses && Choruses(noteProgram) ? chorus : 0);
                    // a note that slides in bends no other way, and either only where the note holds long enough for it
                    var shape = BendKind.None;
                    if ((slides || soloist is not null && !echoes) && slidesNow && note.Value.Duration >= shortestSlide - 1e-9 && Fits(BendKind.Slide, rise, seconds, eighth)
                        && !isRelease && isLine && Bends(noteProgram) && Math.Abs(from) is >= 1 and <= Rendering.ExpressionRender.BendRange)
                    {
                        shape = BendKind.Slide;
                        state = state.With(CompositionStateKinds.Bend, (int)BendKind.Slide).With(CompositionStateKinds.BendDepth, from);
                    }
                    else if (bends && bendsNow && Fits(kind, rise, seconds, eighth))
                    {
                        shape = kind;
                        state = state.With(CompositionStateKinds.Bend, (int)kind).With(CompositionStateKinds.BendDepth, kind switch
                        {
                            BendKind.BendAndRelease or BendKind.PrebendRelease or BendKind.LateBend => BendDistance(note.Value, size),
                            BendKind.Scoop or BendKind.FallOff or BendKind.ReleaseAndBend => BendDistance(note.Value, -size),
                            _ => 0
                        });
                    }

                    if (shape != BendKind.None)
                        state = state.With(CompositionStateKinds.BendRise, (int)Math.Round(1000 * rise));
                    // and whether it sings a vibrato at all likewise, where its pitch holds long enough for one after its bend
                    // a wide one between the note and a scale note above it, so the note must be on the scale, and the note's
                    // instrument, where the part switches, suiting it
                    var width = !Suits(noteVibrato, noteProgram) || noteVibrato.Steps != 0 && !IsOnScale(note.Value) ? NarrowFor(noteProgram) : noteVibrato;
                    var sings = InstrumentTraits.Has(noteProgram, InstrumentTrait.SingsVibrato);
                    if ((soloist is null ? vibrates : soloist.Vibrates && !echoes) && isLine && Bends(noteProgram) && HoldsVibrato(shape, rise, seconds, eighth, width.Rate, sings))
                        state = (width.Steps != 0
                                ? state.With(CompositionStateKinds.WideVibrato, ScaleDistance(note.Value, width.Steps))
                                : state.With(CompositionStateKinds.Vibrato, width.Cents))
                            .With(CompositionStateKinds.VibratoRate, (int)Math.Round(10 * width.Rate))
                            .With(CompositionStateKinds.VibratoDelay, (int)Math.Round(10 * VibratoWait(shape, rise, seconds, eighth, width.Rate, sings)));
                    // a soloist attacks its notes rather than swell into them, and a melody swells only into its phrase's held last
                    // note, as a voice or a bow lets a phrase bloom, on a sound that holds, where a softer attack on every long
                    // note would blur its rhythm
                    if (swells && !isSolo && gate == 0 && (isSung
                            ? note.Value.State.GetStateValue(StateKinds.HeldDuration) > 0 && note.Value.Duration >= LineSwellBeats && !InstrumentTraits.Has(noteProgram, InstrumentTrait.Decays)
                            : note.Value.Duration >= 1))
                        state = state.With(CompositionStateKinds.Swell, 1);
                    if (role != TrackRole.Drum)
                        state = state.With(CompositionStateKinds.Tremolo, trembles ? tremolo : 0).With(CompositionStateKinds.AutoPan, sweeps);
                    if (pumps && kicked.Contains(BarStart(note.Position)))
                        state = state.With(CompositionStateKinds.Pump, PumpDepth);
                    // but a chord cycled at the frame rate, whose cycle an echo would blur
                    if (echoes && (isLine || role == TrackRole.Rhythm) && note.Value.State.GetStateValue(CompositionStateKinds.Cycled) == 0)
                        state = state.With(CompositionStateKinds.Echo, (int)Math.Round(delay * 4)).With(CompositionStateKinds.EchoOnce, once ? 1 : 0);
                    if (gate != 0)
                        state = state.With(CompositionStateKinds.Gate, gate);
                    if (tone != 0)
                        state = state.With(CompositionStateKinds.Tone, tone);
                    if (tape && role != TrackRole.Drum)
                        state = state.With(CompositionStateKinds.Wow, wow).With(CompositionStateKinds.Detune, detuned ? detune : 0);
                    if (filter != StrongEffect.None)
                        state = state.With(CompositionStateKinds.Filter, (int)filter).With(CompositionStateKinds.FilterDepth, filterDepth)
                            .With(CompositionStateKinds.FilterCycle, filter == StrongEffect.Opening ? span.Meter.PatternDuration : wobbleCycle);
                    // a pinch, a held note's harmonic, where the song pinches
                    var phrase = (index, span.PhraseOf(note.Position));
                    // but where it echoes, which plays its notes straight
                    if (songPinches && !echoes && Pinches(noteProgram) && note.Value.Pitches.Length == 1 && state.GetStateValue(CompositionStateKinds.Octave) == 0
                        && role.PlaysLine() && role != TrackRole.Bass
                        && state.GetStateValue(CompositionStateKinds.PalmMute) == 0 && !pinchedPhrases.Contains(phrase) && !isRelease
                        && note.Value.Duration * beatSeconds(note.Position) >= PinchShortest
                        && timeline.Count(x => Math.Abs(x.Position - note.Position) < 1 - 1e-9) / 2.0 <= PinchBusiest
                        && LongestOfBar(timeline, i, span))
                    {
                        var bar = span.BarOf(note.Position);
                        var afterChug = i > 0 && timeline[i - 1].Value.State.GetStateValue(CompositionStateKinds.PalmMute) > 0;
                        var phraseEnd = (bar + 1) % span.Meter.PhraseBars == 0;
                        var pinchLean = role is TrackRole.Riff or TrackRole.RiffTwin
                            ? Math.Log(afterChug ? PinchAfterChugOdds : 1) + Math.Log(phraseEnd ? PinchPhraseEndOdds : 1)
                            : isSolo ? intensity.LogOdds : 0;
                        var pinching = Pinching(note.Position, 0);
                        var pinched = pinching.TestProbability(new Tilt(pinchLean).Chance(PinchChance, 1));
                        var bent = pinching.TestProbability(PinchBendChance);
                        if (pinched)
                        {
                            pinchedPhrases.Add(phrase);
                            accents[i] = PinchAccent;
                            // bent up quickly and back where it holds long enough to be shaken after, its squeal's waver the
                            // gesture, and shaken where it holds long enough
                            var pinchShape = bent && Fits(BendKind.BendAndRelease, PartRise, seconds, eighth) && HoldsVibrato(BendKind.BendAndRelease, PartRise, seconds, eighth, PinchRate, true)
                                ? BendKind.BendAndRelease
                                : BendKind.None;
                            // shaken at once, as a squeal is
                            var shakes = HoldsVibrato(pinchShape, PartRise, seconds, eighth, PinchRate, true);
                            state = state.With(CompositionStateKinds.Pinch, 1).With(CompositionStateKinds.Vibrato, shakes ? PinchVibrato : 0)
                                .With(CompositionStateKinds.VibratoRate, shakes ? (int)Math.Round(10 * PinchRate) : 0)
                                .With(CompositionStateKinds.VibratoDelay, shakes ? (int)Math.Round(10 * VibratoWait(pinchShape, PartRise, seconds, eighth, PinchRate, true)) : 0)
                                .With(CompositionStateKinds.WideVibrato, 0);
                            state = pinchShape == BendKind.BendAndRelease
                                ? state.With(CompositionStateKinds.Bend, (int)BendKind.BendAndRelease).With(CompositionStateKinds.BendDepth, ScaleDistance(note.Value, 1))
                                    .With(CompositionStateKinds.BendRise, (int)Math.Round(1000 * PartRise))
                                : state.With(CompositionStateKinds.Bend, (int)BendKind.None).With(CompositionStateKinds.BendDepth, 0).With(CompositionStateKinds.BendRise, 0);
                            state = state.With(CompositionStateKinds.Program, GuitarHarmonics + 1);
                        }
                    }

                    // the riff's longest note of its last bar before a louder section diving into it, where the song dives, the
                    // riff letting go of its notes after it, as a guitarist leaves the riff for the bar
                    if (songDives && !echoes && role.Source() is TrackRole.Riff && !isSolo && Pinches(noteProgram) && note.Value.Pitches.Length <= 2
                        && note.Position >= span.LastBar - 1e-9 && LongestOfBar(timeline, i, span) && !holds.Meets(Hold.Stop, span.LastBar, span.End)
                        && index + 1 < energies.Count && energies[index + 1] > energies[index]
                        && note.Value.Duration >= 1 && note.Value.Duration * beatSeconds(note.Position) >= PinchShortest
                        && state.GetStateValue(CompositionStateKinds.Pinch) == 0 && Pinching(note.Position, 1).TestProbability(DiveChance))
                    {
                        state = state.With(CompositionStateKinds.Bend, (int)BendKind.Dive).With(CompositionStateKinds.BendDepth, -DiveDepth).With(CompositionStateKinds.BendRise, 0)
                            .With(CompositionStateKinds.Vibrato, 0).With(CompositionStateKinds.WideVibrato, 0).With(CompositionStateKinds.VibratoRate, 0).With(CompositionStateKinds.VibratoDelay, 0);
                        // held through to the louder section's downbeat, where the band plays on to it, so that the dive is heard
                        held[i] = Math.Max(note.Value.Duration, trackCuts.Where(x => x > note.Position + 1e-9).Append(span.End).Min() - note.Position);
                        for (var next = i + 1; next < timeline.Count && timeline[next].Position < span.End - 1e-9; next++)
                            dropped[next] = true;
                    }

                    states[i] = state;
                }
            }

            // an intro's notes before the first section, in the sound the part plays the section it is made of in, as the
            // ending's below: a hook intro's last bar, which leads into the chorus as its own, and a hook not quoted, the
            // chorus's; an intro's entries before the first section, the first's
            if (Array.FindIndex(states, x => x is not null) is var firstPlayed and >= 0
                && Enumerable.Range(0, timeline.Count).FirstOrDefault(i => states[i] is not null && introSource.IsWithin(timeline[i].Position), firstPlayed) is var sourced)
                for (var i = 0; i < timeline.Count && timeline[i].Position < map.Origin - 1e-9; i++)
                    if (states[i] is null)
                    {
                        states[i] = Steady(timeline[i].Value, states[sourced]!);
                        // and ducking under the kick where it strikes the bar, as the section's notes do
                        if (pumps && kicked.Contains(BarStart(timeline[i].Position)))
                            states[i] = states[i]!.With(CompositionStateKinds.Pump, PumpDepth);
                    }

            // the ending's notes, the final chord, in the sound the part last played in, the last section's or before it where
            // it rested there: its sends, its tone, its tape and its steady effects, none of a note's own gestures
            if (Array.FindLastIndex(states, x => x is not null) is var lastPlayed and >= 0)
                for (var i = lastPlayed + 1; i < timeline.Count; i++)
                    if (states[i] is null && timeline[i].Position >= map.Ending.Start - 1e-9)
                        states[i] = Steady(timeline[i].Value, states[lastPlayed]!);

            result[track] = EventTimeline.Create(
                timeline.Duration,
                timeline.Select((x, i) => states[i] is { } state
                    ? (x.Value with
                    {
                        State = state,
                        Duration = held[i] ?? x.Value.Duration
                    }).WithVelocity(x.Value.Velocity + accents[i]).ToTimelineItem(x.Position)
                    : x).Where((_, i) => !dropped[i])
            );
        }

        // and a backbeat drum, a snare's, where the rhythm part echoes, as a dub echoes its skank and its snare, every stroke
        // of it, as a drum's send to the delay is its note's, at the rhythm part's delay
        if (carriers.TryGetValue(StrongEffect.Echo, out var echoing) && definitions[echoing].Role == TrackRole.Rhythm && carrierEcho is var (echoDelay, echoOnce))
            foreach (var track in backbeat.Where(result.ContainsKey))
                result[track] = EventTimeline.Create(result[track].Duration,
                    result[track].Select(x => (x.Value with
                    {
                        State = x.Value.State.With(CompositionStateKinds.Echo, (int)Math.Round(echoDelay * 4)).With(CompositionStateKinds.EchoOnce, echoOnce ? 1 : 0)
                    }).ToTimelineItem(x.Position)));

        return result.ToImmutable();
    }
}

/// <summary>The effects a song plays at all: the gentle ones, each its own, the strong one drawn first, or none, and the others beside it.</summary>
internal sealed record SongEffects(bool Vibrato, bool Chorus, bool Swell, bool Bends, StrongEffect Strong, ImmutableArray<StrongEffect> Others);

/// <summary>
///     The effects a song plays at all, and the part that plays each strong one, by its track, a pump's every part it suits
///     whatever part it names (<see cref="Expression.DrawSound" />).
/// </summary>
internal sealed record SongSound(SongEffects Effects, ImmutableDictionary<StrongEffect, int> Carriers);

/// <summary>How an echo repeats (<see cref="Expression.EchoKinds" />).</summary>
public enum EchoKind
{
    DottedEighth,
    Quarter,
    Eighth,
    Cascade
}
