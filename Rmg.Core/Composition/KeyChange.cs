using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A song going up a key for its last section, where that section came back before, as a last chorus does: by a
///     whole step most often and a half step otherwise, now and then by the scale facet of its unconventionality: never
///     in the plainest song, which keeps its key, and every time at the wild end. The whole band moves, and the
///     lines, placed after, go on into the new key.
/// </summary>
/// <param name="Position">Where the new key starts, the last section's start.</param>
/// <param name="Semitones">How far up the key goes.</param>
public sealed record KeyChange(double Position, int Semitones)
{
    /// <summary>
    ///     The chance a song whose last section came back before goes up a key for it: none of the plainest songs, a few at
    ///     the middle and every one at the wild end.
    /// </summary>
    public static ByConvention Chance { get; } = new(0, 0.08, 1);

    /// <summary>The chance the key goes up a half step, rather than a whole one.</summary>
    public const double HalfStepChance = 0.4;

    /// <param name="sectionIds">The song's sections in its order, before any it plays again to fade out.</param>
    /// <param name="unconventionality">The scale facet of the song's unconventionality.</param>
    internal static KeyChange? Generate(IGenerationContext context, double unconventionality, SongMap map, IReadOnlyList<int> sectionIds)
    {
        var changes = context.TestProbability(Chance.At(unconventionality));
        var semitones = context.TestProbability(HalfStepChance) ? 1 : 2;
        var last = sectionIds.Count - 1;
        var cameBack = sectionIds.Take(last).Contains(sectionIds[last]);
        return changes && cameBack ? new KeyChange(map.Sections[last].Start, semitones) : null;
    }

    /// <summary>The key's step from where it changes, over the song's key, none for a song that keeps its key.</summary>
    internal static StateTimelineMap ToStateTimelineMap(KeyChange? change, double duration)
    {
        return change is null
            ? StateTimelineMap.Create(duration)
            : StateTimelineMap.Create(duration, [StateTimeline.Create(duration, StateKinds.KeyOffset, [change.Semitones.ToTimelineItem(change.Position)])]);
    }
}
