using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>The notes a line's passing notes take, over each chord it plays over.</summary>
public enum LineScale
{
    /// <summary>The section's scale, all of it.</summary>
    Section,

    /// <summary>
    ///     The key's major or minor pentatonic, as the mode's third is, kept to the section's scale; all of the scale where it
    ///     has more than one tritone (<see cref="LineScales.Classes" />).
    /// </summary>
    Pentatonic,

    /// <summary>The blues scale on the chord's root: its minor third, fourth, flat fifth, fifth and minor seventh.</summary>
    Blues,

    /// <summary>Melodic minor on the chord's root.</summary>
    MelodicMinor,

    /// <summary>The whole-tone scale on the chord's root.</summary>
    WholeTone,

    /// <summary>The octatonic scale on the chord's root, a half step and a whole step in turn.</summary>
    Octatonic,

    /// <summary>All twelve notes.</summary>
    Chromatic,

    /// <summary>
    ///     The pentatonic union on the key's tonic, both thirds over the major pentatonic with the fourth and the flat seventh,
    ///     as a rock melody bends between its major and minor third: a genre's alone.
    /// </summary>
    Union
}

/// <summary>
///     A line's scale: the notes a melody's or a riff's passing notes take, its strong beats taking its chord's notes as
///     they do, where the line takes them. The plainest lines take the section's scale, or most often its pentatonic;
///     the wildest a scale of their own over every chord, measured from its root rather than the key, as a fusion player
///     thinks of them: melodic minor, whole-tone, octatonic or chromatic, now and then the blues or the section's own.
/// </summary>
internal static class LineScales
{
    public static ImmutableArray<(LineScale Scale, ByConvention Weight)> Weights { get; } =
    [
        (LineScale.Section, new ByConvention(0.32, 0.6, 0.2)),
        (LineScale.Pentatonic, new ByConvention(0.68, 0.33, 0)),
        (LineScale.Blues, new ByConvention(0, 0.05, 0.2)),
        (LineScale.MelodicMinor, new ByConvention(0, 0.005, 1)),
        (LineScale.WholeTone, new ByConvention(0, 0.005, 1)),
        (LineScale.Octatonic, new ByConvention(0, 0.005, 1)),
        (LineScale.Chromatic, new ByConvention(0, 0.005, 1)),
        (LineScale.Union, new ByConvention(0, 0, 0))
    ];

    /// <summary>A line's scale as a genre's page shows it.</summary>
    public static string LabelOf(LineScale scale) => scale switch
    {
        LineScale.Section => "The section's",
        LineScale.Pentatonic => "Pentatonic",
        LineScale.Blues => "Blues",
        LineScale.MelodicMinor => "Melodic minor",
        LineScale.WholeTone => "Whole-tone",
        LineScale.Octatonic => "Octatonic",
        LineScale.Chromatic => "Chromatic",
        LineScale.Union => "Pentatonic union",
        _ => throw new ArgumentOutOfRangeException(nameof(scale), scale, null)
    };

    private const int OctaveNoteCount = 12;

    /// <summary>The major and the minor pentatonic, in semitones above their tonic.</summary>
    private static readonly ImmutableArray<int> MajorPentatonic = [0, 2, 4, 7, 9], MinorPentatonic = [0, 3, 5, 7, 10];

    /// <summary>The pentatonic union, in semitones above the key's tonic.</summary>
    private static readonly ImmutableArray<int> UnionOffsets = [0, 2, 3, 4, 5, 7, 9, 10];

    private static readonly ImmutableDictionary<LineScale, ImmutableArray<int>> OffsetsFromRoot = new Dictionary<LineScale, ImmutableArray<int>>
    {
        [LineScale.Blues] = [0, 3, 5, 6, 7, 10],
        [LineScale.MelodicMinor] = [0, 2, 3, 5, 7, 9, 11],
        [LineScale.WholeTone] = [0, 2, 4, 6, 8, 10],
        [LineScale.Octatonic] = [0, 1, 3, 4, 6, 7, 9, 10],
        [LineScale.Chromatic] = [..Enumerable.Range(0, OctaveNoteCount)]
    }.ToImmutableDictionary();

    /// <summary>A section's line's scale, by the scale facet of its unconventionality, among a genre's pool where it names one.</summary>
    /// <param name="riff">Whether the line is a riff's, which takes a genre's riff's scale where it names one, the lines' otherwise.</param>
    public static LineScale Draw(IGenerationContext context, double unconventionality, Genre genre, bool riff)
    {
        var choice = riff && genre.Names(GenreChoices.RiffScale) ? GenreChoices.RiffScale : GenreChoices.LineScale;
        return context.Pick(ByConvention.Weigh(Weights, unconventionality, genre.Pool<LineScale>(choice)));
    }

    /// <summary>
    ///     The pitch classes the line takes over the chord, or none for all of the section's scale: a pentatonic line's the
    ///     key's own pentatonic, major where the mode's third is and minor where it is not, kept to the scale's notes, all of
    ///     the scale where it has more than one tritone, as harmonic minor; the pentatonic union on the key's tonic; and any
    ///     other's notes on the chord's root.
    /// </summary>
    public static IReadOnlySet<int>? Classes(LineScale scale, ChordContext chord)
    {
        switch (scale)
        {
            case LineScale.Section:
                return null;
            case LineScale.Pentatonic:
            {
                var classes = Enumerable.Range(0, Scales.StepCount).Select(x => chord.GetPitch(x).Mod(OctaveNoteCount)).Distinct().ToArray();
                var pair = classes.Where(x => classes.Contains((x + 6) % OctaveNoteCount)).ToHashSet();
                if (pair.Count != 2)
                    return null;
                var tonic = chord.Tonic;
                var major = classes.Contains((tonic + 4).Mod(OctaveNoteCount));
                return (major ? MajorPentatonic : MinorPentatonic).Select(x => (tonic + x).Mod(OctaveNoteCount)).Where(classes.Contains).ToHashSet();
            }
            case LineScale.Union:
                return UnionOffsets.Select(x => (chord.Tonic + x).Mod(OctaveNoteCount)).ToHashSet();
            default:
                return OffsetsFromRoot[scale].Select(x => (chord.Root + x).Mod(OctaveNoteCount)).ToHashSet();
        }
    }
}
