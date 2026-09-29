using System.Collections.Immutable;

namespace Rmg.Core.Composition;

/// <summary>
///     How often a section's chords change: every chord as long as the others, a span of beats that the section's
///     4-bar pattern holds a whole number of, such as a chord a bar, two a bar or one every two bars.
/// </summary>
/// <param name="Span">How long a chord lasts, in beats.</param>
internal sealed record HarmonicRhythm(double Span)
{
    /// <summary>A chord a bar.</summary>
    public static HarmonicRhythm OneABar { get; } = new(Meter.BarDuration);

    /// <summary>How many chords the pattern has.</summary>
    public int Count => (int)Math.Round(Meter.PatternDuration / Span);

    /// <summary>Where every chord starts in the pattern, in beats.</summary>
    public ImmutableArray<double> Changes => [..Enumerable.Range(0, Count).Select(x => x * Span)];

    /// <summary>The chord playing at a place in the section, of its pattern's chords, the pattern played again after it.</summary>
    public int IndexAt(double position)
    {
        return Math.Clamp((int)Math.Floor(position.Mod(Meter.PatternDuration) / Span + 1e-9), 0, Count - 1);
    }
}
