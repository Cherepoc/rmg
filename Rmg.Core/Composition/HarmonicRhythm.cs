using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     How often a section's chords change: every chord as long as the others, a number of bars that the section's
///     4-bar pattern holds a whole number of, such as a chord a bar, two a bar or one every two bars, in the section's
///     meter.
/// </summary>
/// <param name="Bars">How long a chord lasts, in bars.</param>
internal sealed record HarmonicRhythm(double Bars, Meter Meter)
{
    /// <summary>How long a chord lasts, in beats.</summary>
    public double Span => Bars * Meter.BarDuration;

    /// <summary>
    ///     The spans a section's chords may last, in bars, and how often: a chord a bar the most, one every two bars,
    ///     as a loop of two chords plays, now and then, and two a bar rarely; each leans by how fast it is, from -1, the
    ///     slowest, to 1, the fastest, so that a section with more energy changes its chords faster.
    /// </summary>
    public static ImmutableArray<(Weighted<double> Span, double Lean)> Spans { get; } =
    [
        (new Weighted<double>(0.25, 2), -1),
        (new Weighted<double>(0.6, 1), 0),
        (new Weighted<double>(0.15, 0.5), 1)
    ];

    /// <summary>A section's harmonic rhythm in its meter, leaned by its energy's pull.</summary>
    public static HarmonicRhythm Draw(IGenerationContext context, Tilt energy, Meter meter)
    {
        var leans = Spans.ToDictionary(x => x.Span.Value, x => x.Lean);
        return new HarmonicRhythm(context.Pick(energy.Weigh(Spans.Select(x => x.Span), span => leans[span])), meter);
    }

    /// <summary>How many chords the pattern has.</summary>
    public int Count => (int)Math.Round(Meter.PatternDuration / Span);

    /// <summary>
    ///     Where every chord starts in the pattern, in beats: every span on from the pattern's start, on the start of the
    ///     group nearest it, so that two chords a bar change at 4/4's and 6/8's middle and on 3/4's third beat.
    /// </summary>
    public ImmutableArray<double> Changes => [..Enumerable.Range(0, Count).Select(x => Meter.NearestGroupStart(x * Span))];

    /// <summary>How long a chord of the pattern lasts, in beats, up to the next chord's start.</summary>
    public double LengthOf(int index)
    {
        var changes = Changes;
        return (index + 1 < changes.Length ? changes[index + 1] : Meter.PatternDuration) - changes[index];
    }

    /// <summary>The chord playing at a place in the section, of its pattern's chords, the pattern played again after it.</summary>
    public int IndexAt(double position)
    {
        var inPattern = position.Mod(Meter.PatternDuration);
        return Math.Clamp(Changes.Count(x => x <= inPattern + 1e-9) - 1, 0, Count - 1);
    }
}
