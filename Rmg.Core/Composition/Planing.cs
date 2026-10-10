using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     A section planing: every chord the home chord's shape, a constant structure, moved by semitones, each move one a
///     voice moves little by, so that the chords drift by their voices rather than by the scale's steps, as planing in film
///     scores and modern jazz does, now and then each chord in a mode of its own, roots off the scale with qualities of
///     their own, as fusion moves. A wild song's.
/// </summary>
internal static class Planing
{
    /// <summary>The chance a section planes, by the progression facet: a wild song's alone.</summary>
    public static ByConvention Chance { get; } = new(0, 0, 0.25);

    /// <summary>
    ///     The chance a planed section's chords each take a mode of their own, major or minor as it makes them, rather than
    ///     the home's structure (<see cref="ChordScales.Planed" />): how planing is played, not whether, as often in a middling
    ///     song as in a wild one.
    /// </summary>
    public static ByConvention ModesChance { get; } = new(0, 0.5, 0.5);

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

    /// <summary>
    ///     Every chord's move from the home's, the first home, a draw a chord whatever is played; where the section leads home,
    ///     as the song's last does, its last chord home again by a move of its own, the chord before a move away from it.
    /// </summary>
    public static ImmutableArray<int> Draw(IGenerationContext context, int count, bool endsHome)
    {
        var shifts = new int[count];
        var moves = Moves.Select(x => x.Value).Where(x => x > 0).Select(Math.Abs).ToHashSet();
        for (var chord = 1; chord < count; chord++)
        {
            var from = shifts[chord - 1];
            int Land(int move) => Math.Abs(from + move) > Reach ? from - move : from + move;
            // leading home, the last chord onto it and the one before onto a move from it
            bool Fits(int move) => !endsHome || count < 3 || chord < count - 2
                                   || (chord == count - 1 ? Land(move) == 0 : moves.Contains(Math.Abs(Land(move))));
            ImmutableArray<Weighted<int>> fitting = [..Moves.Where(x => Fits(x.Value))];
            var move = context.Pick(fitting.IsEmpty ? Moves : fitting);
            shifts[chord] = Land(move);
        }

        return [..shifts];
    }
}
