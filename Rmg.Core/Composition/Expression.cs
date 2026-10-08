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
    Dive
}

/// <summary>A vibrato's width: in cents either side of the note, or in scale steps up from it, the wide sort.</summary>
public readonly record struct Vibrato(int Cents, int Steps);

/// <summary>The strong effects, which move a part's sound about, a song playing one of them first and others beside it now and then.</summary>
public enum StrongEffect
{
    None,
    Tremolo,
    Echo,
    Sweep,

    /// <summary>The pads, the chords and the bass ducking on every beat and swelling back, as a kick four on the floor pumps them.</summary>
    Pump,

    /// <summary>A synth's filter closing and opening again and again, in time, as a wobbling bass's.</summary>
    Wobble,

    /// <summary>A synth's filter opening on every note, ringing, and closing over it, as an acid line's squelches.</summary>
    Squelch,

    /// <summary>A synth's filter opening over the first phrase of every section the part plays, as a track builds in.</summary>
    Opening,

    /// <summary>A held chord chopped in 16ths, sounding in some and silent between, as a trance gate chops a pad.</summary>
    Gate
}

/// <summary>How a pinch harmonic sounds (<see cref="Expression.Pinches" />).</summary>
public enum PinchSound
{
    /// <summary>General MIDI's guitar harmonics, a clean bell, at the note's pitch.</summary>
    Harmonics,

    /// <summary>The driven guitar itself at the harmonic's pitch, an octave and a fifth or two octaves up, a squeal.</summary>
    Squeal
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
    private static int ProgramOf(RealizedNote note, int program) => note.State.GetStateValue(CompositionStateKinds.Program) is var stated and > 0 ? stated - 1 : program;

    /// <summary>Whether an instrument bends and sings a vibrato: guitars, strings, voices, brass, reeds, pipes, synth leads, a fretless or synth bass.</summary>
    public static bool Bends(int program) => InstrumentTraits.Has(program, InstrumentTrait.Bends);

    /// <summary>Whether an instrument sounds with a chorus the classic way: an electric piano, a clean guitar, a synth pad.</summary>
    private static bool ChorusesClassically(int program) => InstrumentTraits.Has(program, InstrumentTrait.ChorusesClassically);

    /// <summary>Whether an instrument may take a chorus: pianos, guitars, strings, leads and pads.</summary>
    private static bool Choruses(int program) => InstrumentTraits.Has(program, InstrumentTrait.Choruses);

