using System.Collections.Immutable;
using System.Diagnostics;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     A chord, as the heights of its notes above the root, in semitones, in close position. <c>Render</c> snaps every
///     height to the nearest note of the scale, so a height can sit between two qualities and leave the choice to the
///     scale, as the third does between a minor and a major third.
/// </summary>
/// <param name="Unconventionality">
///     How far the chord strays from convention, from 0 for a triad to <see cref="ChordShapes.MaxUnconventionality" />.
/// </param>
/// <param name="Weight">How likely the chord is among the others of its unconventionality.</param>
/// <param name="IsVoicingFixed">Whether the chord's layout is what it is, so that it is never revoiced.</param>
[DebuggerDisplay("ChordShape {Name}")]
public sealed record ChordShape(
    string Name,
    int Unconventionality,
    double Weight,
    ImmutableArray<double> Targets,
    bool IsVoicingFixed = false
);

internal static class ChordShapes
{
    public const int MaxUnconventionality = 5;

    // heights that leave the quality to the scale: steps of the octave divided evenly into seven, which fall between
    // the two qualities a 7-note scale can give, such as a minor and a major third
    private const double Third = 12.0 * 2 / 7;
    private const double Sixth = 12.0 * 5 / 7;
    private const double Seventh = 12.0 * 6 / 7;
    private const double Thirteenth = 12 + Sixth;

    public static ImmutableArray<ChordShape> All { get; } =
    [
        new("Triad", 0, 1, [0, Third, 7]),

        new("Power chord", 1, 0.8, [0, 7]),
        new("Sus2", 1, 0.6, [0, 2, 7]),
        new("Sus4", 1, 0.6, [0, 5, 7]),
        new("Seventh", 1, 1, [0, Third, 7, Seventh]),
        new("Add9", 1, 0.7, [0, Third, 7, 14]),
        new("Sixth", 1, 0.5, [0, Third, 7, Sixth]),

        new("Ninth", 2, 1, [0, Third, 7, Seventh, 14]),
        new("Six-nine", 2, 0.6, [0, Third, 7, Sixth, 14]),
        new("Seven-sus4", 2, 0.7, [0, 5, 7, Seventh]),
        new("Shell", 2, 0.8, [0, Third, Seventh]),
        new("Quartal triad", 2, 0.5, [0, 5, 10]),

        new("Eleventh", 3, 0.8, [0, Third, 7, Seventh, 14, 17]),
        new("Thirteenth", 3, 0.8, [0, Third, Seventh, 14, Thirteenth]),
        new("Quartal stack", 3, 0.6, [0, 5, 10, 15, 20], true),
        new("So What", 3, 0.5, [0, 5, 10, 15, 19], true),
        new("Lydian", 3, 0.6, [0, 4, 7, 11, 18]),

        new("Augmented", 4, 0.8, [0, 4, 8]),
        new("Diminished seventh", 4, 0.8, [0, 3, 6, 9]),
        new("Seven-flat-nine", 4, 0.3, [0, 4, 10, 13]),
        new("Seven-sharp-nine", 4, 0.3, [0, 4, 10, 15]),
        new("Phrygian", 4, 0.6, [0, 1, 7]),
        new("Small cluster", 4, 0.6, [0, 2, 3], true),

        new("Dense cluster", 5, 0.8, [0, 1, 2, 3], true),
        new("Viennese trichord", 5, 0.6, [0, 1, 6]),
        new("Polychord", 5, 0.5, [0, 4, 7, 14, 18, 21], true),
        new("Mystic chord", 5, 0.4, [0, 6, 10, 16, 21, 26], true),
        new("Tritone stack", 5, 0.5, [0, 6, 11, 17])
    ];

    private static readonly ImmutableArray<ImmutableArray<ChordShape>> ShapesByUnconventionality =
    [
        ..Enumerable.Range(0, MaxUnconventionality + 1).Select(level => All.Where(x => x.Unconventionality == level).ToImmutableArray())
    ];

    private static readonly ImmutableArray<Func<IGenerationContext, int>> ShapeIndexGenerators =
    [
        ..ShapesByUnconventionality.Select(shapes => Generators.WeightedIndex([..shapes.Select(x => new Weighted<ChordShape>(x.Weight, x))]))
    ];

