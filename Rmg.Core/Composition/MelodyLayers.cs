using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     How a melody moves, drawn by layer: the melody instrument sets how stepwise the song's melody is, a section
///     moves it, and a section draws the shape its phrases take. Every note then draws where it means to go, from its
///     bar pattern's seed, so that a bar that comes back comes back with its shape.
/// </summary>
internal static class MelodyLayers
{
    /// <summary>Winds, strings and leads that sing, which move by step.</summary>
    public const double Stepwise = 0.8;

    /// <summary>Keyboards and guitars, in between.</summary>
    public const double Mixed = 0.6;

    /// <summary>Mallets and the like, which leap more.</summary>
    public const double Leaping = 0.4;

    /// <summary>How far the song moves away from its instrument's stepwiseness, either way.</summary>
    public const double Song = 0.15;

    /// <summary>How far a section moves away from the song's stepwiseness, either way.</summary>
    public const double Section = 0.3;

    /// <summary>How the melody moves, and what its strong beats take, as a line (<see cref="LineProfile" />).</summary>
    public static LineProfile Line { get; } = new(
        RangeWidth: 17,
        RegisterPull: 7,
        LeapSize: 7,
        StrongestWeakRank: 1,
        RepeatChance: 0.2,
        MaxLeapChance: 0.4,
        ContinueChance: 0.85,
        AimOdds: 16,
        RegisterFreedom: 0.25,
        RegisterFreedomSpread: 0.25,
        ContourShare: 1,
        ImprovisationShare: 1
    );

    /// <summary>How much less often the melody's cycles are drawn afresh than the other tracks', so it plays riffs.</summary>
    public const double RhythmVariation = -0.2;

    /// <summary>
    ///     Where a phrase ends in its last bar, as the beat before which its last note starts, and how likely each is;
    ///     0 for a phrase that runs on into the next. The last note is held until <see cref="PhraseEndRest" /> before
    ///     the bar line, so the earlier it starts the longer it is.
    /// </summary>
    public static ImmutableArray<Weighted<int>> PhraseEnds { get; } =
    [
        new(0.2, 0),
        new(0.4, 1),
        new(0.3, 2),
        new(0.1, 3)
    ];

    /// <summary>
    ///     How much of the answer's every bar, the section's second 4 bars, is mutated from the question's, by the answer
    ///     amount: its first half as the question, the classic answer's start, and its second half changed; where a
    ///     phrase varies, as an improvised appearance's rhythm does too.
    /// </summary>
    public static ImmutableArray<double> AnswerBars { get; } = [0, 0, 0.8, 0.8];

    /// <summary>
    ///     How likely an answer's changing bar is to draw its rhythm afresh, over the bar's share of the answer
    ///     (<see cref="AnswerBars" />) and the answer's amount: half the time at the amount with no lean.
    /// </summary>
    public const double AnswerRhythm = 1.25;

    /// <summary>The chance a note of the answer's changing bars is mutated, at the middle of the melody facet.</summary>
    public const double AnswerAmount = 0.5;

    /// <summary>The chance a note of the answer's changing bars is mutated, by the melody facet: never at the plain end, every note at the wild.</summary>
    public static ByConvention AnswerAmounts { get; } = RhythmicUnconventionality.Ends(AnswerAmount, 1);

    /// <summary>
    ///     The chance the melody leads into a chord change, its last note before the change a step from
    ///     the note it changes to, with no spread; a section's is this spread by <see cref="LeadingSpread" /> either way,
    ///     so that some sections hardly lead, as a riff, and some lead most changes.
    /// </summary>
    public const double Leading = 0.5;

    /// <summary>How far a section's chance of leading spreads from <see cref="Leading" />, either way.</summary>
    public const double LeadingSpread = 0.5;

    /// <summary>
    ///     How much a song improvises its melody as its sections recur: the chance a note of a section's later appearance
    ///     is mutated from its first, as a singer varies a tune each time, drawn per song as a value around 0 of up to
    ///     twice this either way, spread evenly rather than gathered near 0, and none below 0, so that about half the
    ///     songs play their sections' melodies as first heard, as riffs do; the less conventional the song's rhythm, the
    ///     more it leans up.
    /// </summary>
    public const double Improvisation = 0.3;