    /// <summary>
    ///     Whether a strong effect suits a part, which plays it the likelier: an echo a lead's or a comping part's, a dub's
    ///     skank's, a tremolo a guitar's, an
    ///     electric piano's or an organ's, a sweep a pad's, a synth's or an organ's.
    /// </summary>
    private static bool Suits(StrongEffect effect, TrackRole role, int program) => effect switch
    {
        StrongEffect.Echo => role is TrackRole.Melody or TrackRole.Rhythm,
        StrongEffect.Tremolo => InstrumentTraits.Has(program, InstrumentTrait.Tremolo),
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

    /// <summary>Whether a strong effect is played only by a part it suits: a filter's, a synth's, and a gate's, held chords'.</summary>
    private static bool OnlySuited(StrongEffect effect) => IsFilter(effect) || effect == StrongEffect.Gate;

    /// <summary>Whether a part may echo: one that plays lines, or the rhythm part, whose stabs a dub echoes.</summary>
    private static bool EchoesFrom(TrackRole role) => IsLineRole(role) || role == TrackRole.Rhythm;

    /// <summary>Whether a part plays lines, single notes one after another, rather than chords or drums.</summary>
    private static bool IsLineRole(TrackRole role) => !role.PlaysChords() && role is not (TrackRole.Pad or TrackRole.Drum);

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

    /// <summary>The chance a pitched part of a song off a tape is held off the rest (<see cref="TapeChance" />).</summary>
    private const double DetuneChance = 0.35;

    /// <summary>How far a tape's pitch drifts either way, and a part is held off at most, in cents.</summary>
    private const int WowLeast = 4, WowMost = 14, DetuneMost = 8;

    /// <summary>
    ///     The chance a song's driven guitars pinch now and then, a held note's harmonic squealing over it, its highlight: a
    ///     metal band's, seldom the songs'.
    /// </summary>
    public static ByConvention SongPinchChance { get; } = new(0, 0.1, 0.1);

    /// <summary>
    ///     Whether a note may be pinched, and how likely: a driven guitar's single note in a line, the riff's, the melody's,
    ///     the counter-melody's or a solo's, held a while by the clock and the longest of its bar, never within a run, one a
    ///     part a phrase at most; a riff's the likelier after palm-muted chugs, its contrast, and at its phrase's end, a
    ///     solo's the more intense it is. Its sound a genre's (<see cref="PinchSounds" />), and always wavering, often bent.
    /// </summary>
    public const double PinchChance = 0.12;

    /// <summary>The shortest note a pinch takes, in seconds, and the most notes a beat around it.</summary>
    public const double PinchShortest = 0.4, PinchBusiest = 2.5;

    /// <summary>How much likelier a riff's pinch is after a palm-muted note, and on its phrase's last bar, as odds.</summary>
    public const double PinchAfterChugOdds = 4, PinchPhraseEndOdds = 2;

    /// <summary>How wide a pinch's vibrato is, in cents, and the chance it is bent up to the next scale note and back.</summary>
    public const int PinchVibrato = 40;

    public const double PinchBendChance = 0.5;

    /// <summary>A pinch's sound, as the songs have it, General MIDI's harmonics, the squeal a genre's.</summary>
    public static ImmutableArray<(PinchSound Sound, ByConvention Weight)> PinchSounds { get; } =
    [
        (PinchSound.Harmonics, new ByConvention(1, 1, 1)),
        (PinchSound.Squeal, new ByConvention(0, 0, 0))
    ];

    /// <summary>The semitones above its note a squealing pinch sounds at, an octave and a fifth or two octaves.</summary>
    private static readonly int[] PinchIntervals = [19, 24];

    /// <summary>
    ///     The chance a song's riff dives on the whammy bar into a louder section, its last held note there falling away,
    ///     a metal band's, seldom the songs' (<see cref="BendKind.Dive" />); and the chance a section it may dive into does.
    /// </summary>
    public static ByConvention SongDiveChance { get; } = new(0, 0.05, 0.05);

    public const double DiveChance = 0.5;

    /// <summary>How far a dive falls, in semitones: an octave.</summary>
    public const int DiveDepth = 12;

    /// <summary>General MIDI's guitar harmonics, which a pinch sounds on where it rings as a bell.</summary>
    private const int GuitarHarmonics = 31;

    /// <summary>Whether a note is the longest of its bar, a held one among the part's, which a pinch takes.</summary>
    private static bool LongestOfBar(EventTimeline<RealizedNote> notes, int index, SectionSpan span)
    {
        var bar = span.Start + Math.Floor((notes[index].Position - span.Start) / span.Meter.BarDuration + 1e-9) * span.Meter.BarDuration;
        return notes.Where(x => x.Position >= bar - 1e-9 && x.Position < bar + span.Meter.BarDuration - 1e-9).All(x => x.Value.Duration <= notes[index].Value.Duration + 1e-9);
    }

    /// <summary>Whether a program is a driven guitar, which pinches.</summary>
    private static bool Pinches(int program) => program is 29 or 30;

    /// <summary>How far a part's tone closes at its deepest, in its quietest section, in the brightness controller's steps, short of a filter's reach.</summary>
    private const int ToneReach = 16;

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
    ///     an echo goes to a line, and a tremolo and a sweep to any pitched part.
    /// </summary>
    public const double SuitedStrongWeight = 4;

    /// <summary>The chance a part that can sings a vibrato where the song does, on its long notes.</summary>
    public static ByConvention VibratoChance { get; } = new(0, 0.6, 0.9);

    /// <summary>
    ///     How wide a held note's vibrato is: narrow, in cents either side of the note, as a voice or a violin sings, or
    ///     wide, in scale steps up from the note and back, as a guitar's does, up to four semitones, both its ends on scale
    ///     notes; the wide the wilder, and only an instrument it suits plays it. Each leans as a part's strength and its
    ///     section's energy lean, the wider the more.
    /// </summary>
    public static ImmutableArray<(Vibrato Width, ByConvention Weight)> Vibratos { get; } =
    [
        (new Vibrato(15, 0), new ByConvention(1, 0.5, 0.2)),
        (new Vibrato(35, 0), new ByConvention(0, 0.35, 0.2)),
        (new Vibrato(60, 0), new ByConvention(0, 0.15, 0.2)),
        (new Vibrato(0, 1), new ByConvention(0, 0.02, 0.25)),
        (new Vibrato(0, 2), new ByConvention(0, 0, 0.15))
    ];

    /// <summary>How far a width leans to the wide side, for a strength or an energy to lean it.</summary>
    private static double Lean(Vibrato width) => width.Steps > 0 ? 0.5 * width.Steps : width.Cents / 60.0 - 1;

    /// <summary>The narrow vibrato a note plays in place of a wide one it cannot, a note off the scale or an instrument it does not suit.</summary>
    private static Vibrato WidestNarrow => new(60, 0);

    /// <summary>Whether a wide vibrato suits an instrument: a guitar's or a synth lead's.</summary>
    private static bool VibratesWide(int program) => InstrumentTraits.Has(program, InstrumentTrait.VibratesWide);

    /// <summary>
    ///     The chance a part slides from note to note at all, never in a plain song and not every part in a wild one, and
    ///     how often one that does slides into a note it may, its own rate spread about this.
    /// </summary>
    public static ByConvention SlideChance { get; } = new(0, 0.25, 0.6);

    public static ByConvention SlideRate { get; } = new(0, 0.1, 0.3);

    /// <summary>The chance a song's bass glides from note to note, a genre's that the songs never play, and the share of its notes it slides into (<see cref="GenreChoices.Glide" />).</summary>
    public static ByConvention GlideChance { get; } = new(0, 0, 0);

    public const double GlideRate = 0.5;

    /// <summary>How far after the note before, in beats, a gliding bass still slides from it, and the shortest note it slides into.</summary>
    public const double GlideGap = 1;

    public const double GlideShortest = 0.25;

    /// <summary>How long a note must be for a bend or a slide on it to be heard, in beats: an 8th, a run's notes going by straight.</summary>
    public const double ShortestShaped = 0.5;

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

    /// <summary>How long a soloist's note must be to sing a vibrato, in beats: half a beat, as a soloist sings one on any note it holds, where a part's lines from a beat.</summary>
    public const double SoloVibratoBeats = 0.5;

    /// <summary>How far a solo's intensity leans its notes, as a strength does: the bigger bends and vibratos, and the oftener bends and slides, the more intense.</summary>
    public const double SoloOdds = 8;

    public static ImmutableArray<(BendKind Kind, ByConvention Weight)> BendKinds { get; } =
    [
        (BendKind.Scoop, new ByConvention(1, 0.4, 1)),
        (BendKind.FallOff, new ByConvention(1, 0.25, 1)),
        (BendKind.BendAndRelease, new ByConvention(1, 0.15, 1)),
        (BendKind.ReleaseAndBend, new ByConvention(0, 0.06, 1)),
        (BendKind.PrebendRelease, new ByConvention(0, 0.08, 1))
    ];

    /// <summary>The chance a part's held notes swell in where the song's do, a pad's likely, any other's now and then.</summary>
    public static ByConvention PadSwellChance { get; } = new(0, 0.5, 0.9);

    public static ByConvention SwellChance { get; } = new(0, 0.05, 0.3);

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

    /// <summary>The delays an echo repeats after, in beats: a dotted 8th, a quarter, an 8th.</summary>
    public static ImmutableArray<double> EchoDelays { get; } = [0.75, 1, 0.5];

    /// <summary>A part's reverb, from 0 to 127, as a mix sets it by role, and how far a section strays from it, by the sound facet.</summary>
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
        var lean = genre.Shift(GenreChoices.Space, GenreChoices.Name((role == TrackRole.RiffTwin ? TrackRole.Riff : role).ToString()));
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
        var weights = ByConvention.Weigh(StrongEffects, songSound, genre.Pool<StrongEffect>(GenreChoices.StrongEffect, x => GenreChoices.Name(x.ToString())));
        var strong = songStream.Pick(weights);

        // the parts that may play an effect, the likelier one it suits, but one that plays another filter's already; a gate
        // the parts the genre has it chop
        var gates = ByConvention.Weigh(GateParts, songSound, genre.Pool<TrackRole>(GenreChoices.GateParts, x => GenreChoices.Name(x.ToString())))
            .Where(x => x.Weight > 0).Select(x => x.Value).ToHashSet();
        bool Fits(StrongEffect effect, TrackRole role, int program) => effect == StrongEffect.Gate ? gates.Contains(role) : Suits(effect, role, program);
        var carriers = ImmutableDictionary.CreateBuilder<StrongEffect, int>();
        ImmutableArray<Weighted<int>> MayCarry(StrongEffect effect) =>
        [
            ..definitions
                .Where(x => playing.Contains(x.Key) && x.Value is PitchInstrumentTrack && (effect != StrongEffect.Echo || EchoesFrom(x.Value.Role))
                            && (!OnlySuited(effect) || Fits(effect, x.Value.Role, ((PitchInstrumentTrack)x.Value).InstrumentCode))
                            && !(IsFilter(effect) && carriers.Any(c => IsFilter(c.Key) && c.Value == x.Key)))
                .Select(x => new Weighted<int>(Fits(effect, x.Value.Role, ((PitchInstrumentTrack)x.Value).InstrumentCode) ? SuitedStrongWeight : 1, x.Key))
        ];
        var first = MayCarry(strong);
        if (strong != StrongEffect.None && !first.IsEmpty)
            carriers[strong] = songStream.Pick(first);
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
    /// <param name="toneStream">The song's sequence of its parts' tones, whether the song plays them and each part's, in order.</param>
    /// <param name="pinchStream">The song's sequence of whether its guitars pinch and how it sounds.</param>
    /// <param name="pinchStreams">A part's sequence of its pinches, by its track.</param>
    /// <param name="beatSeconds">How long a beat lasts at a position, in seconds, which a pinched note's length is held to.</param>
    /// <param name="tapeStream">The song's sequence of its tape's wow, whether it plays one, how deep, and each part's detune, in order.</param>
    /// <param name="genre">The genre, whose chances of the gentle effects and pool of the strong ones are the song's where it names them.</param>
    /// <param name="changes">Where the chords change, in order, which a gated part's held chords change at.</param>
    /// <param name="cuts">Where a part's notes end as the band stops, by its track, which a gated part's held chords end by.</param>
    /// <param name="stopTime">The spans played in stop time, whose hits a gated part plays as they are.</param>
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
        IReadOnlyList<(double Start, double End)> stopTime
    )
    {
        var ((songVibrato, songChorus, songSwell, songBends, _, _), carriers) = songEffects;
        // a gated part holding its chords, which the gate chops in time
        if (carriers.TryGetValue(StrongEffect.Gate, out var gated))
            notes = notes.SetItem(gated, GatedChords.Hold(notes[gated], map, changes, cuts(gated), stopTime,
                note => InstrumentTraits.Has(ProgramOf(note, ((PitchInstrumentTrack)definitions[gated]).InstrumentCode), InstrumentTrait.Decays)));

        // the snares, which echo with a comping part's echo, at its delay, the drums' tracks coming after the pitched parts'
        var backbeat = DrumGroups.Snare.Drums.Select(DrumGroups.GetTrackNumber).ToHashSet();
        double? carrierDelay = null;
        // whether the song gives its parts tones at all, the genre's where it names it
        var songTone = toneStream.TestProbability(SongToneChance.At(songSound, genre.Chance(GenreChoices.Effects, "tone")));
        // and whether it plays off a tape, and how far its pitch drifts
        var tape = tapeStream.TestProbability(TapeChance.At(songSound, genre.Chance(GenreChoices.Effects, "tape")));
        var wow = tapeStream.GenerateInt(WowLeast, WowMost + 1);
        // and whether its riff dives into a louder section
        var songDives = pinchStream.TestProbability(SongDiveChance.At(songSound, genre.Chance(GenreChoices.Effects, "dive")));
        // and whether its guitars pinch, and how a pinch sounds
        var songPinches = pinchStream.TestProbability(SongPinchChance.At(songSound, genre.Chance(GenreChoices.Effects, "pinch")));
        var pinchSound = pinchStream.Pick(ByConvention.Weigh(PinchSounds, songSound, genre.Pool<PinchSound>(GenreChoices.PinchSound, x => GenreChoices.Name(x.ToString()))));

        var result = notes.ToBuilder();
        foreach (var (track, timeline) in notes)
        {
            var definition = definitions[track];
            var role = definition.Role;
            var program = definition is PitchInstrumentTrack pitched ? pitched.InstrumentCode : -1;
            var states = new StateMap?[timeline.Count];
            var raised = new int[timeline.Count];
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
            var swells = part.TestProbability((role == TrackRole.Pad ? PadSwellChance : SwellChance).At(songSound)) & songSwell;
            var bendsHere = part.TestProbability(PartBendChance.At(songSound)) & songBends;
            bool Carries(StrongEffect effect) => carriers.TryGetValue(effect, out var carrier) && carrier == track;
            // and a backbeat drum, a snare's, where the rhythm part echoes, as a dub echoes its skank and its snare
            var echoesBackbeat = carriers.TryGetValue(StrongEffect.Echo, out var echoing) && definitions[echoing].Role == TrackRole.Rhythm && backbeat.Contains(track);
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
            var delay = EchoDelays[part.GenerateInt(0, EchoDelays.Length)];
            if (echoes)
                carrierDelay = delay;
            var room = Reverb(role, genre);
            var reverb = (int)Math.Round(room + ReverbSpread.At(songSound) * (part.GenerateDouble() * 127 - room));
            var choruses = part.TestProbability((ChorusesClassically(program) ? ClassicChorusChance : ChorusChance).At(songSound)) & songChorus;
            var chorus = 40 + part.GenerateInt(0, 60);
            // how much the part leans to the bigger sizes, around 0
            var strength = StrengthSpread.At(songSound) * Generators.SplineValue()(part);
            // and whether it slides from note to note, and how often, but where it echoes, playing straight
            var slides = part.TestProbability(SlideChance.At(songSound)) & !echoes;
            var slideRate = SlideRate.At(songSound) * 2 * part.GenerateDouble();
            // a bass that glides, a genre's, into most of its notes from the note before, as an 808's or an acid line's
            // does; drawn last, so that the part's other draws stay
            var glides = part.TestProbability(GlideChance.At(songSound, genre.Chance(GenreChoices.Glide, "bass"))) && role == TrackRole.Bass && !echoes;
            if (glides)
                (slides, slideRate) = (true, GlideRate);
            // and from across a gap, into a short note too
            var (slideGap, shortestSlide) = glides ? (GlideGap, GlideShortest) : (SlideGap, ShortestShaped);
            // and how far its filter closes, and a wobble's cycle, drawn last, so that the part's other draws stay
            var filterDepth = 40 + part.GenerateInt(0, 60);
            var wobbleCycle = WobbleCycles[part.GenerateInt(0, WobbleCycles.Length)];
            // the filter this part plays, one at most
            var filter = carriers.Where(x => IsFilter(x.Key) && x.Value == track).Select(x => x.Key).FirstOrDefault();
            // and a gate's 16ths, every one or a pattern's, its first always sounding, drawn whatever the song plays
            var patterned = part.TestProbability(GatePatternChance.At(songSound));
            var steps = Enumerable.Range(0, 16).Select(_ => part.TestProbability(GateStepChance)).ToArray();
            var gate = Carries(StrongEffect.Gate) ? steps.Select((x, i) => i == 0 || !patterned || x ? 1 << i : 0).Sum() : 0;
            // and its tone, from a sequence of its own, every part drawing whatever it plays
            var toned = toneStream.TestProbability(ToneChance.At(songSound)) & songTone && role != TrackRole.Drum;
            var toneDepth = 0.3 + 0.7 * toneStream.GenerateDouble();
            // and its detune, either way, every part drawing whatever it plays; a drum's kit is left in tune
            var detuned = tapeStream.TestProbability(DetuneChance) && tape && role != TrackRole.Drum;
            var detune = (tapeStream.TestProbability(0.5) ? 1 : -1) * tapeStream.GenerateInt(2, DetuneMost + 1);
            // and its pinches, from a sequence of its own, one a phrase at most
            var pinching = pinchStreams(track);
            var pinchedPhrases = new HashSet<(int, int)>();
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
                var vibrato = context.Pick(lean.Weigh(ByConvention.Weigh(Vibratos.Where(x => x.Width.Steps == 0 || VibratesWide(program)), u), Lean));
                // the part's tone here, darker the quieter the section, as dark as it goes at an energy of -1 and below, half as
                // dark at 1 and above
                var tone = toned ? (int)Math.Round(ToneReach * toneDepth * (3 - Math.Clamp(energies[index], -1, 1)) / 4) : 0;

                // the notes in the section, in order, the timeline's being
                for (var i = first(span.Start); i < timeline.Count && timeline[i].Position < span.End - 1e-9; i++)
                {
                    // a note the riff let go of for a dive plays no more
                    if (dropped[i])
                        continue;
                    var note = timeline[i];

                    var noteProgram = ProgramOf(note.Value, program);
                    // one note at a time, or in octaves, which bend and echo together
                    var isLine = (note.Value.Pitches.Length == 1 || note.Value.State.GetStateValue(CompositionStateKinds.Octave) > 0)
                                 && (IsLineRole(role) || note.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 1);
                    var isSolo = note.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 1;
                    // a solo's note plays as its soloist leans, its strength in place of the part's, and the more intense the
                    // solo there, the bigger and the oftener; whether it bends at all is the soloist's choice, not the song's,
                    // which is the band's, but where it echoes
                    var soloist = isSolo ? soloists.GetValueOrDefault(role) : null;
                    var bends = (soloist is null ? bendsHere : soloist.Bends && !echoes) && isLine && Bends(noteProgram);
                    var intensity = Tilt.Of(SoloOdds, 2 * note.Value.State.GetStateValue(CompositionStateKinds.SoloIntensity) / 100.0 - 1);
                    var soloLean = soloist is null ? lean : new Tilt(Tilt.Of(StrengthOdds, soloist.BendLean).LogOdds + SectionEnergy.Tilt(energies[index], SizeCoupling).LogOdds + intensity.LogOdds);
                    // every note draws whether it bends, and how, so that the draws stay as they are
                    // a release bends into it now and then, the likelier the more intense, and slides into it never
                    var isRelease = note.Value.State.GetStateValue(CompositionStateKinds.SoloRelease) == 1;
                    // a soloist's note bends as often as it bends a note, leaned by its lean to bends as well as by the intensity
                    if (isSolo && soloist is null)
                        throw new InvalidOperationException($"Track {track} solos with no soloist.");
                    var bendsNow = context.TestProbability(soloist is null ? bendChance
                        : isRelease ? intensity.Chance(ReleaseBendChance, 1)
                        : new Tilt(intensity.LogOdds + Tilt.Of(StrengthOdds, soloist.BendLean - Soloist.ExpressionLean).LogOdds).Chance(soloist.BendRate, 1));
                    var kind = context.Pick(bendKinds);
                    if (isRelease)
                        kind = context.Pick(ReleaseBends);
                    var size = context.Pick(soloist is null ? sizes : soloLean.Weigh(ByConvention.Weigh(BendSizes, u), steps => steps > 1 ? 1 : -1));
                    var slidesNow = context.TestProbability(soloist is null ? slideRate : new Tilt(intensity.LogOdds + Tilt.Of(StrengthOdds, soloist.SlideLean - Soloist.ExpressionLean).LogOdds).Chance(SlideRate.At(songSound), 1));
                    // and its vibrato, a note at a time, the wide likelier the more intense
                    var noteVibrato = soloist is null ? vibrato : context.Pick(soloLean.Weigh(ByConvention.Weigh(Vibratos.Where(x => x.Width.Steps == 0 || VibratesWide(program)), u), Lean));
                    // from the note before, where it was heard up to a moment before, and no further than a pitch bend reaches
                    var from = i > 0 && timeline[i - 1].Position + timeline[i - 1].Value.Duration >= note.Position - slideGap
                        ? timeline[i - 1].Value.Pitches[0] - note.Value.Pitches[0]
                        : 0;
                    var state = note.Value.State
                        .With(CompositionStateKinds.Reverb, reverb + 1)
                        .With(CompositionStateKinds.Chorus, choruses && Choruses(noteProgram) ? chorus : 0);
                    // and whether it sings a vibrato at all likewise
                    if ((soloist is null ? vibrates : soloist.Vibrates && !echoes) && isLine && Bends(noteProgram) && note.Value.Duration >= (soloist is null ? 1 : SoloVibratoBeats))
                    {
                        // a wide one between the note and a scale note above it, so the note must be on the scale
                        var width = noteVibrato.Steps > 0 && (!VibratesWide(noteProgram) || !IsOnScale(note.Value)) ? WidestNarrow : noteVibrato;
                        state = width.Steps > 0
                            ? state.With(CompositionStateKinds.WideVibrato, ScaleDistance(note.Value, width.Steps))
                            : state.With(CompositionStateKinds.Vibrato, width.Cents);
                    }
                    // a note that slides in bends no other way
                    var shaped = note.Value.Duration >= ShortestShaped - 1e-9;
                    if ((slides || soloist is not null && !echoes) && slidesNow && note.Value.Duration >= shortestSlide - 1e-9 && !isRelease && isLine && Bends(noteProgram) && Math.Abs(from) is >= 1 and <= Rendering.ExpressionRender.BendRange)
                        state = state.With(CompositionStateKinds.Bend, (int)BendKind.Slide).With(CompositionStateKinds.BendDepth, from);
                    else if (bends && bendsNow && shaped)
                        state = state.With(CompositionStateKinds.Bend, (int)kind).With(CompositionStateKinds.BendDepth, kind switch
                        {
                            BendKind.BendAndRelease or BendKind.PrebendRelease => ScaleDistance(note.Value, size),
                            BendKind.Scoop or BendKind.FallOff or BendKind.ReleaseAndBend => ScaleDistance(note.Value, -size),
                            _ => 0
                        });
                    // a soloist attacks its notes rather than swell into them
                    if (swells && !isSolo && note.Value.Duration >= 1 && gate == 0)
                        state = state.With(CompositionStateKinds.Swell, 1);
                    // a tremolo moving a held note alone, as a gate chops a held chord, never a run's
                    if (role != TrackRole.Drum)
                        state = state.With(CompositionStateKinds.Tremolo, note.Value.Duration >= 1 - 1e-9 ? tremolo : 0).With(CompositionStateKinds.AutoPan, sweeps);
                    if (pumps)
                        state = state.With(CompositionStateKinds.Pump, PumpDepth);
                    if (echoes && (isLine || role == TrackRole.Rhythm) || echoesBackbeat && Math.Abs(note.Position - Math.Round(note.Position)) < 1e-9)
                        state = state.With(CompositionStateKinds.Echo, (int)Math.Round((echoesBackbeat ? carrierDelay ?? throw new InvalidOperationException("A snare echoes before its part does.") : delay) * 4));
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
                    var phrase = (index, (int)Math.Floor((note.Position - span.Start) / (Meter.PhraseBarCount * span.Meter.BarDuration) + 1e-9));
                    if (songPinches && Pinches(noteProgram) && note.Value.Pitches.Length == 1 && state.GetStateValue(CompositionStateKinds.Octave) == 0
                        && role is TrackRole.Riff or TrackRole.RiffTwin or TrackRole.Melody or TrackRole.CounterMelody or TrackRole.Solo
                        && state.GetStateValue(CompositionStateKinds.PalmMute) == 0 && !pinchedPhrases.Contains(phrase)
                        && note.Value.Duration * beatSeconds(note.Position) >= PinchShortest
                        && timeline.Count(x => Math.Abs(x.Position - note.Position) < 1 - 1e-9) / 2.0 <= PinchBusiest
                        && LongestOfBar(timeline, i, span))
                    {
                        var bar = Math.Floor((note.Position - span.Start) / span.Meter.BarDuration + 1e-9);
                        var afterChug = i > 0 && timeline[i - 1].Value.State.GetStateValue(CompositionStateKinds.PalmMute) > 0;
                        var phraseEnd = (bar + 1) % Meter.PhraseBarCount == 0;
                        var pinchLean = role is TrackRole.Riff or TrackRole.RiffTwin
                            ? Math.Log(afterChug ? PinchAfterChugOdds : 1) + Math.Log(phraseEnd ? PinchPhraseEndOdds : 1)
                            : isSolo ? intensity.LogOdds : 0;
                        var pinched = pinching.TestProbability(new Tilt(pinchLean).Chance(PinchChance, 1));
                        var bent = pinching.TestProbability(PinchBendChance);
                        var interval = PinchIntervals[pinching.GenerateInt(0, PinchIntervals.Length)];
                        if (pinched)
                        {
                            pinchedPhrases.Add(phrase);
                            state = state.With(CompositionStateKinds.Pinch, 1).With(CompositionStateKinds.Vibrato, PinchVibrato).With(CompositionStateKinds.WideVibrato, 0);
                            state = bent
                                ? state.With(CompositionStateKinds.Bend, (int)BendKind.BendAndRelease).With(CompositionStateKinds.BendDepth, ScaleDistance(note.Value, 1))
                                : state.With(CompositionStateKinds.Bend, (int)BendKind.None).With(CompositionStateKinds.BendDepth, 0);
                            if (pinchSound == PinchSound.Harmonics)
                                state = state.With(CompositionStateKinds.Program, GuitarHarmonics + 1);
                            else if (note.Value.Pitches[0] + interval <= 127)
                                raised[i] = interval;
                        }
                    }

                    // the riff's longest note of its last bar before a louder section diving into it, where the song dives, the
                    // riff letting go of its notes after it, as a guitarist leaves the riff for the bar
                    if (songDives && role is TrackRole.Riff && !isSolo && Pinches(noteProgram) && note.Value.Pitches.Length <= 2
                        && note.Position >= span.End - span.Meter.BarDuration - 1e-9 && LongestOfBar(timeline, i, span)
                        && index + 1 < energies.Count && energies[index + 1] > energies[index]
                        && note.Value.Duration >= 1 && note.Value.Duration * beatSeconds(note.Position) >= PinchShortest
                        && state.GetStateValue(CompositionStateKinds.Pinch) == 0 && pinching.TestProbability(DiveChance))
                    {
                        state = state.With(CompositionStateKinds.Bend, (int)BendKind.Dive).With(CompositionStateKinds.BendDepth, -DiveDepth)
                            .With(CompositionStateKinds.Vibrato, 0).With(CompositionStateKinds.WideVibrato, 0);
                        // held through to the louder section's downbeat, where the band plays on to it, so that the dive is heard
                        held[i] = Math.Max(note.Value.Duration, trackCuts.Where(x => x > note.Position + 1e-9).Append(span.End).Min() - note.Position);
                        for (var next = i + 1; next < timeline.Count && timeline[next].Position < span.End - 1e-9; next++)
                            dropped[next] = true;
                    }

                    states[i] = state;
                }
            }

            result[track] = EventTimeline.Create(
                timeline.Duration,
                timeline.Select((x, i) => states[i] is { } state
                    ? (x.Value with
                    {
                        State = state,
                        Pitches = raised[i] == 0 ? x.Value.Pitches : [..x.Value.Pitches.Select(p => p + raised[i])],
                        Duration = held[i] ?? x.Value.Duration
                    }).ToTimelineItem(x.Position)
                    : x).Where((_, i) => !dropped[i])
            );
        }

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
