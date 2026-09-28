using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     Chooses the drums of a song and the drums that play in a section. Playing all the drums at once would only
///     be noise, so a section gets a lead for each role in the groove and a few drums that colour it.
/// </summary>
public static class DrumKitGenerator
{
    /// <summary>
    ///     The drums a section plays, out of the song's: a lead for the ground, one for the backbeat, and mostly one to
    ///     keep time, each drawn among the drums whose main role it is (<see cref="PercussionInstrumentDefinition.MainRole" />)
    ///     by weight, the loud ones likelier the more energy the section has, from the drum kit, or from the percussion in
    ///     a section of percussion only; then the drums that colour the groove, none to <see cref="MaxColourGroups" />
    ///     groups of them (the toms, the accents, the percussion), more the more energy. What a drum plays there is its
    ///     role in the section, which a wild one may draw away from its main one, such as the snare keeping time. A section
    ///     of percussion only plays its percussion's leads and more of it, up to
    ///     <see cref="PercussionSections.MaxActiveDrums" />. Last, now and then a drum doubles a lead, such as the clap on
    ///     the snare's backbeat or a shaker over the hi-hat: one whose role in the section is the lead's and that doubles
    ///     (<see cref="PercussionInstrumentDefinition.Doubling" />), the likelier the more energy the section has.
    /// </summary>
    /// <param name="role">A drum's role in the section, which a drum that doubles takes on.</param>
    /// <param name="tilt">The section's pull of its energy.</param>
    public static SectionKit SelectKit(
        IGenerationContext context,
        ImmutableArray<PercussionInstrumentDefinition> songDrums,
        Func<PercussionInstrumentDefinition, DrumRole> role,
        Tilt tilt,
        bool isPercussionOnly
    )
    {
        var family = songDrums.Where(x => x.Family.HasFlag(isPercussionOnly ? DrumFamily.Percussion : DrumFamily.Kit)).ToArray();
        var kit = new List<PercussionInstrumentDefinition>();
        var leads = new List<PercussionInstrumentDefinition>();
        foreach (var lead in LeadRoles)
        {
            var candidates = family.Where(x => x.MainRole == lead).ToArray();
            if (candidates.Length == 0 || !context.TestProbability(tilt.Chance(lead == DrumRole.Time ? TimeChance : 1, 1)))
                continue;

            var picked = PickWeighted(context, candidates.Select(x => new Weighted<PercussionInstrumentDefinition>(tilt.Weigh(x.Weight, x.Loudness), x)), 1);
            kit.AddRange(picked);
            leads.AddRange(picked);
        }

        if (isPercussionOnly)
        {
            var more = family.Except(kit).Select(x => new Weighted<PercussionInstrumentDefinition>(tilt.Weigh(x.Weight, x.Loudness), x));
            kit.AddRange(PickWeighted(context, more, PercussionSections.MaxActiveDrums - kit.Count));
            return new SectionKit([..kit], [..leads], ImmutableDictionary<PercussionInstrumentDefinition, PercussionInstrumentDefinition>.Empty);
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

        var doubles = ImmutableDictionary.CreateBuilder<PercussionInstrumentDefinition, PercussionInstrumentDefinition>();
        // a lead that doubles itself, such as a shaker keeping time, is not doubled
        foreach (var lead in leads.Where(x => x.Doubling <= 0))
        {
            var candidates = family.Where(x => x.Doubling > 0 && role(x) == lead.MainRole && !kit.Contains(x) && !doubles.ContainsKey(x)).ToArray();
            if (candidates.Length == 0 || !context.TestProbability(tilt.Chance(DoublingChance, 1)))
                continue;

            doubles[PickWeighted(context, candidates.Select(x => new Weighted<PercussionInstrumentDefinition>(x.Doubling, x)), 1)[0]] = lead;
        }

        return new SectionKit([..kit, ..doubles.Keys], [..leads], doubles.ToImmutable());
    }

    /// <summary>The chance a section has a drum double a lead that one may double, with no lean; the more energy, the likelier.</summary>
    public const double DoublingChance = 0.15;

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

/// <summary>
///     The drums a section plays; those of them that lead a role, the ground, the backbeat or time; and those that double
///     a lead, by the lead they double.
/// </summary>
public sealed record SectionKit(
    ImmutableArray<PercussionInstrumentDefinition> Drums,
    ImmutableArray<PercussionInstrumentDefinition> Leads,
    ImmutableDictionary<PercussionInstrumentDefinition, PercussionInstrumentDefinition> Doubles
);
