using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>The notes a line's passing notes take, over each chord it plays over.</summary>
public enum LineScale
{
    /// <summary>The section's scale, all of it.</summary>
    Section,

    /// <summary>The section's scale but for its tritone pair, the two notes that make its only tritone.</summary>
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
    Chromatic
}

/// <summary>
///     A line's scale: the notes a melody's or a riff's passing notes take, its strong beats taking its chord's notes as
///     they do, where the line takes them. The plainest lines take the section's scale, or most often its pentatonic;
///     the wildest a scale of their own over every chord, measured from its root rather than the key, as a player such as
///     Allan Holdsworth thinks of them: melodic minor, whole-tone, octatonic or chromatic, now and then the blues or the
///     section's own.
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
        (LineScale.Chromatic, new ByConvention(0, 0.005, 1))
    ];

    private const int OctaveNoteCount = 12;

    private static readonly ImmutableDictionary<LineScale, ImmutableArray<int>> OffsetsFromRoot = new Dictionary<LineScale, ImmutableArray<int>>
    {
        [LineScale.Blues] = [0, 3, 5, 6, 7, 10],
        [LineScale.MelodicMinor] = [0, 2, 3, 5, 7, 9, 11],
        [LineScale.WholeTone] = [0, 2, 4, 6, 8, 10],
        [LineScale.Octatonic] = [0, 1, 3, 4, 6, 7, 9, 10],
        [LineScale.Chromatic] = [..Enumerable.Range(0, OctaveNoteCount)]
    }.ToImmutableDictionary();

    /// <summary>A section's line's scale, by the scale facet of its unconventionality.</summary>
    public static LineScale Draw(IGenerationContext context, double unconventionality)
    {
        return context.Pick(ByConvention.Weigh(Weights, unconventionality));
    }

    /// <summary>
    ///     The pitch classes the line takes over the chord, or none for all of the section's scale: a pentatonic line's
    ///     scale but for its tritone pair, all of it where it has more than one tritone, as harmonic minor, and any other's
    ///     notes on the chord's root.
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
                return pair.Count == 2 ? classes.Except(pair).ToHashSet() : null;
            }
            default:
                return OffsetsFromRoot[scale].Select(x => (chord.Root + x).Mod(OctaveNoteCount)).ToHashSet();
        }
    }
}
