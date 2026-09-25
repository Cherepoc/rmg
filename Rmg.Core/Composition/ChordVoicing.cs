using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     Lays out a chord shape by moving some of its notes by octaves, which keeps the chord and changes how it is spread.
///     A shape whose layout is what it is keeps it.
/// </summary>
public static class ChordVoicing
{
    private static readonly ImmutableArray<Voicing> Voicings =
    [
        new("Close", 0.4, 1, x => x),
        // the lowest note an octave up
        new("First inversion", 0.2, 2, x => [..x.Skip(1), x[0] + 12]),
        // the two lowest notes an octave up
        new("Second inversion", 0.1, 3, x => [..x.Skip(2), x[0] + 12, x[1] + 12]),
        // the second highest note an octave down
        new("Drop 2", 0.15, 3, x => [x[^2] - 12, ..x[..^2], x[^1]]),
        // the second lowest note an octave up
        new("Open", 0.15, 3, x => [x[0], ..x[2..], x[1] + 12])
    ];

    /// <summary>The shape's heights, in semitones above its root, laid out by a voicing that suits its note count.</summary>
    public static ImmutableArray<double> Apply(IGenerationContext context, ChordShape shape)
    {
        if (shape.IsVoicingFixed)
            return shape.Targets;

        var voicings = Voicings.Where(x => shape.Targets.Length >= x.MinNoteCount).ToImmutableArray();
        var voicing = voicings[Generators.WeightedIndex([..voicings.Select(x => new Weighted<Voicing>(x.Weight, x))])(context)];
        return [..voicing.Apply(shape.Targets).Order()];
    }

    private sealed record Voicing(
        string Name,
        double Weight,
        int MinNoteCount,
        Func<ImmutableArray<double>, ImmutableArray<double>> Apply
    );
}
