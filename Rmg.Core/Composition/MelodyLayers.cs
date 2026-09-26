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

    /// <summary>How long the melody rests before its next phrase, in beats.</summary>
    public const double PhraseEndRest = 1;

    /// <summary>
    ///     The shapes a phrase takes, as the register it aims at in each of its four bars, in semitones above or below
    ///     the middle of the melody's range, and how likely each is.
    /// </summary>
    public static ImmutableArray<Weighted<ImmutableArray<double>>> Contours { get; } =
    [
        // up and back down
        new(0.4, [-2, 3, 5, 0]),
        new(0.25, [5, 2, -1, -4]),
        new(0.15, [-4, -1, 2, 5]),
        // up and down and up
        new(0.2, [0, 4, -2, 2])
    ];

    /// <summary>A layer's shift of the stepwiseness, up to the given size either way.</summary>
    public static Func<IGenerationContext, double> CreateGenerator(double size)
    {
        return Generators.SplineValue().Then(x => x * size);
    }

    /// <summary>
    ///     Where a note means to go, from the way the melody goes: on (positive) more often than back (negative), so
    ///     that it runs up or down a while before it turns, and a leap (2) the less likely the more stepwise it is.
    /// </summary>
    public static int GenerateStep(IGenerationContext context, double stepwiseness)
    {
        if (context.TestProbability(RepeatChance))
            return 0;

        var direction = context.TestProbability(ContinueChance) ? 1 : -1;
        var leapChance = (1 - Math.Clamp(stepwiseness, 0, 1)) * MaxLeapChance;
        return context.TestProbability(leapChance) ? 2 * direction : direction;
    }
}
