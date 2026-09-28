using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     Chooses the drums of a song and the drums that play in a section. Playing all the drums at once would only
///     be noise, so a section gets a lead for each role in the groove and a few drums that colour it.
/// </summary>
public static class DrumKitGenerator
{
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
    ///     The drums a section plays, out of the song's: a lead for the ground, one for the backbeat, and mostly one to
    ///     keep time, each drawn among the drums whose main role it is (<see cref="PercussionInstrumentDefinition.MainRole" />)
    ///     by weight, the loud ones likelier the more energy the section has, from the drum kit, or from the percussion in
    ///     a section of percussion only; then the drums that colour the groove, none to <see cref="MaxColourGroups" />
    ///     groups of them (the toms, the accents, the percussion), more the more energy. What a drum plays there is its
    ///     role in the section, which a wild one may draw away from its main one, such as the snare keeping time. A section
    ///     of percussion only plays its percussion's leads and more of it, up to
    ///     <see cref="PercussionSections.MaxActiveDrums" />.
    /// </summary>
    /// <param name="tilt">The section's pull of its energy.</param>
    public static ImmutableArray<PercussionInstrumentDefinition> SelectKit(
        IGenerationContext context,
        ImmutableArray<PercussionInstrumentDefinition> songDrums,
        Tilt tilt,
        bool isPercussionOnly
    )
    {
        var family = songDrums.Where(x => DrumGroups.All.Single(g => g.Drums.Contains(x)) == DrumGroups.Percussion == isPercussionOnly).ToArray();
        var kit = new List<PercussionInstrumentDefinition>();
        foreach (var lead in LeadRoles)
        {
            var candidates = family.Where(x => x.MainRole == lead).ToArray();
            if (candidates.Length == 0 || !context.TestProbability(tilt.Chance(lead == DrumRole.Time ? TimeChance : 1, 1)))
                continue;

            kit.AddRange(PickWeighted(context, candidates.Select(x => new Weighted<PercussionInstrumentDefinition>(tilt.Weigh(x.Weight, x.Loudness), x)), 1));
        }

        if (isPercussionOnly)
        {
            var more = family.Except(kit).Select(x => new Weighted<PercussionInstrumentDefinition>(tilt.Weigh(x.Weight, x.Loudness), x));
            kit.AddRange(PickWeighted(context, more, PercussionSections.MaxActiveDrums - kit.Count));
            return [..kit];
        }

        // the colour: the groups that hold no role, those of a lower chance of grooving now and then
        var colourGroups = DrumGroups.All
            .Where(x => !x.HoldsARole && x.Drums.Any(songDrums.Contains))
            .Where(x => x.GrooveChance >= 1 || context.TestProbability(tilt.Chance(x.GrooveChance, GetLoudness(x, songDrums))))
            .ToArray();
        var maxCount = Math.Min(MaxColourGroups, colourGroups.Length);
        ImmutableArray<Weighted<int>> counts =
        [
            ..Enumerable.Range(0, maxCount + 1).Select(x => new Weighted<int>(tilt.Weigh(ColourCountWeights[x], maxCount > 0 ? 2.0 * x / maxCount - 1 : 0), x))
        ];
        var count = counts[Generators.WeightedIndex(counts)(context)].Value;
        var groups = PickWeighted(context, colourGroups.Select(x => new Weighted<DrumGroup>(tilt.Weigh(x.SelectionWeight, GetLoudness(x, songDrums)), x)), count)
            .OrderBy(x => DrumGroups.All.IndexOf(x));
        foreach (var group in groups)
            kit.AddRange(
                PickWeighted(
                    context,
                    group.Drums.Where(songDrums.Contains).Except(kit).Select(x => new Weighted<PercussionInstrumentDefinition>(tilt.Weigh(x.Weight, x.Loudness), x)),
                    group.MaxActiveDrums
                )
            );

        return [..kit];
    }

    /// <summary>The roles a section has a lead for, the ground and the backbeat always, time by <see cref="TimeChance" />.</summary>
    public static ImmutableArray<DrumRole> LeadRoles { get; } = [DrumRole.Ground, DrumRole.Backbeat, DrumRole.Time];

    /// <summary>The chance a section has a drum to keep time, with no lean; the more energy, the likelier.</summary>
    public const double TimeChance = 0.9;

    /// <summary>How many groups of drums that colour the groove a section plays at most.</summary>
    public const int MaxColourGroups = 2;

    /// <summary>How likely a section is to play none, one or two groups of colour, with no lean; the more energy, the more.</summary>
    public static ImmutableArray<double> ColourCountWeights { get; } = [0.45, 0.42, 0.13];

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
