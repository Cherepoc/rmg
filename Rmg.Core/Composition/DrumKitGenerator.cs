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

    /// <summary>Chooses the drums that play in a section out of the drums available in the song.</summary>
    public static ImmutableArray<PercussionInstrumentDefinition> SelectActiveDrums(
        IGenerationContext context,
        ImmutableArray<PercussionInstrumentDefinition> songDrums
    )
    {
        var alwaysOnGroups = DrumGroups.All.Where(x => x.IsAlwaysOn).ToImmutableArray();
        // a song can have no drums in a group at all
        var optionalGroups = DrumGroups.All
            .Where(x => !x.IsAlwaysOn && x.Drums.Any(songDrums.Contains))
            .ToImmutableArray();

        var maxOptionalGroupCount = Math.Min(MaxGroupsPerSection - alwaysOnGroups.Length, optionalGroups.Length);
        var optionalGroupCount = maxOptionalGroupCount > 0
            ? context.GenerateInt(1, maxOptionalGroupCount + 1)
            : 0;

        var selectedOptionalGroups = PickWeighted(
            context,
            optionalGroups.Select(x => new Weighted<DrumGroup>(x.SelectionWeight, x)),
            optionalGroupCount
        );

        // keep the group order stable so that the result only depends on what was picked
        var activeGroups = alwaysOnGroups.Concat(selectedOptionalGroups)
            .OrderBy(x => DrumGroups.All.IndexOf(x));

        return
        [
            ..activeGroups.SelectMany(group => PickWeighted(
                    context,
                    group.Drums.Where(songDrums.Contains).Select(x => new Weighted<PercussionInstrumentDefinition>(x.Weight, x)),
                    group.MaxActiveDrums
                )
            )
        ];
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
