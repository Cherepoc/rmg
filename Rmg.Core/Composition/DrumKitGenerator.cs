using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     Chooses the drums of a song and the drums that play in a section. Playing all the drums at once would only
///     be noise, so a section gets a lead for each role in the groove and a few drums that colour it.
/// </summary>
internal static class DrumKitGenerator
{
    /// <summary>
    ///     The drums a section plays, out of the song's: a lead for the ground, one for the backbeat and one to keep time,
    ///     so that a section keeps its backbeat and its time together, each drawn by weight among the drums that play the
    ///     role in the section, or where none does among those whose main role it is
    ///     (<see cref="PercussionInstrumentDefinition.MainRole" />), which then play it, the loud ones likelier the more
    ///     energy the section has, from the drum kit, or from the percussion in a section of percussion only, and beside a
    ///     lead of another main role a drum whose main role it is, in the role it plays; then the drums that colour the groove, none to <see cref="MaxColourGroups" />
    ///     groups of them (the toms, the accents, the percussion), more the more energy. What a drum plays there is its
    ///     role in the section, which a wild one may draw away from its main one, such as the snare keeping time while the
    ///     hi-hat plays the backbeat. A section
    ///     of percussion only plays its percussion's leads and more of it, up to
    ///     <see cref="PercussionSections.MaxActiveDrums" />. Last, now and then a drum doubles a lead, such as the clap on
    ///     the snare's backbeat or a shaker over the hi-hat: one that doubles (<see cref="PercussionInstrumentDefinition.Doubling" />)
    ///     and has an affinity for the lead's role, the likelier the more it has, as a clap now and then on the kick, and a
    ///     section doubles the likelier the more energy it has and the more eager the drum (<see cref="PercussionInstrumentDefinition.DoublingOdds" />).
    /// </summary>
    /// <param name="tilt">The section's pull of its energy.</param>
    /// <param name="roles">Every one of the song's drums' role in the section, as the song and the section draw it.</param>
    public static SectionKit SelectKit(
        IGenerationContext context,
        ImmutableArray<PercussionInstrumentDefinition> songDrums,
        Tilt tilt,
        bool isPercussionOnly,
        IReadOnlyDictionary<PercussionInstrumentDefinition, DrumRole> roles
    )
    {
        var family = songDrums.Where(x => x.Family.HasFlag(isPercussionOnly ? DrumFamily.Percussion : DrumFamily.Kit)).ToArray();
        PercussionInstrumentDefinition Pick(IEnumerable<PercussionInstrumentDefinition> drums) =>
            PickWeighted(context, drums.Select(x => new Weighted<PercussionInstrumentDefinition>(tilt.Weigh(x.Weight, x.Loudness), x)), 1)[0];
        var kit = new List<PercussionInstrumentDefinition>();
        var leads = ImmutableDictionary.CreateBuilder<DrumRole, PercussionInstrumentDefinition>();
        foreach (var lead in LeadRoles)
        {
            // a lead of a group that grooves, not of one that all but never does, such as the calls
            var grooving = family.Where(x => !leads.ContainsValue(x) && DrumGroups.GroupOf(x).GrooveChance >= 1).ToArray();
            var playing = grooving.Where(x => roles[x] == lead).ToArray();
            var candidates = playing.Length > 0 ? playing : grooving.Where(x => x.MainRole == lead).ToArray();
            if (candidates.Length == 0)
                continue;

            var picked = Pick(candidates);
            leads[lead] = picked;
            if (!kit.Contains(picked))
                kit.Add(picked);
            // and a drum whose main role it is, in the role it plays, as a jazz snare comps while the hi-hat plays the backbeat
            if (!kit.Any(x => x.MainRole == lead) && grooving.Where(x => x.MainRole == lead).ToArray() is { Length: > 0 } own)
                kit.Add(Pick(own));
        }

        // every drum's role in the section, a lead's the one it leads
        var sectionRoles = songDrums.ToImmutableDictionary(x => x, x => roles[x]).SetItems(leads.Select(x => KeyValuePair.Create(x.Value, x.Key)));
        if (isPercussionOnly)
        {
            var more = family.Except(kit).Select(x => new Weighted<PercussionInstrumentDefinition>(tilt.Weigh(x.Weight, x.Loudness), x));
            kit.AddRange(PickWeighted(context, more, Math.Max(0, PercussionSections.MaxActiveDrums - kit.Count)));
            return new SectionKit([..kit], leads.ToImmutable(), sectionRoles, ImmutableDictionary<PercussionInstrumentDefinition, PercussionInstrumentDefinition>.Empty);
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
        var count = context.Pick(counts);
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
        foreach (var lead in leads.Values.Where(x => x.Doubling <= 0))
        {
            // any of the song's drums that doubles and has an affinity for the lead's role, the percussion too, likelier
            // the more it has, so that a clap mostly doubles the snare and now and then the kick
            var candidates = songDrums
                .Where(x => x.Doubling > 0 && x.AffinityFor(lead.MainRole) > 0 && !kit.Contains(x) && !doubles.ContainsKey(x))
                .ToArray();
            // the likelier the more eager, and the more at home in the lead's role, the likeliest of them is, so that a
            // lead that only drums of another main role may double, as the kick, is seldom doubled
            if (candidates.Length == 0)
                continue;
            var eagerness = candidates.Max(x => x.DoublingOdds * x.AffinityFor(lead.MainRole));
            if (!context.TestProbability(new Tilt(tilt.LogOdds + Math.Log(eagerness)).Chance(DoublingChance, 1)))
                continue;

            doubles[PickWeighted(context, candidates.Select(x => new Weighted<PercussionInstrumentDefinition>(x.Doubling * x.AffinityFor(lead.MainRole), x)), 1)[0]] = lead;
        }

        return new SectionKit([..kit, ..doubles.Keys], leads.ToImmutable(), sectionRoles, doubles.ToImmutable());
    }

    /// <summary>The chance a section has a drum double a lead that one may double, with no lean; the more energy, the likelier.</summary>
    public const double DoublingChance = 0.15;

    /// <summary>The share of its lead's beats a drum that accents the lead plays (<see cref="DrumBinding.Accent" />).</summary>
    public const double AccentShare = 0.5;

    /// <summary>
    ///     The chance a drum that colours a section plays on the feel of the lead of its main role, with no lean; the less
    ///     conventional the section, the likelier it crosses it, a tuplet against the lead's for the whole section.
    /// </summary>
    public const double FeelChance = 0.85;

    /// <summary>The roles a section has a lead for, in the order it draws them.</summary>
    public static ImmutableArray<DrumRole> LeadRoles { get; } = [DrumRole.Ground, DrumRole.Backbeat, DrumRole.Time];

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
///     The drums a section plays; those of them that lead a role, the ground, the backbeat or time, by the role; every one
///     of the song's drums' role in the section; and those that double a lead, by the lead they double.
/// </summary>
public sealed record SectionKit(
    ImmutableArray<PercussionInstrumentDefinition> Drums,
    ImmutableDictionary<DrumRole, PercussionInstrumentDefinition> Leads,
    ImmutableDictionary<PercussionInstrumentDefinition, DrumRole> Roles,
    ImmutableDictionary<PercussionInstrumentDefinition, PercussionInstrumentDefinition> Doubles
);