    /// <summary>
    ///     The shapes that end a phrase with pull towards home, and how likely each is: the seventh most, which on the
    ///     fifth is the dominant seventh, then the suspended ones, which resolve by a step, and the plain triad.
    /// </summary>
    private static readonly ImmutableArray<Weighted<ChordShape>> CadenceShapes =
    [
        new(1, Named("Seventh")),
        new(0.6, Named("Seven-sus4")),
        new(0.5, Named("Sus4")),
        new(0.5, Named("Triad")),
        new(0.3, Named("Ninth"))
    ];

    private static readonly Func<IGenerationContext, int> CadenceShapeIndexGenerator = Generators.WeightedIndex(CadenceShapes);

    /// <summary>
    ///     How likely a chord of each level is, by the chords' unconventionality: a plain song plays triads and the colours
    ///     of level 1, a ninth or a six-nine now and then; a wild one no triads or level 1, the stranger the likelier; the
    ///     tuned weights the corpus's chords as its anchor drew them.
    /// </summary>
    public static ImmutableArray<(int Level, ByConvention Weight)> Levels { get; } =
    [
        (0, new ByConvention(1, 0.35, 0)),
        (1, new ByConvention(0.6, 0.35, 0)),
        (2, new ByConvention(0.1, 0.13, 0.15)),
        (3, new ByConvention(0, 0.10, 0.4)),
        (4, new ByConvention(0, 0.055, 0.8)),
        (5, new ByConvention(0, 0.011, 1))
    ];

    /// <summary>
    ///     How likely a home chord of each level is, which stays plain for the release but in the wildest songs: a triad
    ///     or a colour of level 1, and at the wild end the strangest levels.
    /// </summary>
    public static ImmutableArray<(int Level, ByConvention Weight)> HomeLevels { get; } =
    [
        (0, new ByConvention(1, 0.37, 0)),
        (1, new ByConvention(0.4, 0.63, 0)),
        (3, new ByConvention(0, 0, 0.3)),
        (4, new ByConvention(0, 0, 0.6)),
        (5, new ByConvention(0, 0, 1))
    ];

    /// <summary>
    ///     How likely a cadence is to play a shape made for it (none for its level), which pulls towards home, or one of the
    ///     strangest levels, which keeps a strange song's strangeness there; the tuned weights the corpus's cadences.
    /// </summary>
    public static ImmutableArray<(int? Level, ByConvention Weight)> CadenceLevels { get; } =
    [
        (null, new ByConvention(1, 0.86, 0)),
        (3, new ByConvention(0, 0.095, 0.3)),
        (4, new ByConvention(0, 0.034, 0.6)),
        (5, new ByConvention(0, 0.015, 1))
    ];

    /// <summary>A shape of a level drawn from those given at the chords' unconventionality.</summary>
    public static ChordShape Pick(IGenerationContext context, ImmutableArray<(int Level, ByConvention Weight)> levels, double unconventionality)
    {
        return Pick(context, context.Pick(ByConvention.Weigh(levels.Select(x => (x.Level, x.Weight)), unconventionality)));
    }

    /// <summary>A cadence's shape at the chords' unconventionality: one made for it, or one of a strange level.</summary>
    public static ChordShape PickCadence(IGenerationContext context, double unconventionality)
    {
        var level = context.Pick(ByConvention.Weigh(CadenceLevels.Select(x => (x.Level, x.Weight)), unconventionality));
        return level is { } strange ? Pick(context, strange) : PickCadence(context);
    }

    /// <summary>A shape of the unconventionality, the heavier ones being more likely.</summary>
    public static ChordShape Pick(IGenerationContext context, int unconventionality)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(unconventionality);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(unconventionality, MaxUnconventionality);

        return ShapesByUnconventionality[unconventionality][ShapeIndexGenerators[unconventionality](context)];
    }

    /// <summary>A shape that ends a phrase with pull towards home, the heavier ones being more likely.</summary>
    private static ChordShape PickCadence(IGenerationContext context)
    {
        return CadenceShapes[CadenceShapeIndexGenerator(context)].Value;
    }

    private static ChordShape Named(string name)
    {
        return All.Single(x => x.Name == name);
    }
}
