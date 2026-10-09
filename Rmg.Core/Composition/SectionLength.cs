using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     How many times a section plays its 4-bar pattern: twice the most, its melody a question and its answer, and now
///     and then once, a phrase alone, as a short section between others plays, or four times, as a long chorus or a
///     groove that settles plays. A section's role leans it to its own length, a verse and a chorus to sixteen bars, a
///     pre-chorus to four and a bridge to eight, which is where the convention is, so that no length leans by the form's
///     unconventionality: every one is allowed at both ends.
/// </summary>
internal static class SectionLength
{
    /// <summary>
    ///     The plays and how likely each is at the ends, every one allowed at both, the roles holding the convention; and
    ///     eight, thirty-two bars, a dance track's section, a genre's that the songs never play.
    /// </summary>
    public static ImmutableArray<(int Plays, ByConvention Weight)> Options { get; } =
    [
        (1, RhythmicUnconventionality.WeightEnds(0.2, 0)),
        (2, RhythmicUnconventionality.WeightEnds(0.65, 0)),
        (4, RhythmicUnconventionality.WeightEnds(0.15, 0)),
        (8, default)
    ];

    /// <summary>A length's name, as a genre names it: "bars16", the bars it plays.</summary>
    public static string NameOf(int plays) => $"bars{plays * Meter.DefaultPhraseBars}";

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
            SectionRole.Intro or SectionRole.Outro => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "A role with no length.")
        };
    }

    /// <summary>
    ///     How far a song's length given leans its sections' plays, as the odds of a doubling over its halving at a lean of
    ///     1, so that a song kept short plays its sections once and one drawn out four times (<see cref="SongLength" />).
    /// </summary>
    public const double LengthOdds = 4;

    /// <param name="lean">How far the song's length leans the plays, from below 0, shorter, to above, longer; 0 for none.</param>
    /// <param name="genre">The genre whose pool of lengths the plays are drawn among, where it names one.</param>
    public static int Draw(IGenerationContext context, double unconventionality, SectionRole role, double lean, Genre genre)
    {
        return context.Pick(Weights(unconventionality, role, lean, genre));
    }

    /// <summary>The plays a section of a role plays on the average, at a lean of the length (<see cref="Draw" />).</summary>
    public static double Expected(double unconventionality, SectionRole role, double lean, Genre genre)
    {
        var weights = Weights(unconventionality, role, lean, genre);
        return weights.Sum(x => x.Weight * x.Value) / weights.Sum(x => x.Weight);
    }

    private static ImmutableArray<Weighted<int>> Weights(double unconventionality, SectionRole role, double lean, Genre genre)
    {
        var rolePlays = GetRolePlays(role);
        var weights = ByConvention.Weigh(Options, unconventionality, genre.Pool<int>(GenreChoices.SectionLength, NameOf));
        weights = Tilt.Of(RoleOdds, 1).Weigh(weights, plays => plays == rolePlays ? 1 : 0);
        return lean == 0 ? weights : Tilt.Of(LengthOdds, lean).Weigh(weights, plays => Math.Log2(plays) - 1);
    }
}
