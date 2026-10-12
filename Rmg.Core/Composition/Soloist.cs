using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>A figure a soloist's runs are made of, played again from note to note.</summary>
public enum SoloShape
{
    /// <summary>Up or down the scale, step by step.</summary>
    Scale,

    /// <summary>A short figure played again a step on, and again, as a sequence climbs.</summary>
    Sequence,

    /// <summary>The scale's thirds stacked from the note it is on, broken, a sequence of the scale's own chords as it moves a third on.</summary>
    Arpeggio,

    /// <summary>Around a note, a step above it and below and back.</summary>
    Turn,

    /// <summary>A note struck again between every other, as a pedal under a moving line.</summary>
    Pedal
}

/// <summary>
///     A soloist of a song, whose solos sound like one player's, as the drummer's fills do (<see cref="Drummer" />): the
///     speeds it runs at, in notes a beat, the sizes of the cells its runs are made of, and whether it groups cells of
///     several sizes into a larger figure, such as five and seven; the shapes it plays them in; how often it pauses; how
///     far and how fast its intensity swings; how wide it ranges, in scale steps; and how far it leans to bigger bends and
///     to slides, and whether it bends at all and sings a vibrato at all, its own choice rather than the song's, which is
///     the band's, a guitar or a synth lead the likelier to bend. Drawn once for every part that solos in a song, by the
///     song's melody facet, its expression by the sound facet: at the plain end 8ths and 16ths in cells of three or four, the scale, a sequence or the chord, and a steady
///     player; at the wild end any speed, cells of five, seven or nine grouped, and one who swings from runs to long notes
///     in a beat. A soloist on an instrument that plays chords stacks some of its notes, the chord's notes under its own,
///     as a pianist's or a guitarist's solo does, as many as it favours, and a wild one now and then planes them, its
///     chord's shape moved with its line.
/// </summary>
internal sealed record Soloist(
    ImmutableArray<int> Speeds,
    ImmutableArray<int> CellSizes,
    bool Groups,
    ImmutableArray<SoloShape> Shapes,
    double PauseRate,
    double Volatility,
    int RegisterSteps,
    double BendLean,
    double SlideLean,
    double MaxNotesASecond,
    bool Bends,
    bool Vibrates,
    double Stamina,
    ImmutableArray<int> PhraseBars,
    double OffBarChance,
    double BreathChance,
    ImmutableDictionary<SoloGesture, double> GestureWeights,
    double BendRate,
    double ChordRate,
    int Voices,
    bool Planes,
    bool Octaves,
    bool Approaches,
    double MotifInversion
)
{
    /// <summary>The chance a phrase answering the motif before inverts it rather than moving it, by the melody facet: never at the plain end.</summary>
    public static ByConvention MotifInversionChance { get; } = new(0, 0.3, 0.5);

    /// <summary>
    ///     The chance a soloist that stacks approaches a chord change by its chord: its stacked note off the beat before the
    ///     change planed, its shape moved with the note against the band's chord, by the melody facet: never at the plain
    ///     end, which is never reharmonised, and as often at the wild end as in the middle.
    /// </summary>
    public static ByConvention ApproachChance { get; } = new(0, 0.5, 0.5);

    /// <summary>The chance a soloist plays its held notes, licks and quotes in octaves, its runs alone, by the melody facet: never at the plain end.</summary>
    public static ByConvention OctaveChance { get; } = new(0, 0.1, 0.2);

    /// <summary>
    ///     How often a soloist on an instrument that plays chords stacks a gesture, about which each draws its own: a
    ///     conventional way to solo on one, a plain soloist's too, leaned by the gesture and its intensity (<see cref="SoloLines" />).
    /// </summary>
    public static ByConvention ChordRates { get; } = new(0.25, 0.3, 0.35);

    /// <summary>How many notes a soloist's stacked notes have, its own and the chord's under it: two or three the plainest, four the wilder.</summary>
    public static ImmutableArray<(int Voices, ByConvention Weight)> VoiceOptions { get; } =
    [
        (2, new ByConvention(1, 0.4, 0.3)),
        (3, new ByConvention(1, 0.4, 0.3)),
        (4, new ByConvention(0, 0.2, 0.4))
    ];

    /// <summary>
    ///     The chance a soloist planes its chords, their shape moved with its line note for note rather than the band's chord
    ///     under it, as a pianist moves fourths in parallel: never at the plain end.
    /// </summary>
    public static ByConvention PlaneChance { get; } = new(0, 0.15, 0.6);

    /// <summary>
    ///     How often a soloist that bends bends a note it holds, an 8th or longer, by the sound facet: a third of them in the
    ///     middle, as a rock guitarist bends, most of them at the wild end; leaned by its lean to bends and its solo's intensity.
    /// </summary>
    public static ByConvention BendRates { get; } = new(0, 0.35, 0.85);

    /// <summary>
    ///     The fastest a soloist plays, in notes a second, which its speeds keep under at a song's tempo: a plain one's
    ///     16ths at 120, a middling one's sextuplets at 130, a wild one's 64ths at 105.
    /// </summary>
    public static ByConvention MaxNotesASeconds { get; } = new(8, 13, 28);

    /// <summary>
    ///     The wildest speeds, 32nds in sextuplets and 64ths, of the wild end alone, as a shredder's, each by a chance of its
    ///     own, drawn after the rest of the vocabulary so that it stays as it was.
    /// </summary>
    public static ImmutableArray<(int NotesABeat, ByConvention Chance)> FastestSpeedChances { get; } =
    [
        (12, new ByConvention(0, 0, 0.5)),
        (16, new ByConvention(0, 0, 0.35))
    ];

    /// <summary>
    ///     How long a soloist keeps up its fastest speed, in beats, as a power of two about which each draws its own: a beat
    ///     at the plain end, a few in the middle, and anything from a burst to bars of it at the wild end, as the fastest
    ///     players run on; a slower speed lasts the longer, as many notes as the fastest's.
    /// </summary>
    public static ByConvention StaminaPowers { get; } = new(0, 2, 2.5);

    public static ByConvention StaminaSpread { get; } = new(0, 0.5, 2);

    /// <summary>How long a soloist's phrases are, in bars: two or four the plainest, one the wilder too.</summary>
    public static ImmutableArray<(int Bars, ByConvention Weight)> PhraseBarOptions { get; } =
    [
        (2, new ByConvention(1, 0.5, 0.35)),
        (4, new ByConvention(1, 0.4, 0.3)),
        (1, new ByConvention(0, 0.1, 0.35))
    ];

    /// <summary>The chance a phrase starts off the bar, a beat or two in, as a wild soloist's may: never at the plain end.</summary>
    public static ByConvention OffBarChances { get; } = new(0, 0.05, 0.5);

    /// <summary>The chance a soloist breathes after a phrase's release, a plain one the oftener.</summary>
    public static ByConvention BreathChances { get; } = new(0.8, 0.45, 0.4);

    /// <summary>How many beats the soloist may play a speed at in one run: its stamina at its fastest, the longer the slower.</summary>
    public double BeatsAt(int notesABeat) => Stamina * Speeds[^1] / notesABeat;

    /// <summary>The speeds a soloist may run at, in notes a beat, each by a chance of its own: 8ths and 16ths always, triplets, sextuplets and 32nds the wilder.</summary>
    public static ImmutableArray<(int NotesABeat, ByConvention Chance)> SpeedChances { get; } =
    [
        (2, new ByConvention(1, 1, 1)),
        (4, new ByConvention(1, 0.9, 1)),
        (3, new ByConvention(0, 0.4, 1)),
        (6, new ByConvention(0, 0.5, 0.8)),
        (8, new ByConvention(0, 0.2, 0.5))
    ];

    /// <summary>The sizes of a run's cells, in notes: four and three the plainest, the odd and long ones the wilder.</summary>
    public static ImmutableArray<(int Size, ByConvention Weight)> CellSizeOptions { get; } =
    [
        (4, new ByConvention(1, 0.35, 0.15)),
        (3, new ByConvention(1, 0.24, 0.15)),
        (6, new ByConvention(0, 0.25, 0.2)),
        (5, new ByConvention(0, 0.08, 0.2)),
        (7, new ByConvention(0, 0.08, 0.2)),
        (9, new ByConvention(0, 0, 0.1))
    ];

    /// <summary>How many sizes and how many shapes a soloist favours: one the plainest, three the wilder.</summary>
    public static ImmutableArray<(int Count, ByConvention Weight)> Counts { get; } =
    [
        (1, new ByConvention(1, 0.5, 0.2)),
        (2, new ByConvention(0, 0.35, 0.4)),
        (3, new ByConvention(0, 0.15, 0.4))
    ];

    /// <summary>The chance a soloist groups cells of several sizes into one figure, five and seven say.</summary>
    public static ByConvention GroupingChance { get; } = new(0, 0.3, 1);

    public static ImmutableArray<(SoloShape Shape, ByConvention Weight)> ShapeOptions { get; } =
    [
        (SoloShape.Scale, new ByConvention(1, 0.35, 0.2)),
        (SoloShape.Sequence, new ByConvention(1, 0.25, 0.2)),
        (SoloShape.Arpeggio, new ByConvention(1, 0.2, 0.2)),
        (SoloShape.Turn, new ByConvention(0, 0.12, 0.2)),
        (SoloShape.Pedal, new ByConvention(0, 0.08, 0.2))
    ];

    /// <summary>How often a soloist pauses between its gestures, a plain one breathing the more, about which each draws its own.</summary>
    public static ByConvention PauseRates { get; } = new(0.35, 0.15, 0.1);

    /// <summary>How far and how fast its intensity swings, a plain soloist building steadily and a wild one leaping.</summary>
    public static ByConvention Volatilities { get; } = new(0.1, 0.3, 0.9);

    /// <summary>How wide it ranges, in scale steps, about which each draws its own.</summary>
    public static ByConvention Registers { get; } = new(8, 11, 15);

    /// <summary>How far a soloist leans to bigger bends and to slides, around a lean of its own, wider the wilder its sound.</summary>
    public static ByConvention ExpressionSpread { get; } = new(0, 0.5, 1);

    public const double ExpressionLean = 0.5;

    /// <summary>The chances a soloist bends at all and sings a vibrato at all, by the sound facet: never at the plain end, always at the wild.</summary>
    public static ByConvention BendChance { get; } = new(0, 0.6, 1);

    public static ByConvention VibratoChance { get; } = new(0, 0.85, 1);

    /// <summary>How much likelier a guitar or a synth lead is to bend than another instrument that bends, as the odds of 1.</summary>
    public const double BendSuitOdds = 4;

    /// <summary>How far an instrument leans to bending: a guitar's and a synth lead's the most, as their players do.</summary>
    private static double BendSuit(int program) => InstrumentTraits.Has(program, InstrumentTrait.SoloBends) ? 1 : 0;

    /// <summary>The program of a soloing drummer, none, which neither bends nor plays chords.</summary>
    public const int DrumsProgram = -1;

    /// <param name="program">The instrument the soloing part plays, which leans whether it bends; <see cref="DrumsProgram" /> for the drums.</param>
    public static Soloist Generate(IGenerationContext context, double melody, double sound, int program)
    {
        // every draw made in this order, whatever the instrument
        var speeds = SpeedChances.Where(x => context.TestProbability(x.Chance.At(melody))).Select(x => x.NotesABeat).ToArray();
        var sizes = Favourites(context, CellSizeOptions, melody);
        var groups = context.TestProbability(GroupingChance.At(melody));
        var shapes = Favourites(context, ShapeOptions, melody);
        var pauseRate = PauseRates.At(melody) * (0.5 + context.GenerateDouble());
        var volatility = Volatilities.At(melody) * (0.5 + context.GenerateDouble());
        var register = (int)Math.Round(Registers.At(melody)) + context.GenerateInt(-2, 3);
        var bendLean = ExpressionLean + ExpressionSpread.At(sound) * Generators.SplineValue()(context);
        var slideLean = ExpressionLean + ExpressionSpread.At(sound) * Generators.SplineValue()(context);
        var bends = context.TestProbability(Tilt.Of(BendSuitOdds, BendSuit(program)).Chance(BendChance.At(sound), 1));
        var vibrates = context.TestProbability(VibratoChance.At(sound));
        var fastest = FastestSpeedChances.Where(x => context.TestProbability(x.Chance.At(melody))).Select(x => x.NotesABeat).ToArray();
        var stamina = Math.Pow(2, StaminaPowers.At(melody) + StaminaSpread.At(melody) * Generators.SplineValue()(context));
        var phraseBars = Favourites(context, PhraseBarOptions, melody);
        var chordDraw = context.GenerateDouble();
        var chordRate = InstrumentRoles.PlaysChords(program) ? ChordRates.At(melody) * (0.5 + chordDraw) : 0;
        var voices = context.Pick(ByConvention.Weigh(VoiceOptions, melody));
        var planes = context.TestProbability(PlaneChance.At(melody));
        var octaves = context.TestProbability(OctaveChance.At(melody));
        var approaches = context.TestProbability(ApproachChance.At(melody));
        return new Soloist([..speeds.Concat(fastest).Order()], sizes, groups && sizes.Length > 1, shapes, pauseRate, volatility, register, bendLean, slideLean, MaxNotesASeconds.At(melody), bends, vibrates, stamina,
            phraseBars, OffBarChances.At(melody), BreathChances.At(melody), SoloLines.Gestures.ToImmutableDictionary(x => x.Gesture, x => x.Weight.At(melody)), BendRates.At(sound), chordRate, voices, planes, octaves, approaches, MotifInversionChance.At(melody));
    }

    /// <summary>The options a soloist favours, as many as it draws, each picked from those left.</summary>
    private static ImmutableArray<T> Favourites<T>(IGenerationContext context, ImmutableArray<(T Option, ByConvention Weight)> options, double melody)
    {
        var count = context.Pick(ByConvention.Weigh(Counts, melody));
        var left = options.ToList();
        var favourites = new List<T>();
        while (favourites.Count < count && left.Any(x => x.Weight.At(melody) > 0))
        {
            var favourite = context.Pick(ByConvention.Weigh(left, melody));
            favourites.Add(favourite);
            left.RemoveAll(x => EqualityComparer<T>.Default.Equals(x.Option, favourite));
        }

        return [..favourites];
    }
}
