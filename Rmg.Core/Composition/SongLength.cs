using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     A song's length given, in seconds, which the draws that make its form lean to, none deciding and each drawing as
///     often as without it: the form, by how near its length comes (<see cref="Closeness" />), a free form's parts, and
///     its sections' plays (<see cref="SectionLength" />), the one lean of them whose length comes nearest.
/// </summary>
/// <param name="Target">The length given, in seconds.</param>
/// <param name="SecondsAt">
///     How long an appearance of a section of a role plays on the average at a lean of the plays, in seconds, before the
///     song's sections are drawn.
/// </param>
internal sealed record SongLength(double Target, Func<SectionRole, double, double> SecondsAt)
{
    /// <summary>How far a length may be from the one given and still weigh, as the spread of the ratio's logarithm: three quarters of it or a third more weighs a sixth, half or twice it next to nothing.</summary>
    public const double Spread = 0.15;

    /// <summary>How long a song plays in the middle of the songs, in seconds, as drawn (<c>SongLengthReportTest</c>).</summary>
    public const double Typical = 158;

    /// <summary>
    ///     The lean of the sections' plays a form is planned at for a length: half of how many times it doubles the
    ///     songs' typical length, so that a long song is as much longer sections as more of them, and a short one shorter
    ///     and fewer.
    /// </summary>
    public static double PlanningLean(double target) => Math.Clamp(Math.Log2(target / Typical) / 2, Leans[0], Leans[^1]);

    /// <summary>The leans of the sections' plays the song tries, the nearest taken.</summary>
    public static ImmutableArray<double> Leans { get; } = [..Enumerable.Range(-12, 25).Select(x => x / 4.0)];

    /// <summary>
    ///     The chance a song takes one of the forms, leaned by how near the nearest of them comes, a free form coming as
    ///     near as any by its parts (<see cref="SongStructureGenerator" />), so that a song too long for any form is a free
    ///     one the likelier.
    /// </summary>
    public double FormChance(double chance, IEnumerable<IEnumerable<SectionRole>> forms)
    {
        var nearest = forms.Max(x => Closeness(Math.Clamp(Target, x.Sum(r => SecondsAt(r, Leans[0])), x.Sum(r => SecondsAt(r, Leans[^1])))));
        // the chance's odds times the closeness, exact at a chance of 0 or 1
        return new Tilt(Math.Log(nearest)).Chance(chance, 1);
    }

    /// <summary>How long an appearance of a section of a role plays on the average at the lean a form is planned at (<see cref="PlanningLean" />).</summary>
    public double AppearanceSeconds(SectionRole role) => SecondsAt(role, PlanningLean(Target));

    /// <summary>How much a length weighs against the one given: 1 at it, less the farther, by the ratio either way.</summary>
    public double Closeness(double seconds) => Math.Exp(-Math.Pow(Math.Log(seconds / Target), 2) / (2 * Spread * Spread));

    /// <summary>The lean of the sections' plays whose length comes nearest, the smallest of those as near.</summary>
    public double NearestLean(Func<double, double> secondsAt) =>
        Leans.OrderBy(x => Math.Abs(Math.Log(secondsAt(x) / Target))).ThenBy(Math.Abs).First();
}
