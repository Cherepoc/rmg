using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     Lays out a chord shape by moving some of its notes by octaves, which keeps the chord and changes how it is spread,
///     by the chords facet of the unconventionality: the plainest songs close and inverted, as a triad sits under the
///     hand, the wildest every layout but close, spread and dropped as much as inverted. A shape whose layout is what it
///     is keeps it.
/// </summary>
internal static class ChordVoicing
{
    private static readonly ImmutableArray<Voicing> Voicings =
    [
        new("Close", new ByConvention(0.5, 0.4, 0), 1, x => x),
        // the lowest note an octave up
        new("First inversion", new ByConvention(0.3, 0.2, 1), 2, x => [..x.Skip(1), x[0] + 12]),
        // the two lowest notes an octave up
        new("Second inversion", new ByConvention(0.2, 0.1, 1), 3, x => [..x.Skip(2), x[0] + 12, x[1] + 12]),
        // the second highest note an octave down
        new("Drop 2", new ByConvention(0, 0.15, 1), 3, x => [x[^2] - 12, ..x[..^2], x[^1]]),
        // the second lowest note an octave up
        new("Open", new ByConvention(0, 0.15, 1), 3, x => [x[0], ..x[2..], x[1] + 12])
    ];

    /// <summary>The shape's heights, in semitones above its root, laid out by a voicing that suits its note count.</summary>
    /// <param name="unconventionality">The chords facet of the unconventionality.</param>
    public static ImmutableArray<double> Apply(IGenerationContext context, ChordShape shape, double unconventionality)
    {
        if (shape.IsVoicingFixed)
            return shape.Targets;

        var voicings = Voicings.Where(x => shape.Targets.Length >= x.MinNoteCount).Select(x => (x, x.Weight));
        var voicing = context.Pick(ByConvention.Weigh(voicings, unconventionality));
        return [..voicing.Apply(shape.Targets).Order()];
    }

    private sealed record Voicing(
        string Name,
        ByConvention Weight,
        int MinNoteCount,
        Func<ImmutableArray<double>, ImmutableArray<double>> Apply
    );
}