    /// <summary>
    ///     How much more a song improvises by its end: the odds of its improvisation times this to the power of how far
    ///     into the song an appearance falls, from 0 at its start to 1 at its end, so that the last chorus, and a fade's,
    ///     varies most, as a singer ad-libs more as a song goes on; a song that does not improvise stays as it is.
    /// </summary>
    public const double ImprovisationGrowth = 3;

    /// <summary>
    ///     How likely a bar of a section's later appearance is to draw its melody's rhythm afresh, over the song's
    ///     improvisation and where a phrase varies (<see cref="AnswerBars" />).
    /// </summary>
    public const double ImprovisedRhythm = 2;

    /// <summary>
    ///     How far a song's improvisation reaches, by the melody facet: none at the plain end, whose sections come back as
    ///     first heard, as tuned at the middle, and the whole range at the wild end.
    /// </summary>
    public static ByConvention ImprovisationReach { get; } = new(0, Improvisation, 1);

    /// <summary>A song's improvisation (<see cref="Improvisation" />), by the melody facet of its unconventionality.</summary>
    public static double GenerateImprovisation(IGenerationContext context, double unconventionality)
    {
        var tilt = new RhythmicUnconventionality(unconventionality).Tilt;
        var improvisation = tilt.SplineValue(0)(context) * ImprovisationReach.At(unconventionality);
        // none below 0, and none at all where the reach is none, not a negative zero
        return improvisation > 0 ? Math.Min(improvisation, 1) : 0;
    }

    /// <summary>How long the melody rests before its next phrase, in beats.</summary>
    public const double PhraseEndRest = 1;

    /// <summary>
    ///     The bar a phrase peaks in, and how likely each is: most often its third, an arch that rises and comes back
    ///     down, then its first, a phrase that falls from its start, then its last, one that rises to its end.
    /// </summary>
    public static ImmutableArray<Weighted<int>> PeakBars { get; } = [new(0.25, 0), new(0.1, 1), new(0.45, 2), new(0.2, 3)];

    /// <summary>The register a phrase aims at in the bar it peaks in, in semitones above the middle of the melody's range.</summary>
    public const double PeakRegister = 5;

    /// <summary>How far below its peak a phrase aims for every bar away from it, in semitones, and how far that spreads either way.</summary>
    public const double Slope = 3;

    public const double SlopeSpread = 1;

    /// <summary>
    ///     How many bars a phrase's shape takes before it starts again, and how likely each is: mostly the whole phrase,
    ///     and now and then half of it, a wave that rises and falls twice; the shorter leans unconventional.
    /// </summary>
    public static ImmutableArray<Weighted<int>> Periods { get; } = [new(0.8, Progressions.BarCount), new(0.2, Progressions.BarCount / 2)];

    private static readonly Func<IGenerationContext, int> PeakBarGenerator = Generators.WeightedIndex(PeakBars);

    private static readonly Func<IGenerationContext, double> SlopeGenerator = Generators.SplineValue().Then(x => Slope + x * SlopeSpread);

    /// <summary>
    ///     The shape a phrase takes, as the register it aims at in each of its four bars: its peak in the bar drawn, and
    ///     lower by the slope drawn for every bar away from it, so that it rises to its peak and falls from it; the shape
    ///     starts again every period, drawn from its own sequence, so that a period of half the phrase makes a wave.
    /// </summary>
    /// <param name="periodContext">The sequence the period is drawn from.</param>
    /// <param name="tilt">How unconventional the section is, which leans the shorter period.</param>
    /// <param name="unconventionality">The melody facet of the section's unconventionality, whose ends the contour's periods lean by.</param>
    public static ImmutableArray<double> GenerateContour(IGenerationContext context, IGenerationContext periodContext, double unconventionality)
    {
        var peak = PeakBars[PeakBarGenerator(context)].Value;
        var slope = SlopeGenerator(context);
        var periods = ByConvention.Weigh(Periods.Select(x => (x.Value, RhythmicUnconventionality.WeightEnds(x.Weight, x.Value < Progressions.BarCount ? 1 : 0))), unconventionality);
        var period = periodContext.Pick(periods);
        return [..Enumerable.Range(0, Progressions.BarCount).Select(bar => PeakRegister - slope * Math.Abs(bar % period - peak % period))];
    }

    /// <summary>A layer's shift of the stepwiseness, up to the given size either way.</summary>
    public static Func<IGenerationContext, double> CreateGenerator(double size)
    {
        return Generators.SplineValue().Then(x => x * size);
    }
}
