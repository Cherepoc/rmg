using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     A section in a meter of its own, every time it plays, by the feel facet of the song's unconventionality: never in
///     the plainest song, whose sections all keep its meter; rarely at the middle, a chorus or a bridge the likelier; and
///     every section but the first at the wild end. Its meter is another of the song's options, as the facet weighs them,
///     its groups in an order of their own; a meter given is the song's, which its sections stray from as a drawn one's.
/// </summary>
internal static class SectionMeter
{
    /// <summary>The chance a section, but the song's first, is in a meter of its own, before its contrast leans it.</summary>
    public static ByConvention Chance { get; } = new(0, 0.02, 1);

    /// <summary>Every section's meter, by its id, each drawing from a sequence of its own.</summary>
    /// <param name="sections">Every section's draws, by its id.</param>
    /// <param name="song">The song's meter, which its first section is in.</param>
    /// <param name="unconventionality">The feel facet of the song's unconventionality.</param>
    /// <param name="sectionIds">The song's sections in its order.</param>
    /// <param name="roles">What each section does in the song's form, by its id.</param>
    public static ImmutableDictionary<int, Meter> Generate(
        Func<int, IGenerationContext> sections,
        Meter song,
        double unconventionality,
        IReadOnlyList<int> sectionIds,
        IReadOnlyDictionary<int, SectionRole> roles
    )
    {
        var songOption = Meter.OptionOf(song);
        return sectionIds.Distinct().ToImmutableDictionary(id => id, id =>
        {
            var drawing = sections(id);
            var hasOwn = drawing.TestProbability(SectionContrast.Of(roles[id]).Chance(Chance.At(unconventionality), 1));
            // the first section sets the song's meter
            if (!hasOwn || id == sectionIds[0])
                return song;

            // another of the options the facet allows, drawn from the section's own sequence, whose draws touch no other's
            var others = ByConvention.Weigh(Meter.Options.Where((_, i) => i != songOption).Select(x => (x.Meter, x.Weight)), unconventionality);
            return Meter.InAnOrder(drawing, drawing.Pick(others));
        });
    }
}
