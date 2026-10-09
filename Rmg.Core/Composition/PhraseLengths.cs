using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     How many bars a section's phrase has (<see cref="Meter.PhraseBars" />): four, as nearly every song's are; three,
///     a folk tune's or a ballad's, its four-bar scheme without its second bar; and six, four and two, its last two bars,
///     the preparation and the cadence, played again, so that the cadence goes back to its preparation before it arrives.
///     Drawn by the form facet, the plainest song in fours, and a genre's pool, for a section whose pattern is one phrase:
///     a genre's longer progression counts its phrases in fours.
/// </summary>
internal static class PhraseLengths
{
    /// <summary>The phrase lengths, in bars, and how likely each is at the ends.</summary>
    public static ImmutableArray<(int Bars, ByConvention Weight)> Options { get; } =
    [
        (Meter.DefaultPhraseBars, new ByConvention(1, 0.94, 0.4)),
        (3, new ByConvention(0, 0.02, 0.3)),
        (6, new ByConvention(0, 0.04, 0.3))
    ];

    /// <summary>A length's name, as a genre names it: "threeBars", "fourBars", "sixBars".</summary>
    public static string NameOf(int bars) => bars switch
    {
        3 => "threeBars",
        4 => "fourBars",
        6 => "sixBars",
        _ => throw new ArgumentOutOfRangeException(nameof(bars), bars, null)
    };

    /// <summary>A length as a genre's page shows it.</summary>
    public static string LabelOf(int bars) => bars switch
    {
        3 => "Three bars",
        4 => "Four bars",
        _ => "Four and two"
    };

    /// <summary>A section's phrase length, by the form facet and the genre's pool; four for a pattern of several phrases, drawn all the same.</summary>
    public static int Draw(IGenerationContext context, double unconventionality, Genre genre, int phrases)
    {
        var drawn = context.Pick(ByConvention.Weigh(Options, unconventionality, genre.Pool<int>(GenreChoices.PhraseBars, NameOf)));
        return phrases == 1 ? drawn : Meter.DefaultPhraseBars;
    }

    /// <summary>
    ///     The bars of a phrase of four its phrase plays, in order: every one for four; the first and the last two for
    ///     three, its start and its cadence; and every one and then its last for more, the bars before its end again.
    /// </summary>
    public static ImmutableArray<int> BarsOf(int phraseBars)
    {
        const int four = Meter.DefaultPhraseBars;
        return phraseBars <= four
            ? [0, ..Enumerable.Range(four - phraseBars + 1, phraseBars - 1)]
            : [..Enumerable.Range(0, four), ..Enumerable.Range(2 * four - phraseBars, phraseBars - four)];
    }

    /// <summary>
    ///     A phrase's chords, past four bars, its last bars' again (<see cref="BarsOf" />): every change past the fourth bar
    ///     the chord of the one as many bars before as the phrase plays again.
    /// </summary>
    /// <param name="progression">The chords of the first four bars.</param>
    /// <param name="changes">Where every chord of the phrase starts, in beats.</param>
    public static ImmutableArray<int> Extend(ImmutableArray<int> progression, ImmutableArray<double> changes, Meter meter)
    {
        var again = (meter.PhraseBars - Meter.DefaultPhraseBars) * meter.BarDuration;
        var four = Meter.DefaultPhraseBars * meter.BarDuration;
        return [..progression, ..changes.Where(x => x >= four - 1e-9).Select(x => progression[changes.Count(c => c <= x - again + 1e-9) - 1])];
    }
}
