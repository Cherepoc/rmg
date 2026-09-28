using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     Which of a section's drums play in which bars, and how. The section chooses its drums, and its first bar pattern,
///     the phrase scheme's A, plays them as the section does; a bar of another letter may vary them, so that bars of the
///     same letter play the same drums and a contrasting bar plays them otherwise, as an arrangement does. A drum that
///     holds a role in the groove (<see cref="DrumGroup.HoldsARole" />), as does every drum of a section of percussion
///     only, never leaves it empty: it may change its stroke there (<see cref="DrumStrokes" />), such as the hi-hat
///     opening or the snare going to its cross-stick. A drum that colours the groove may sit out such a bar, the likelier
///     the less conventional the section's rhythm and the less likely the more energy it has.
/// </summary>
public static class DrumPresence
{
    /// <summary>The chance a drum that colours the groove sits out the bars of a letter other than the first, with no lean.</summary>
    public const double SitOutChance = 0.3;

    /// <summary>
    ///     How every drum plays the bars of every letter after the first: the drums that colour the groove that sit out,
    ///     and the strokes the drums that hold a role change to; drawn from a sequence of its own, in the order of the
    ///     tracks and the letters.
    /// </summary>
    /// <param name="tracks">The drums the section plays, by their track numbers.</param>
    /// <param name="isPercussionOnly">Whether the section plays its percussion alone, which then holds every role.</param>
    /// <param name="stroke">A drum's stroke in the section, by its track, for a drum that has strokes.</param>
    /// <param name="rhythm">How unconventional the section's rhythm is, which leans a drum to sit out or change.</param>
    /// <param name="energy">The section's pull of its energy, which leans a drum to keep playing and its strokes by loudness.</param>
    public static BarDrums Draw(
        IGenerationContext context,
        IEnumerable<int> tracks,
        PhraseScheme scheme,
        bool isPercussionOnly,
        Func<int, int> stroke,
        Tilt rhythm,
        Tilt energy
    )
    {
        var sitOut = rhythm.Chance(energy.Chance(SitOutChance, -1), 1);
        var resting = ImmutableHashSet.CreateBuilder<(int Track, int Letter)>();
        var strokes = ImmutableDictionary.CreateBuilder<(int Track, int Letter), int>();
        foreach (var track in tracks.Order())
        {
            var drum = DrumGroups.GetDrum(track);
            var holdsARole = isPercussionOnly || DrumGroups.GetGroup(track).HoldsARole;
            foreach (var letter in Enumerable.Range(1, scheme.PatternCount - 1))
            {
                if (!holdsARole)
                {
                    if (context.TestProbability(sitOut))
                        resting.Add((track, letter));
                }
                else if (drum.HasStrokes && DrumStrokes.DrawChange(context, drum, stroke(track), DrumStrokes.BarChangeChance, rhythm, energy) is { } change)
                {
                    strokes[(track, letter)] = change;
                }
            }
        }

        return new BarDrums(resting.ToImmutable(), strokes.ToImmutable());
    }
}

/// <summary>How a section's drums play the bars of its later letters.</summary>
/// <param name="Resting">The bars a drum sits out, by its track and the letter: it keeps its state there, with no notes.</param>
/// <param name="Strokes">The stroke a drum changes to in the bars of a letter, by its track and the letter.</param>
public sealed record BarDrums(ImmutableHashSet<(int Track, int Letter)> Resting, ImmutableDictionary<(int Track, int Letter), int> Strokes)
{
    public static BarDrums None { get; } = new([], ImmutableDictionary<(int Track, int Letter), int>.Empty);
}
