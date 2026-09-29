using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>What a song's drums are: the drum kit, the kit with some percussion, or percussion alone.</summary>
public enum DrumSetup
{
    Kit,
    KitAndPercussion,
    Percussion
}

/// <summary>
///     A song's drum setup, drawn once, and the drums it brings: most songs play the drum kit alone, some the kit with one
///     or two percussion drums, which may switch to sections of percussion only and back
///     (<see cref="PercussionSections" />), and a few percussion alone, with a drum for each role in the groove by its main
///     role and more beside, the hand percussion among them, so that it plays as a band does. The less conventional the
///     song's rhythm, the likelier the percussion.
/// </summary>
internal static class DrumSetups
{
    /// <summary>How likely every setup is, with no lean, and how it leans to the unconventional.</summary>
    public static ImmutableArray<(DrumSetup Setup, double Weight, double Lean)> All { get; } =
    [
        (DrumSetup.Kit, 0.6, 0),
        (DrumSetup.KitAndPercussion, 0.33, 0.5),
        (DrumSetup.Percussion, 0.07, 1)
    ];

    /// <summary>How many percussion drums a song of the kit and percussion has, and how likely each is.</summary>
    public static ImmutableArray<Weighted<int>> PercussionCounts { get; } = [new(0.6, 1), new(0.33, 2), new(0.07, 3)];

    /// <summary>How many drums a percussion song has beside one for every role, and how likely each is.</summary>
    public static ImmutableArray<Weighted<int>> MorePercussion { get; } = [new(0.3, 1), new(0.45, 2), new(0.25, 3)];

    /// <summary>How much less often a percussion song's drums draw their cycles afresh, so that their figures repeat, as an ensemble's do.</summary>
    public const double PercussionVariation = -0.3;

    /// <summary>A song's setup, the percussion likelier the less conventional its rhythm.</summary>
    public static DrumSetup Pick(IGenerationContext context, Tilt rhythm)
    {
        ImmutableArray<Weighted<DrumSetup>> weights = [..All.Select(x => new Weighted<DrumSetup>(rhythm.Weigh(x.Weight, x.Lean), x.Setup))];
        return context.Pick(weights);
    }

    /// <summary>
    ///     The drums of a song of the setup: the drum kit's by its groups' rules, and the percussion's by the setup; a
    ///     percussion song's, a drum of the percussion for every role, then more.
    /// </summary>
    public static ImmutableArray<PercussionInstrumentDefinition> SelectSongDrums(IGenerationContext context, DrumSetup setup)
    {
        if (setup == DrumSetup.Percussion)
        {
            var family = DrumGroups.AllDrums.Where(x => x.Family.HasFlag(DrumFamily.Percussion)).ToArray();
            var drums = new List<PercussionInstrumentDefinition>();
            foreach (var role in DrumKitGenerator.LeadRoles)
                drums.AddRange(DrumKitGenerator.PickWeighted(context, family.Where(x => x.MainRole == role).Except(drums).Select(Weighed), 1));
            var more = context.Pick(MorePercussion);
            drums.AddRange(DrumKitGenerator.PickWeighted(context, family.Except(drums).Select(Weighed), more));
            return [..DrumGroups.AllDrums.Where(drums.Contains)];
        }

        var kit = DrumGroups.All.Where(x => x != DrumGroups.Percussion).SelectMany(group =>
            {
                var drums = group.SongRule.Select(context, group.Drums);
                if (group.IsAlwaysOn && drums.IsEmpty)
                    throw new InvalidOperationException($"An always-on drum group '{group.Name}' has no drums in the song.");
                return drums;
            }
        ).ToArray();
        if (setup == DrumSetup.Kit)
            return [..DrumGroups.AllDrums.Where(kit.Contains)];

        var count = context.Pick(PercussionCounts);
        var percussion = DrumKitGenerator.PickWeighted(context, DrumGroups.Percussion.Drums.Select(Weighed), count);
        return [..DrumGroups.AllDrums.Where(x => kit.Contains(x) || percussion.Contains(x))];
    }

    private static Weighted<PercussionInstrumentDefinition> Weighed(PercussionInstrumentDefinition drum) => new(drum.Weight, drum);
}
