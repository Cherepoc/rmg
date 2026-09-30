using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     How far the harmony of a song or a section strays from convention. Its chords' shapes by the chords facet of its
///     unconventionality (<see cref="Chords" />), from 0 to 1, through the levels' plain, tuned and wild weights
///     (<see cref="ChordShapes.Levels" />); its scales still by its anchor, from 0, the most conventional, to
///     <see cref="ChordShapes.MaxUnconventionality" />, until they go by a facet of their own.
/// </summary>
/// <param name="Anchor">How often a section plays another scale.</param>
/// <param name="Chords">The chords facet of its unconventionality, from 0 to 1.</param>
public sealed record HarmonicUnconventionality(double Anchor, double Chords)
{
    private static readonly Func<IGenerationContext, double> AnchorGenerator =
        Generators.AbsSplineValue().Then(x => x * ChordShapes.MaxUnconventionality);

    private static readonly Func<IGenerationContext, double> SectionShiftGenerator = Generators.SplineValue();

    /// <summary>A song's: its anchor, below 1 in about 60% of songs and above 2.5 in about 13%, and its chords facet.</summary>
    public static HarmonicUnconventionality Generate(IGenerationContext context, double chords)
    {
        return new HarmonicUnconventionality(AnchorGenerator(context), chords);
    }

    /// <summary>
    ///     How much of what a section's energy leans it to the harmony follows, as the rhythm's
    ///     (<see cref="RhythmicUnconventionality.Coupling" />): all at an anchor of 0, a fifth at the most unconventional.
    /// </summary>
    public double Coupling =>
        1 - RhythmicUnconventionality.MaxDecoupling * Math.Clamp(Anchor / ChordShapes.MaxUnconventionality, 0, 1);

    /// <summary>A section's: the song's, with the anchor moved by up to 1 either way, and the section's chords facet.</summary>
    public HarmonicUnconventionality GenerateSection(IGenerationContext context, double chords)
    {
        return new HarmonicUnconventionality(Anchor + SectionShiftGenerator(context), chords);
    }

    /// <summary>
    ///     A chord: a shape of a level drawn by the chords facet, laid out by a voicing, as the heights of its notes above
    ///     the root in fractions of an octave, which <c>Render</c> snaps to the scale.
    /// </summary>
    public Chord GenerateChord(IGenerationContext context)
    {
        return Voice(context, ChordShapes.Pick(context, ChordShapes.Levels, Chords));
    }

    /// <summary>The home chord of a phrase, which stays plain for the release but in the wildest songs (<see cref="ChordShapes.HomeLevels" />).</summary>
    public Chord GenerateHomeChord(IGenerationContext context)
    {
        return Voice(context, ChordShapes.Pick(context, ChordShapes.HomeLevels, Chords));
    }

    /// <summary>
    ///     The cadence chord of a phrase, which pulls towards home, a shape made for it, or keeps a strange song's
    ///     strangeness there (<see cref="ChordShapes.CadenceLevels" />).
    /// </summary>
    public Chord GenerateCadenceChord(IGenerationContext context)
    {
        return Voice(context, ChordShapes.PickCadence(context, Chords));
    }

    private static Chord Voice(IGenerationContext context, ChordShape shape)
    {
        return new Chord([..ChordVoicing.Apply(context, shape).Select(x => x / 12)], shape.IsVoicingFixed, shape);
    }
}
