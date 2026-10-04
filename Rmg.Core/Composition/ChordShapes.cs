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
)
{
    /// <summary>How it sounds, in a sentence, which the page shows over its name.</summary>
    public required string Description { get; init; }
}

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
        new("Triad", 0, 1, [0, Third, 7]) { Description = "Root, third and fifth: the plain chord." },

        new("Power chord", 1, 0.8, [0, 7]) { Description = "Root and fifth, no third: neither major nor minor, as rock plays it." },
        new("Sus2", 1, 0.6, [0, 2, 7]) { Description = "The second for the third: open and unresolved." },
        new("Sus4", 1, 0.6, [0, 5, 7]) { Description = "The fourth for the third: leaning to fall back to the third." },
        new("Seventh", 1, 1, [0, Third, 7, Seventh]) { Description = "A triad with the seventh: fuller, and on the fifth the pull home." },
        new("Add9", 1, 0.7, [0, Third, 7, 14]) { Description = "A triad with the ninth: bright and open, no seventh." },
        new("Sixth", 1, 0.5, [0, Third, 7, Sixth]) { Description = "A triad with the sixth: sweet, as swing and old pop play it." },

        new("Ninth", 2, 1, [0, Third, 7, Seventh, 14]) { Description = "A seventh chord with the ninth: smooth and jazzy." },
        new("Six-nine", 2, 0.6, [0, Third, 7, Sixth, 14]) { Description = "A triad with the sixth and ninth: lush and settled, a jazz ending." },
        new("Seven-sus4", 2, 0.7, [0, 5, 7, Seventh]) { Description = "A seventh with the fourth for the third: a soft dominant, as gospel and soul play it." },
        new("Shell", 2, 0.8, [0, Third, Seventh]) { Description = "Root, third and seventh, no fifth: the lean voicing of jazz piano." },
        new("Quartal triad", 2, 0.5, [0, 5, 10]) { Description = "Two fourths stacked: open and modern, no third." },
        // a triad with its second close under the third, as jazz-tinged pop plays it
        new("Mu", 2, 0.5, [0, 2, Third, 7]) { Description = "A triad with the second close under the third: a glassy colour." },
        new("Add11", 2, 0.5, [0, Third, 7, 17]) { Description = "A triad with the eleventh: the fourth rubbing on the third." },
        new("Nine-sus4", 2, 0.6, [0, 5, 7, Seventh, 14]) { Description = "A ninth with the fourth for the third: smooth and floating." },

        new("Eleventh", 3, 0.8, [0, Third, 7, Seventh, 14, 17]) { Description = "Thirds stacked up to the eleventh: full and ambiguous." },
        new("Thirteenth", 3, 0.8, [0, Third, Seventh, 14, Thirteenth]) { Description = "Third, seventh, ninth and thirteenth: the richest dominant." },
        new("Quartal stack", 3, 0.6, [0, 5, 10, 15, 20], true) { Description = "Four fourths stacked: modal jazz, open and suspended." },
        new("So What", 3, 0.5, [0, 5, 10, 15, 19], true) { Description = "Three fourths and a third on top: the voicing of modal jazz." },
        new("Lydian", 3, 0.6, [0, 4, 7, 11, 18]) { Description = "A major seventh with the sharp eleventh: bright and floating." },

        new("Augmented", 4, 0.8, [0, 4, 8]) { Description = "Two major thirds, the fifth raised: restless and dreamlike." },
        new("Diminished seventh", 4, 0.8, [0, 3, 6, 9]) { Description = "Minor thirds stacked, the octave split evenly: tense, suspense." },
        new("Seven-flat-nine", 4, 0.3, [0, 4, 10, 13]) { Description = "A dominant seventh with the flat ninth: dark, pulling hard home." },
        new("Seven-sharp-nine", 4, 0.3, [0, 4, 10, 15]) { Description = "A dominant seventh with the sharp ninth: major and minor at once, gritty." },
        new("Phrygian", 4, 0.6, [0, 1, 7]) { Description = "Root, flat second and fifth: dark and Spanish." },
        new("Small cluster", 4, 0.6, [0, 2, 3], true) { Description = "Three notes a step or less apart: a soft rub." },
        // a minor triad with a major seventh, and a dominant with a sharp ninth and a flat thirteenth, each where the
        // scale has its notes, and the nearest of the scale's where it has not
        new("Minor-major seventh", 4, 0.4, [0, 3, 7, 11]) { Description = "A minor triad with a major seventh: suspense, film noir." },
        new("Altered dominant", 4, 0.3, [0, 4, 10, 15, 20]) { Description = "A dominant with the sharp ninth and flat thirteenth: tense jazz." },

        new("Dense cluster", 5, 0.8, [0, 1, 2, 3], true) { Description = "Four notes a half step apart: a smear of sound." },
        new("Viennese trichord", 5, 0.6, [0, 1, 6]) { Description = "A half step and a tritone: atonal, of the twentieth century." },
        new("Polychord", 5, 0.5, [0, 4, 7, 14, 18, 21], true) { Description = "A major triad with another a step above: two keys at once." },
        new("Mystic chord", 5, 0.4, [0, 6, 10, 16, 21, 26], true) { Description = "Fourths stacked, some of them augmented: shimmering and mysterious." },
        new("Tritone stack", 5, 0.5, [0, 6, 11, 17]) { Description = "Tritones and fourths: harsh and unsettled." },
        // the minor and the major third together, as the blues plays them
        new("Split third", 5, 0.4, [0, 3, 4, 7], true) { Description = "The minor and the major third together: the blues' clash." }
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
        new(0.3, Named("Ninth")),
        new(0.3, Named("Nine-sus4"))
    ];

    private static readonly Func<IGenerationContext, int> CadenceShapeIndexGenerator = Generators.WeightedIndex(CadenceShapes);

    /// <summary>
    ///     How likely a chord of each level is, by the chords' unconventionality: the plainest song plays triads alone; a
    ///     wild one no triads or level 1, the stranger the likelier; the tuned
    ///     weights a middling song's, as most songs played before the facets, mostly triads and level 1, so that the jazz
    ///     and the clusters are the wilder songs'.
    /// </summary>
    public static ImmutableArray<(int Level, ByConvention Weight)> Levels { get; } =
    [
        (0, new ByConvention(1, 0.55, 0)),
        (1, new ByConvention(0, 0.38, 0)),
        (2, new ByConvention(0, 0.05, 0.15)),
        (3, new ByConvention(0, 0.015, 0.4)),
        (4, new ByConvention(0, 0.004, 0.8)),
        (5, new ByConvention(0, 0.001, 1))
    ];

    /// <summary>
    ///     How likely a home chord of each level is, which stays plain for the release but in the wildest songs: a triad
    ///     in the plainest, a triad or a colour of level 1 about the middle, and at the wild end the strangest levels.
    /// </summary>
    public static ImmutableArray<(int Level, ByConvention Weight)> HomeLevels { get; } =
    [
        (0, new ByConvention(1, 0.6, 0)),
        (1, new ByConvention(0, 0.4, 0)),
        (3, new ByConvention(0, 0, 0.3)),
        (4, new ByConvention(0, 0, 0.6)),
        (5, new ByConvention(0, 0, 1))
    ];

    /// <summary>
    ///     How likely a cadence is to play a triad, as the plainest song does, a shape made for it (none for its level),
    ///     which pulls towards home, or one of the strangest levels, which keeps a strange song's strangeness there; the
    ///     tuned weights a middling song's.
    /// </summary>
    public static ImmutableArray<(int? Level, ByConvention Weight)> CadenceLevels { get; } =
    [
        (0, new ByConvention(1, 0, 0)),
        (null, new ByConvention(0, 0.97, 0)),
        (3, new ByConvention(0, 0.02, 0.3)),
        (4, new ByConvention(0, 0.007, 0.6)),
        (5, new ByConvention(0, 0.003, 1))
    ];

    /// <summary>
    ///     A shape of a level drawn from those given at the chords' unconventionality, or, within a genre's pool of the
    ///     shapes, a shape drawn at once among the levels' shapes, each weighing its level's weight times its share of it:
    ///     the pool's weights a chord's (<see cref="ByConvention.Weigh{T}(IEnumerable{ValueTuple{T, ByConvention}}, double, Func{T, ByConvention?})" />),
    ///     and leaning a home's own (<see cref="ByConvention.Lean{T}" />), which stays plainer.
    /// </summary>
    /// <param name="isOwn">Whether the levels are a role's own, such as a home's, which the pool leans rather than sets.</param>
    /// <summary>A genre's pool of the shapes: every shape's weights at its ends, and those beside the songs', which a role's own lean by.</summary>
    public sealed record GenrePool(Func<ChordShape, ByConvention?> Middle, Func<ChordShape, (ByConvention Pool, ByConvention Songs)?> Lean);

    public static ChordShape Pick(IGenerationContext context, ImmutableArray<(int Level, ByConvention Weight)> levels, double unconventionality, GenrePool? pool, bool isOwn)
    {
        if (pool is null)
            return Pick(context, context.Pick(ByConvention.Weigh(levels.Select(x => (x.Level, x.Weight)), unconventionality)));

        var shapes = levels.SelectMany(x => Shares(OfLevel(x.Level), x.Weight));
        return context.Pick(isOwn ? ByConvention.Lean(shapes, unconventionality, pool.Lean) : ByConvention.Weigh(shapes, unconventionality, pool.Middle));
    }

    /// <summary>
    ///     A cadence's shape at the chords' unconventionality: one made for it, or one of a strange level; within a genre's
    ///     pool, a shape drawn at once among them, the pool leaning the cadence's own weights.
    /// </summary>
    public static ChordShape PickCadence(IGenerationContext context, double unconventionality, GenrePool? pool)
    {
        if (pool is not null)
            return context.Pick(ByConvention.Lean(
                CadenceLevels.SelectMany(x => Shares(x.Level is { } level ? OfLevel(level) : CadenceShapes, x.Weight)),
                unconventionality,
                pool.Lean
            ));

        var level = context.Pick(ByConvention.Weigh(CadenceLevels.Select(x => (x.Level, x.Weight)), unconventionality));
        return level is { } strange ? Pick(context, strange) : PickCadence(context);
    }

    /// <summary>Every shape's weight at each end: its group's times its share of the group, by its weight in it.</summary>
    private static IEnumerable<(ChordShape Shape, ByConvention Weight)> Shares(IEnumerable<Weighted<ChordShape>> shapes, ByConvention group)
    {
        var all = shapes.ToArray();
        var total = all.Sum(x => x.Weight);
        return all.Select(x => (x.Value, new ByConvention(group.Plain * x.Weight / total, group.Tuned * x.Weight / total, group.Wild * x.Weight / total)));
    }

    /// <summary>A level's shapes, by their weights.</summary>
    private static IEnumerable<Weighted<ChordShape>> OfLevel(int level) => ShapesByUnconventionality[level].Select(x => new Weighted<ChordShape>(x.Weight, x));

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
