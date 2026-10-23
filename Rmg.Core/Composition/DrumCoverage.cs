using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     Every drum of the song's groove played in a section at least: a drum the song has that no section's kit took joins
///     the kit of one it fits whose drums play where it first plays, drawn from a sequence of its own, the likelier the
///     more often it plays, so that a drum the song has, drawn or given, is heard.
///     A drum of the kit fits a section of the kit, and a hand or percussion drum any; where no section plays the kit,
///     one drawn plays it again, its kit drawn as a kit section's, with the drums joined to it before. The drums of the fills and landings, which all but
///     never groove, are heard where they fill or land, and a drum the form leaves out of every section that has it
///     lands once after all (<see cref="FillGenerator" />).
/// </summary>
internal static class DrumCoverage
{
    /// <param name="context">The sequence the sections the drums join are drawn from, drawn from only where one is missing.</param>
    /// <param name="songDrums">The drums the song has.</param>
    /// <param name="sections">Every section's drums, as drawn (<see cref="SectionGenerator.DrawDrums" />).</param>
    /// <param name="plays">How many times every section plays in the song.</param>
    /// <param name="asKit">A section's drums drawn again as a section of the kit.</param>
    public static ImmutableDictionary<int, SectionDrums> Cover(
        IGenerationContext context,
        ImmutableArray<PercussionInstrumentDefinition> songDrums,
        ImmutableDictionary<int, SectionDrums> sections,
        IReadOnlyDictionary<int, int> plays,
        Func<int, SectionDrums> asKit
    )
    {
        var covered = sections.ToBuilder();
        // the sections whose drums play where they first play, rather than rest for a breakdown, or all where none do
        int[] playing = [..sections.Keys.Order().Where(x => sections[x].PlaysDrums)];
        var ids = playing.Length > 0 ? playing : [..sections.Keys.Order()];
        // the drums each section was joined, which a section played as a kit again keeps
        var joins = new Dictionary<int, List<PercussionInstrumentDefinition>>();
        void Join(int id, IEnumerable<PercussionInstrumentDefinition> drums)
        {
            var kit = covered[id].Kit;
            var added = drums.ToHashSet();
            covered[id] = covered[id] with { Kit = kit with { Drums = [..DrumGroups.AllDrums.Where(x => kit.Drums.Contains(x) || added.Contains(x))] } };
        }

        foreach (var drum in songDrums.Where(x => DrumGroups.GroupOf(x).GrooveChance >= 1))
        {
            if (ids.Length == 0 || ids.Any(id => covered[id].Kit.Drums.Contains(drum)))
                continue;

            var fits = ids.Where(id => Fits(drum, covered[id])).ToArray();
            if (fits.Length == 0)
            {
                // a drum of the kit where every section plays percussion only: one of them plays the kit again
                var id = context.Pick([..ids.Select(x => new Weighted<int>(plays[x], x))]);
                covered[id] = asKit(id);
                Join(id, joins.GetValueOrDefault(id, []));
                if (covered[id].Kit.Drums.Contains(drum))
                    continue;
                fits = [id];
            }

            // the likelier the more often the section plays, as the form leaves a drum out of a section now and then, such
            // as while the intro brings the parts in
            var joined = context.Pick([..fits.Select(id => new Weighted<int>(plays[id], id))]);
            Join(joined, [drum]);
            (joins.TryGetValue(joined, out var list) ? list : joins[joined] = []).Add(drum);
        }

        return covered.ToImmutable();
    }

    private static bool Fits(PercussionInstrumentDefinition drum, SectionDrums section) =>
        !section.IsPercussionOnly || drum.Family.HasFlag(DrumFamily.Percussion);
}
