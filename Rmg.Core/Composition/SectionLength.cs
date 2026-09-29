using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     How many times a section plays its 4-bar pattern: twice the most, its melody a question and its answer, and now
///     and then once, a phrase alone, as a short section between others plays, or four times, as a long chorus or a
///     groove that settles plays, both the likelier the less conventional the section's rhythm; a section's role leans
///     it to its own length, a verse and a chorus to sixteen bars, a pre-chorus to four and a bridge to eight.
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

    /// <summary>How far a role leans its section to the length it plays, as the odds of that length over the others.</summary>
    public const double RoleOdds = 8;

    /// <summary>The plays a section of a role leans to, none for a section with no role.</summary>
    public static int? GetRolePlays(SectionRole role)
    {
        return role switch
        {
            SectionRole.Free => null,
            SectionRole.Verse or SectionRole.Chorus => 4,
            SectionRole.PreChorus => 1,
            SectionRole.Bridge => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "A role with no length.")
        };
    }

    public static int Draw(IGenerationContext context, Tilt rhythm, SectionRole role)
    {
        var leans = Options.ToDictionary(x => x.Plays.Value, x => x.Lean);
        var rolePlays = GetRolePlays(role);
        var weights = rhythm.Weigh(Options.Select(x => x.Plays), plays => leans[plays]);
        return context.Pick(Tilt.Of(RoleOdds, 1).Weigh(weights, plays => plays == rolePlays ? 1 : 0));
    }
}
