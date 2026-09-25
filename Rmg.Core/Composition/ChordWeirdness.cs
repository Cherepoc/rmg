using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     How weird the chords of a song or a section are. A chord draws a level of
///     <c>anchor + spread · SplineValue(peak, skew)</c> and plays a shape of that rank, rounded and clamped to the
///     ranks of <see cref="ChordShapes" />. The anchor is the level the chords gather around and the spread how far
///     they stray from it. The peak is the spline's c: at 1 the chords keep close to the anchor, and lower flattens
///     them, so that with a peak of 0 and a spread of 2 every rank is about as likely. A skew below 1 tips them
///     weirder, above 1 plainer. Draws below the lowest rank play triads, so a plain song stays plain.
/// </summary>
public sealed record ChordWeirdness(double Anchor, double Spread, double Peak, double Skew)
{
    private static readonly Func<IGenerationContext, double> AnchorGenerator =
        Generators.AbsSplineValue().Then(x => x * ChordShapes.MaxRank);

    private static readonly Func<IGenerationContext, double> SpreadGenerator =
        Generators.SplineValue().Then(x => Math.Pow(2, 1.5 * x));

    private static readonly Func<IGenerationContext, double> PeakGenerator =
        Generators.AbsSplineValue().Then(x => 1 - x);

    private static readonly Func<IGenerationContext, double> SkewGenerator =
        Generators.SplineValue().Then(x => Math.Pow(2, x));

    private static readonly Func<IGenerationContext, double> SectionShiftGenerator = Generators.SplineValue();

    /// <summary>
    ///     A song's weirdness. The anchor is below 1 in about 60% of songs and above 2.5 in about 13%, so most songs
    ///     are plain and a few are jazzy or stranger. The spread goes from about a third to about 3, and the skew from
    ///     1/2 to 2, most of them near 1; the peak is near 1 in most songs and below 1/2, flat, in about 13%.
    /// </summary>
    public static ChordWeirdness Generate(IGenerationContext context)
    {
        return new ChordWeirdness(
            AnchorGenerator(context),
            SpreadGenerator(context),
            PeakGenerator(context),
            SkewGenerator(context)
        );
    }

    /// <summary>A section's weirdness: the song's, with the anchor moved by up to 1 either way.</summary>
    public ChordWeirdness GenerateSection(IGenerationContext context)
    {
        return this with { Anchor = Anchor + SectionShiftGenerator(context) };
    }

    /// <summary>
    ///     A chord: a shape of the rank drawn, laid out by a voicing, as the heights of its notes above the root in
    ///     fractions of an octave, which <c>Render</c> snaps to the scale.
    /// </summary>
    public ImmutableArray<double> GenerateChord(IGenerationContext context)
    {
        var shape = ChordShapes.Pick(context, GenerateRank(context));
        return [..ChordVoicing.Apply(context, shape).Select(x => x / 12)];
    }

    public int GenerateRank(IGenerationContext context)
    {
        var level = Anchor + Spread * Generators.SplineValue(Peak, Skew)(context);
        return Math.Clamp((int)Math.Round(level, MidpointRounding.AwayFromZero), 0, ChordShapes.MaxRank);
    }
}
