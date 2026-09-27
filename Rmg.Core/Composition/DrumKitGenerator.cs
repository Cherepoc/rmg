using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     Chooses the drums of a song and the drums that play in a section. Playing all the drums at once would only
///     be noise, so a section gets the always-on groups plus a few optional ones, and only some of the drums of
///     each group.
/// </summary>
public static class DrumKitGenerator
{
    public const int MaxGroupsPerSection = 4;

    /// <summary>Chooses the drums that are available in a song.</summary>
    public static ImmutableArray<PercussionInstrumentDefinition> SelectSongDrums(IGenerationContext context)
    {
        var songDrums = DrumGroups.All
            .Select(group => (group, drums: group.SongRule.Select(context, group.Drums)))
            .ToImmutableArray();

        foreach (var (group, drums) in songDrums)
        {
            if (group.IsAlwaysOn && drums.IsEmpty)
                throw new InvalidOperationException($"An always-on drum group '{group.Name}' has no drums in the song.");
        }

        return [..songDrums.SelectMany(x => x.drums)];
    }

    /// <summary>
    ///     Chooses the drums that play in a section out of the drums available in the song. The section's energy leans
    ///     it: to more groups, and to loud drums, and the groups of them, in a section of more energy, and the other way
    ///     in one of less.
    /// </summary>
    public static ImmutableArray<PercussionInstrumentDefinition> SelectActiveDrums(
        IGenerationContext context,
        ImmutableArray<PercussionInstrumentDefinition> songDrums,
        Tilt tilt = default
    )
    {
        var alwaysOnGroups = DrumGroups.All.Where(x => x.IsAlwaysOn).ToImmutableArray();
        // a song can have no drums in a group at all
        var optionalGroups = DrumGroups.All
            .Where(x => !x.IsAlwaysOn && x.Drums.Any(songDrums.Contains))
            .Where(x => x.GrooveChance >= 1 || context.TestProbability(tilt.Chance(x.GrooveChance, GetLoudness(x, songDrums))))
            .ToImmutableArray();

        // from one optional group, the quietest, to as many as may play, the loudest
        var maxOptionalGroupCount = Math.Min(MaxGroupsPerSection - alwaysOnGroups.Length, optionalGroups.Length);
        ImmutableArray<Weighted<int>> counts =
        [
            ..Enumerable.Range(1, maxOptionalGroupCount)
                .Select(x => new Weighted<int>(tilt.Weigh(1, maxOptionalGroupCount > 1 ? 2.0 * (x - 1) / (maxOptionalGroupCount - 1) - 1 : 0), x))
        ];
        var optionalGroupCount = maxOptionalGroupCount > 0
            ? counts[Generators.WeightedIndex(counts)(context)].Value
            : 0;

        var selectedOptionalGroups = PickWeighted(
            context,
            optionalGroups.Select(x => new Weighted<DrumGroup>(tilt.Weigh(x.SelectionWeight, GetLoudness(x, songDrums)), x)),
            optionalGroupCount
        );

        // keep the group order stable so that the result only depends on what was picked
        var activeGroups = alwaysOnGroups.Concat(selectedOptionalGroups)
            .OrderBy(x => DrumGroups.All.IndexOf(x));

        return
        [
            ..activeGroups.SelectMany(group => PickWeighted(
                    context,
                    group.Drums.Where(songDrums.Contains).Select(x => new Weighted<PercussionInstrumentDefinition>(tilt.Weigh(x.Weight, x.Loudness), x)),
                    group.MaxActiveDrums
                )
            )
        ];
    }

    /// <summary>How loud a group leans in a song: its drums' loudness there, the likelier drums counting more.</summary>
    internal static double GetLoudness(DrumGroup group, ImmutableArray<PercussionInstrumentDefinition> songDrums)
    {
        var drums = group.Drums.Where(songDrums.Contains).ToArray();
        var weight = drums.Sum(x => x.Weight);
        return weight > 0 ? drums.Sum(x => x.Weight * x.Loudness) / weight : 0;
    }

    /// <summary>Picks up to <paramref name="count" /> distinct items, more likely the heavier ones.</summary>
    internal static List<T> PickWeighted<T>(IGenerationContext context, IEnumerable<Weighted<T>> items, int count)
    {
        var remaining = items.ToList();
        var result = new List<T>(count);
        while (result.Count < count && remaining.Count > 0)
        {
            var index = Generators.WeightedIndex([..remaining])(context);
            result.Add(remaining[index].Value);
            remaining.RemoveAt(index);
        }

        return result;
    }
}
