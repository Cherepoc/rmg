using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>A side of the music that strays from convention by its own measure, every choice leaning by one of them.</summary>
public enum Facet
{
    /// <summary>The tuplets and groupings the rhythm plays in, the meter and the swing.</summary>
    Feel,

    /// <summary>How the rhythm moves its speed, its phase and its density, and the drums' roles.</summary>
    Groove,

    /// <summary>The fills: their spans, what they do with the groove, their runs.</summary>
    Fills,

    /// <summary>The song's form, its intro and ending, the sections' lengths and the parts they leave out.</summary>
    Form,

    /// <summary>The chords' shapes.</summary>
    Chords,

    /// <summary>How closely the progressions keep to their rules.</summary>
    Progression,

    /// <summary>The scales, the pentatonic melodies and the key change.</summary>
    Scale,

    /// <summary>The melody's improvisation and its answers.</summary>
    Melody
}

/// <summary>
///     How far a song or a section strays from convention, from 0, the plainest, to 1, the wildest: a base, and a value
///     for every facet drawn around it, each from a sequence of its own, so that a song may be plain in one way and wild
///     in another. The facets stray the less the nearer the base is to an end, and not at all there, so that a base of 0
///     makes every facet 0 and one of 1 every facet 1. A section moves every facet a little, and not at all at an end.
/// </summary>
/// <param name="Base">The song's value, which the facets are drawn around.</param>
/// <param name="Facets">Every facet's value.</param>
public sealed record Unconventionality(double Base, ImmutableDictionary<Facet, double> Facets)
{
    /// <summary>
    ///     How far a facet strays from the base, either way, with the base in the middle, so that a middling song's facets
    ///     stray from it by up to about 0.27 nine times in ten, and chords and groove go together about as closely as 0.67.
    /// </summary>
    public const double FacetSpread = 0.6;

    /// <summary>How far a section moves a facet, either way, with the facet in the middle.</summary>
    public const double SectionShift = 0.15;

    private static readonly Func<IGenerationContext, double> ShiftGenerator = Generators.SplineValue();

    /// <summary>
    ///     How much of what a section's energy leans it to follows in a facet: all at 0, a fifth at 1, so that in a wild
    ///     facet a loud section may be plain in it and a quiet one wild (<see cref="RhythmicUnconventionality.Coupling" />).
    /// </summary>
    public static double Coupling(double facet) => 1 - RhythmicUnconventionality.MaxDecoupling * facet;

    /// <summary>A facet's value.</summary>
    public double this[Facet facet] => Facets[facet];

    /// <summary>A song's: the base given, and every facet drawn around it from its own sequence.</summary>
    /// <param name="streams">The sequence of every facet.</param>
    public static Unconventionality Generate(double @base, Func<Facet, IGenerationContext> streams)
    {
        return new Unconventionality(@base, Enum.GetValues<Facet>().ToImmutableDictionary(x => x, x => Around(@base, FacetSpread, streams(x))));
    }

    /// <summary>A section's: every facet of the song's moved a little, each from its own sequence.</summary>
    public Unconventionality GenerateSection(Func<Facet, IGenerationContext> streams)
    {
        return this with { Facets = Facets.ToImmutableDictionary(x => x.Key, x => Around(x.Value, SectionShift, streams(x.Key))) };
    }

    /// <summary>
    ///     A value around the one given, by up to the spread either way where it is 0.5 and the less the nearer it is to an
    ///     end, where it stays.
    /// </summary>
    private static double Around(double value, double spread, IGenerationContext context)
    {
        return Math.Clamp(value + spread * 4 * value * (1 - value) * ShiftGenerator(context), 0, 1);
    }
}

/// <summary>What a test sets in place of the song's own draws; none for the song as its seed makes it.</summary>
/// <param name="Meter">The meter the song's bars are in.</param>
/// <param name="Base">The song's unconventionality's base, which its facets are drawn around.</param>
/// <param name="Facets">Facets set outright, over those drawn.</param>
internal sealed record SongOverrides(Meter? Meter = null, double? Base = null, ImmutableDictionary<Facet, double>? Facets = null)
{
    public static SongOverrides None { get; } = new();
}
