using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     Which of a section's drums play in which bars. The section chooses its drums, and its first bar pattern, the
///     phrase scheme's A, plays them all; an optional drum may sit out the bars of every other letter, so that bars of
///     the same letter play the same drums and a contrasting bar drops one, as an arrangement does. The kick and the
///     snare always play. A drum sits out the likelier the less conventional the section's rhythm, and the less likely
///     the more energy the section has.
/// </summary>
public static class DrumPresence
{
    /// <summary>The chance an optional drum sits out the bars of a letter other than the first, with no lean.</summary>
    public const double SitOutChance = 0.3;

    /// <summary>
    ///     The bars every optional drum sits out, as its track and the letter of the bars; drawn from a sequence of its
    ///     own, one draw for every optional drum and letter after the first, in the order of the tracks.
    /// </summary>
    /// <param name="tracks">The drums the section plays, by their track numbers.</param>
    /// <param name="rhythm">How unconventional the section's rhythm is, which leans a drum to sit out.</param>
    /// <param name="energy">The section's pull of its energy, which leans a drum to keep playing.</param>
    public static ImmutableHashSet<(int Track, int Letter)> Draw(
        IGenerationContext context,
        IEnumerable<int> tracks,
        PhraseScheme scheme,
        Tilt rhythm,
        Tilt energy
    )
    {
        var chance = rhythm.Chance(energy.Chance(SitOutChance, -1), 1);
        var optional = tracks.Where(x => !DrumGroups.GetGroup(x).IsAlwaysOn).Order();
        return
        [
            ..from track in optional
                from letter in Enumerable.Range(1, scheme.PatternCount - 1)
                where context.TestProbability(chance)
                select (track, letter)
        ];
    }
}
