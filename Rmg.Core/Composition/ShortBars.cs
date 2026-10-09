using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     A section's last bar cut short before the next, as a 2/4 bar comes before a chorus: the bar's first half of its
///     groups, a 4/4 bar's first two beats, a 6/8 bar's first dotted quarter, wherever the section plays but at the song's
///     end, and at its start where an intro's entries come in over the first section's bars. A section's decision, by
///     the form facet, never in the plainest song, and a genre's, each section from a stream of its own; its bars before
///     the cut are as they were, every bar counted from its section's start.
/// </summary>
internal static class ShortBars
{
    /// <summary>The chance a section's last bar is cut short, by the form facet.</summary>
    public static ByConvention Chance { get; } = new(0, 0.04, 0.5);

    /// <summary>The sections whose last bar is cut short, each drawn from a stream of its own.</summary>
    /// <param name="sections">A section's stream, by its id.</param>
    /// <param name="sectionIds">The song's sections, each once.</param>
    /// <param name="unconventionality">The form facet of the song's unconventionality.</param>
    public static ImmutableHashSet<int> Draw(Func<int, IGenerationContext> sections, IEnumerable<int> sectionIds, double unconventionality, Genre genre)
    {
        var chance = Chance.At(unconventionality, genre.Chance(GenreChoices.SectionChanges, "shortBar"));
        return [..sectionIds.Where(id => sections(id).TestProbability(chance))];
    }

    /// <summary>The short bar of a meter: the first half of its groups, the odd one with them, as a meter of its own.</summary>
    public static Meter Of(Meter meter) => new([..meter.Groups.Take((meter.Groups.Length + 1) / 2)]);

    /// <summary>How much of a bar of the meter its short bar leaves out, in beats.</summary>
    public static double Cut(Meter meter) => meter.BarDuration - Of(meter).BarDuration;
}
