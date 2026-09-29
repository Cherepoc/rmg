using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     How many times a section plays its 4-bar pattern: twice the most, its melody a question and its answer, and now
///     and then once, a phrase alone, as a short section between others plays, or four times, as a long chorus or a
///     groove that settles plays, both the likelier the less conventional the section's rhythm.
/// </summary>
internal static class SectionLength
{
    /// <summary>The plays, how often, and how each leans: 1 away from convention, 0 not at all.</summary>
    public static ImmutableArray<(Weighted<int> Plays, double Lean)> Options { get; } =
    [
        (new Weighted<int>(0.2, 1), 1),
        (new Weighted<int>(0.65, 2), 0),
        (new Weighted<int>(0.15, 4), 1)
    ];

    public static int Draw(IGenerationContext context, Tilt rhythm)
    {
        var leans = Options.ToDictionary(x => x.Plays.Value, x => x.Lean);
        return context.Pick(rhythm.Weigh(Options.Select(x => x.Plays), plays => leans[plays]));
    }
}
