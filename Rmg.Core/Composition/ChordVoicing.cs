using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     Lays out a chord shape by moving some of its notes by octaves, which keeps the chord and changes how it is spread,
///     by the chords facet of the unconventionality: the plainest songs close, as a triad sits under the hand, the voice
///     leader inverting it as it moves (<see cref="VoiceLeader" />), the wildest spread and dropped as much as close, and
///     stretched over two octaves now and then. A shape whose layout is what it is keeps it.
/// </summary>
internal static class ChordVoicing
{
    private static readonly ImmutableArray<Voicing> Voicings =
    [
        // its inversions the voice leader's, which lays out every chord it moves to in each of them
        new("Close", new ByConvention(1, 0.7, 2), 1, x => x),
        // the second highest note an octave down
        new("Drop 2", new ByConvention(0, 0.15, 1), 3, x => [x[^2] - 12, ..x[..^2], x[^1]]),
        // the second lowest note an octave up
        new("Open", new ByConvention(0, 0.15, 1), 3, x => [x[0], ..x[2..], x[1] + 12]),
        // stretched over two octaves and more, a pianist's spread or a guitarist's wide stretch: the lowest note kept, the
        // second lowest two octaves up and the rest one
        new("Spread", new ByConvention(0, 0.05, 0.5), 3, x => [x[0], ..x[2..].Select(y => y + 12), x[1] + 24])
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
