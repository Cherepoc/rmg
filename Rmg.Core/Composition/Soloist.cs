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

    /// <summary>The chord's notes, broken.</summary>
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
///     to slides. Drawn once for every part that solos in a song, by the song's melody facet, its expression by the sound
///     facet: at the plain end 8ths and 16ths in cells of three or four, the scale, a sequence or the chord, and a steady
///     player; at the wild end any speed, cells of five, seven or nine grouped, and one who swings from runs to long notes
///     in a beat.
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
    double SlideLean
)
{
    /// <summary>The speeds a soloist may run at, in notes a beat, each by a chance of its own: 8ths and 16ths always, triplets, sextuplets and 32nds the wilder.</summary>
    public static ImmutableArray<(int NotesABeat, ByConvention Chance)> SpeedChances { get; } =
    [
        (2, new ByConvention(1, 1, 1)),
        (4, new ByConvention(1, 0.9, 1)),
        (3, new ByConvention(0, 0.4, 1)),
        (6, new ByConvention(0, 0.2, 0.8)),
        (8, new ByConvention(0, 0.05, 0.5))
    ];

    /// <summary>The sizes of a run's cells, in notes: four and three the plainest, the odd and long ones the wilder.</summary>
    public static ImmutableArray<(int Size, ByConvention Weight)> CellSizeOptions { get; } =
    [
        (4, new ByConvention(1, 0.5, 0.15)),
        (3, new ByConvention(1, 0.35, 0.15)),
        (6, new ByConvention(0, 0.1, 0.2)),
        (5, new ByConvention(0, 0.03, 0.2)),
        (7, new ByConvention(0, 0.02, 0.2)),
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
    public static ByConvention GroupingChance { get; } = new(0, 0.15, 1);

    public static ImmutableArray<(SoloShape Shape, ByConvention Weight)> ShapeOptions { get; } =
    [
        (SoloShape.Scale, new ByConvention(1, 0.35, 0.2)),
        (SoloShape.Sequence, new ByConvention(1, 0.25, 0.2)),
        (SoloShape.Arpeggio, new ByConvention(1, 0.2, 0.2)),
        (SoloShape.Turn, new ByConvention(0, 0.12, 0.2)),
        (SoloShape.Pedal, new ByConvention(0, 0.08, 0.2))
    ];

    /// <summary>How often a soloist pauses between its gestures, a plain one breathing the more, about which each draws its own.</summary>
    public static ByConvention PauseRates { get; } = new(0.35, 0.25, 0.1);

    /// <summary>How far and how fast its intensity swings, a plain soloist building steadily and a wild one leaping.</summary>
    public static ByConvention Volatilities { get; } = new(0.1, 0.3, 0.9);

    /// <summary>How wide it ranges, in scale steps, about which each draws its own.</summary>
    public static ByConvention Registers { get; } = new(8, 11, 15);

    /// <summary>How far a soloist leans to bigger bends and to slides, around a lean of its own, wider the wilder its sound.</summary>
    public static ByConvention ExpressionSpread { get; } = new(0, 0.5, 1);

    public const double ExpressionLean = 0.5;

    public static Soloist Generate(IGenerationContext context, double melody, double sound)
    {
        var speeds = SpeedChances.Where(x => context.TestProbability(x.Chance.At(melody))).Select(x => x.NotesABeat).Order();
        var sizes = Favourites(context, CellSizeOptions, melody);
        var groups = context.TestProbability(GroupingChance.At(melody));
        var shapes = Favourites(context, ShapeOptions, melody);
        var pauseRate = PauseRates.At(melody) * (0.5 + context.GenerateDouble());
        var volatility = Volatilities.At(melody) * (0.5 + context.GenerateDouble());
        var register = (int)Math.Round(Registers.At(melody)) + context.GenerateInt(-2, 3);
        var bendLean = ExpressionLean + ExpressionSpread.At(sound) * Generators.SplineValue()(context);
        var slideLean = ExpressionLean + ExpressionSpread.At(sound) * Generators.SplineValue()(context);
        return new Soloist([..speeds], sizes, groups && sizes.Length > 1, shapes, pauseRate, volatility, register, bendLean, slideLean);
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
