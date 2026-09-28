using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     How a melody moves, drawn by layer: the melody instrument sets how stepwise the song's melody is, a section
///     moves it, and a section draws the shape its phrases take. Every note then draws where it means to go, from its
///     bar pattern's seed, so that a bar that comes back comes back with its shape.
/// </summary>
public static class MelodyLayers
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

    /// <summary>The chance of a note meaning to leap, at no stepwiseness; less the more stepwise the melody is.</summary>
    public const double MaxLeapChance = 0.4;

    /// <summary>The chance of a note meaning to stay on the note before.</summary>
    public const double RepeatChance = 0.2;

    /// <summary>The chance of a moving note going on the way the melody goes, rather than turning back.</summary>
    public const double ContinueChance = 0.85;

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
    ///     amount: its first half as the question, the classic answer's start, and its second half changed.
    /// </summary>
    public static ImmutableArray<double> AnswerBars { get; } = [0, 0, 1, 1];

    /// <summary>The chance a note of the answer's changing bars is mutated, with no lean; the less conventional the rhythm, the likelier.</summary>
    public const double AnswerAmount = 0.5;

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
    public static ImmutableArray<double> GenerateContour(IGenerationContext context, IGenerationContext periodContext, Tilt tilt)
    {
        var peak = PeakBars[PeakBarGenerator(context)].Value;
        var slope = SlopeGenerator(context);
        var periods = tilt.Weigh(Periods, x => x < Progressions.BarCount ? 1 : 0);
        var period = periods[Generators.WeightedIndex(periods)(periodContext)].Value;
        return [..Enumerable.Range(0, Progressions.BarCount).Select(bar => PeakRegister - slope * Math.Abs(bar % period - peak % period))];
    }

    /// <summary>A layer's shift of the stepwiseness, up to the given size either way.</summary>
    public static Func<IGenerationContext, double> CreateGenerator(double size)
    {
        return Generators.SplineValue().Then(x => x * size);
    }

    /// <summary>
    ///     How much likelier a note is to go on towards where its phrase aims, and to turn back rather than go on away from
    ///     it, as the odds at <see cref="MelodyLine.RegisterPull" /> semitones from it, less the nearer it is.
    /// </summary>
    public const double AimOdds = 1;

    /// <summary>
    ///     Where a note means to go: staying (0), a step (1), or a leap (2), the less likely the more stepwise the melody
    ///     is; and its draw of whether it goes on the way the melody goes or turns back, from 0 to 1, which goes on below
    ///     <see cref="ContinueChance" /> as the aim leans it (<see cref="GetContinueChance" />), so that it runs up or
    ///     down a while before it turns.
    /// </summary>
    public static (int Step, double Turn) GenerateStep(IGenerationContext context, double stepwiseness)
    {
        if (context.TestProbability(RepeatChance))
            return (0, 0);

        var turn = context.GenerateDouble();
        var leapChance = (1 - Math.Clamp(stepwiseness, 0, 1)) * MaxLeapChance;
        return (context.TestProbability(leapChance) ? 2 : 1, turn);
    }

    /// <summary>
    ///     The chance a note goes on the way the melody goes, leaning to go on towards where its phrase aims and to turn
    ///     back from going on away from it, the more the further it is, up to <see cref="MelodyLine.RegisterPull" />.
    /// </summary>
    /// <param name="towardsAim">How far going on moves towards the aim, in semitones; negative away from it.</param>
    public static double GetContinueChance(double towardsAim)
    {
        return Tilt.Of(AimOdds, 1).Chance(ContinueChance, Math.Clamp(towardsAim / MelodyLine.RegisterPull, -1, 1));
    }
}
