using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     A section planing: every chord the home chord's shape, a constant structure, moved by semitones, each move one a
///     voice moves little by, so that the chords drift by their voices rather than by the scale's steps, as planing in film
///     scores and modern jazz does. A wild song's.
/// </summary>
internal static class Planing
{
    /// <summary>The chance a section planes, by the progression facet: a wild song's alone.</summary>
    public static ByConvention Chance { get; } = new(0, 0, 0.25);

    /// <summary>
    ///     The moves from a chord to the next, in semitones, and how likely each is: by a semitone or a whole step, its
    ///     voices moving least, a minor third less, and a fourth, a root's strongest move, as often; up or down alike.
    /// </summary>
    public static ImmutableArray<Weighted<int>> Moves { get; } =
    [
        new(1, 1), new(1, -1), new(1, 2), new(1, -2), new(0.5, 3), new(0.5, -3), new(0.5, 5), new(0.5, -5)
    ];

    /// <summary>The farthest a planed chord strays from home, in semitones, a move past it turned back.</summary>
    public const int Reach = 6;

    /// <summary>Every chord's move from the home's, the first home, a draw a chord whatever is played.</summary>
    public static ImmutableArray<int> Draw(IGenerationContext context, int count)
    {
        var shifts = new int[count];
        for (var chord = 1; chord < count; chord++)
        {
            var move = context.Pick(Moves);
            shifts[chord] = Math.Abs(shifts[chord - 1] + move) > Reach ? shifts[chord - 1] - move : shifts[chord - 1] + move;
        }

        return [..shifts];
    }
}
