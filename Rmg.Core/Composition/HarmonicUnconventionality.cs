using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     How far the harmony of a song or a section strays from convention, 0 being the most conventional. A chord
///     draws a level of <c>anchor + spread · SplineValue(peak, skew)</c> and plays a shape of that unconventionality,
///     rounded and clamped to the levels of <see cref="ChordShapes" />. The anchor is the level the chords gather
///     around and the spread how far they stray from it. The peak is the spline's c: at 1 the chords keep close to the
///     anchor, and lower flattens them, so that with a peak of 0 and a spread of 2 every level is about as likely. A
///     skew below 1 tips them less conventional, above 1 more. Draws below the lowest level play triads, so a
///     conventional song stays conventional.
/// </summary>
public sealed record HarmonicUnconventionality(double Anchor, double Spread, double Peak, double Skew)
{
    private static readonly Func<IGenerationContext, double> AnchorGenerator =
        Generators.AbsSplineValue().Then(x => x * ChordShapes.MaxUnconventionality);

    private static readonly Func<IGenerationContext, double> SpreadGenerator =
        Generators.SplineValue().Then(x => Math.Pow(2, 1.5 * x));

    private static readonly Func<IGenerationContext, double> PeakGenerator =
        Generators.AbsSplineValue().Then(x => 1 - x);

    private static readonly Func<IGenerationContext, double> SkewGenerator =
        Generators.SplineValue().Then(x => Math.Pow(2, x));

    private static readonly Func<IGenerationContext, double> SectionShiftGenerator = Generators.SplineValue();

    /// <summary>
    ///     A song's unconventionality. The anchor is below 1 in about 60% of songs and above 2.5 in about 13%, so most
    ///     songs are conventional and a few are jazzy or stranger. The spread goes from about a third to about 3, and the skew from
    ///     1/2 to 2, most of them near 1; the peak is near 1 in most songs and below 1/2, flat, in about 13%.
    /// </summary>
    public static HarmonicUnconventionality Generate(IGenerationContext context)
    {
        return new HarmonicUnconventionality(
            AnchorGenerator(context),
            SpreadGenerator(context),
            PeakGenerator(context),
            SkewGenerator(context)
        );
    }

    /// <summary>A section's unconventionality: the song's, with the anchor moved by up to 1 either way.</summary>
    public HarmonicUnconventionality GenerateSection(IGenerationContext context)
    {
        return this with { Anchor = Anchor + SectionShiftGenerator(context) };
    }

    /// <summary>
    ///     A chord: a shape of the unconventionality drawn, laid out by a voicing, as the heights of its notes above the root in
    ///     fractions of an octave, which <c>Render</c> snaps to the scale.
    /// </summary>
    public ImmutableArray<double> GenerateChord(IGenerationContext context)
    {
        return Voice(context, ChordShapes.Pick(context, GenerateChordUnconventionality(context)));
    }

    /// <summary>
    ///     The home chord of a phrase, which stays plain for the release: its unconventionality is at most
    ///     <see cref="HomeChordMaxUnconventionality" />, a triad or a mild colour such as a seventh, an added ninth or a
    ///     suspended chord, however strange the song.
    /// </summary>
    public ImmutableArray<double> GenerateHomeChord(IGenerationContext context)
    {
        var level = Math.Min(GenerateChordUnconventionality(context), HomeChordMaxUnconventionality);
        return Voice(context, ChordShapes.Pick(context, level));
    }

    /// <summary>
    ///     The cadence chord of a phrase, which pulls towards home for the tension: a shape made for it, such as a
    ///     seventh, unless the chord drawn is more unconventional than <see cref="CadenceShapeMaxUnconventionality" />,
    ///     so that a strange song keeps its strangeness there.
    /// </summary>
    public ImmutableArray<double> GenerateCadenceChord(IGenerationContext context)
    {
        var level = GenerateChordUnconventionality(context);
        var shape = level <= CadenceShapeMaxUnconventionality ? ChordShapes.PickCadence(context) : ChordShapes.Pick(context, level);
        return Voice(context, shape);
    }

    /// <summary>The most unconventional a home chord is.</summary>
    public const int HomeChordMaxUnconventionality = 1;

    /// <summary>The most unconventional chord drawn for a cadence that plays a cadence shape instead.</summary>
    public const int CadenceShapeMaxUnconventionality = 2;

    private static ImmutableArray<double> Voice(IGenerationContext context, ChordShape shape)
    {
        return [..ChordVoicing.Apply(context, shape).Select(x => x / 12)];
    }

    /// <summary>
    ///     How closely the progressions keep to their rules (see <see cref="Progressions" />): 1 for an anchor of 0,
    ///     falling evenly to 0 for an anchor at the most unconventional level, where every root is as likely.
    /// </summary>
    public double ProgressionStrictness => Math.Clamp(1 - Anchor / ChordShapes.MaxUnconventionality, 0, 1);

    /// <summary>The unconventionality of a chord, as a level of <see cref="ChordShapes" />.</summary>
    public int GenerateChordUnconventionality(IGenerationContext context)
    {
        var level = Anchor + Spread * Generators.SplineValue(Peak, Skew)(context);
        return Math.Clamp((int)Math.Round(level, MidpointRounding.AwayFromZero), 0, ChordShapes.MaxUnconventionality);
    }
}
