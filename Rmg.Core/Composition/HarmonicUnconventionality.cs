using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     How far the harmony of a song or a section strays from convention in its chords: their shapes by the chords facet
///     of its unconventionality, from 0 to 1, through the levels' plain, tuned and wild weights
///     (<see cref="ChordShapes.Levels" />).
/// </summary>
/// <param name="Chords">The chords facet of its unconventionality, from 0 to 1.</param>
/// <param name="Genre">The genre whose pool of the shapes its chords keep to.</param>
public sealed record HarmonicUnconventionality(double Chords, Genre Genre)
{
    // the genre's pool of the shapes, their middles and their leans, none where it names none
    private ChordShapes.GenrePool? Pool => Genre.Pool<ChordShape>(GenreChoices.ChordShape, ShapeName) is { } middle
        ? new ChordShapes.GenrePool(middle, Genre.Lean<ChordShape>(GenreChoices.ChordShape, ShapeName)!)
        : null;

    private static string ShapeName(ChordShape shape) => GenreChoices.Name(shape.Name);

    /// <summary>
    ///     A chord: a shape of a level drawn by the chords facet, laid out by a voicing, as the heights of its notes above
    ///     the root in fractions of an octave, which <c>Render</c> snaps to the scale.
    /// </summary>
    public Chord GenerateChord(IGenerationContext context)
    {
        return Voice(context, ChordShapes.Pick(context, ChordShapes.Levels, Chords, Pool, isOwn: false, Genre.Mood));
    }

    /// <summary>The home chord of a phrase, which stays plain for the release but in the wildest songs (<see cref="ChordShapes.HomeLevels" />).</summary>
    public Chord GenerateHomeChord(IGenerationContext context)
    {
        return Voice(context, ChordShapes.Pick(context, ChordShapes.HomeLevels, Chords, Pool, isOwn: true, Genre.Mood));
    }

    /// <summary>
    ///     The cadence chord of a phrase, which pulls towards home, a shape made for it, or keeps a strange song's
    ///     strangeness there (<see cref="ChordShapes.CadenceLevels" />).
    /// </summary>
    public Chord GenerateCadenceChord(IGenerationContext context)
    {
        return Voice(context, ChordShapes.PickCadence(context, Chords, Pool, Genre.Mood));
    }

    private Chord Voice(IGenerationContext context, ChordShape shape)
    {
        return new Chord([..ChordVoicing.Apply(context, shape, Chords).Select(x => x / 12)], shape.IsVoicingFixed, shape);
    }
}
