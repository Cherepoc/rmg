using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     How loud and busy each section of a song is meant to be, its energy: the song's, a step for how often the section
///     recurs, the more the more chorus-like, one for where it plays on average in the song, rising towards the later
///     parts, and a random step of its own. A section is made once and plays the same wherever it recurs, so it goes by
///     its average place. The energy leans the section's draws, such as its loudness, the drums' fullness and which drums
///     play, and the fills into it, never deciding them; a section follows it as far as its rhythm is conventional
///     (<see cref="RhythmicUnconventionality.Coupling" />), so that in a wild one a loud and sparse section is as likely
///     as not.
/// </summary>
internal static class SectionEnergy
{
    /// <summary>How far the song's energy spreads either way, which moves all its sections alike.</summary>
    public const double Song = 0.25;

    /// <summary>The step of the section that recurs most over the song's average, and less of the others in proportion.</summary>
    public const double Recurrence = 0.5;

    /// <summary>The step of a section at the song's end, and less the earlier it plays, down to its negative at the start.</summary>
    public const double Arc = 0.5;

    /// <summary>How far a section's own random step spreads either way.</summary>
    public const double Section = 0.5;

    /// <summary>The odds of a draw's high side at a pull of 1.</summary>
    public const double HighOdds = 32;

    /// <summary>
    ///     How far an appearance of a section is from the section's energy: the arc's step for how much later or earlier in
    ///     the song it plays than the section does on average, so that its last chorus plays bigger than its first.
    /// </summary>
    public static double AppearanceStep(double place, double averagePlace)
    {
        return Arc * 2 * (place - averagePlace);
    }

    /// <summary>The song's layer of the energy.</summary>
    public static StateMap GenerateSong(IGenerationContext context)
    {
        return new StateMapBuilder("Song")
            .Add(CompositionStateKinds.Energy, Generators.SplineValue().Then(x => x * Song))
            .ToStateMap(context);
    }

    /// <summary>
    ///     Every section's layers of the energy, by its id, from the sections in the song's order: its recurrence, its
    ///     arc and its own step, drawn in the order of the ids.
    /// </summary>
    public static ImmutableDictionary<int, StateMap> GenerateSections(IGenerationContext context, IReadOnlyList<int> sectionIds)
    {
        var places = sectionIds
            .Select((id, i) => (id, place: sectionIds.Count > 1 ? i / (double)(sectionIds.Count - 1) : 0.5))
            .GroupBy(x => x.id)
            .OrderBy(x => x.Key)
            .Select(x => (Id: x.Key, Count: x.Count(), Place: x.Average(p => p.place)))
            .ToArray();
        var meanCount = places.Average(x => x.Count);
        var maxDeviation = places.Max(x => Math.Abs(x.Count - meanCount));
        var own = Generators.SplineValue();

        return places.ToImmutableDictionary(
            x => x.Id,
            x => new StateMapBuilder("Section recurrence")
                .Add(CompositionStateKinds.Energy, maxDeviation > 0 ? Recurrence * (x.Count - meanCount) / maxDeviation : 0)
                .ToStateMap(context)
                .MergeWith(
                    new StateMapBuilder("Section arc")
                        .Add(CompositionStateKinds.Energy, Arc * (2 * x.Place - 1))
                        .ToStateMap(context)
                )
                .MergeWith(
                    new StateMapBuilder("Section")
                        .Add(CompositionStateKinds.Energy, own.Then(v => v * Section))
                        .ToStateMap(context)
                )
        );
    }

    /// <summary>
    ///     How a section's energy leans its draws: its pull, the energy times how far the draws follow it, their
    ///     coupling, such as the section's rhythm's for its loudness and drums and its harmony's for its scale, as the
    ///     odds of the high side, <see cref="HighOdds" /> to the power of the pull.
    /// </summary>
    public static Tilt Tilt(double energy, double coupling)
    {
        return Probabilities.Tilt.Of(HighOdds, energy * coupling);
    }
}

/// <summary>A section's energy as a trace records it: its sum, and its pull, the part of it its rhythm follows.</summary>
public sealed record SectionEnergyTrace(double Energy, double Pull);
