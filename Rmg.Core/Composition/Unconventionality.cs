using System.Collections.Immutable;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

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

    /// <summary>The scales, the melodies' and riffs' line scales and the key change.</summary>
    Scale,

    /// <summary>The melody's improvisation and its answers.</summary>
    Melody,

    /// <summary>How the parts sound: their instruments over the song, articulations, bends and vibrato, and effects.</summary>
    Sound
}

/// <summary>
///     How far a song or a section strays from convention, from 0, the plainest, to 1, the wildest: a base, and a value
///     for every facet drawn around it, each from a sequence of its own, so that a song may be plain in one way and wild
///     in another. Every value strays in log-odds, the less the nearer it is to an end and not at all there, so that a
///     base of 0 makes every facet 0 and one of 1 every facet 1; a section moves every facet a little the same way.
/// </summary>
/// <param name="Base">The song's value, which the facets are drawn around.</param>
/// <param name="Facets">Every facet's value.</param>
public sealed record Unconventionality(double Base, ImmutableDictionary<Facet, double> Facets)
{
    /// <summary>
    ///     Where a generated song's base gathers: plainer than the middle, where every choice is tuned, as most songs keep
    ///     to most conventions; a wilder song is one asked for.
    /// </summary>
    public const double BaseCentre = 0.25;

    /// <summary>
    ///     How far a generated song's base strays from its centre (<see cref="BaseCentre" />), as the scale of a logistic
    ///     draw in log-odds: half the songs between about 0.17 and 0.35, and every value possible, ever rarer towards the
    ///     ends, above 0.75 in about one song in eighty and above 0.9 in about one in seven hundred.
    /// </summary>
    public const double BaseSpread = 0.5;

    /// <summary>
    ///     How far a facet strays from the base, as the scale of a logistic draw in log-odds about it: freely about the
    ///     middle, less and less towards an end the nearer the base is to it, and not at all at an end, every value
    ///     between possible.
    /// </summary>
    public const double FacetSpread = 0.35;

    /// <summary>How far a section moves a facet, as the scale of a logistic draw in log-odds about it: a little.</summary>
    public const double SectionShift = 0.1;

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
    ///     A generated song's base, drawn about its centre (<see cref="BaseCentre" />, <see cref="BaseSpread" />), in place
    ///     of a supplied one, which is used as given.
    /// </summary>
    public static double DrawBase(IGenerationContext context)
    {
        return Around(BaseCentre, BaseSpread, context);
    }

    /// <summary>
    ///     A value drawn about the one given, its centre, by one law for the base, the facets and the sections: a logistic
    ///     draw in log-odds of the spread's scale, so that the values gather about the centre, every one between the ends
    ///     possible and ever rarer towards them, the less spread the nearer the centre is to an end, and none at one.
    /// </summary>
    private static double Around(double centre, double spread, IGenerationContext context)
    {
        var draw = context.GenerateDouble();
        return centre is 0 or 1 ? centre : Logistic(Math.Log(centre / (1 - centre)) + spread * Math.Log(draw / (1 - draw)));
    }

    private static double Logistic(double logOdds) => 1 / (1 + Math.Exp(-logOdds));
}

/// <summary>What is given in place of the song's own draws, each used as given; none for the song as its seed makes it.</summary>
/// <param name="Meter">The meter the song's bars are in, its groups in the order given.</param>
/// <param name="MeterOption">The meter given by its place in <see cref="Composition.Meter.Options" />, its groups' order drawn.</param>
/// <param name="Tempo">The tempo given, as a multiple of <see cref="Composition.Meter.BaseTempo" />, which need not be one the song may draw.</param>
/// <param name="Key">The key given, as semitones above C, from 0 to 11.</param>
/// <param name="Base">The song's unconventionality's base, from 0 to 1, which its facets are drawn around.</param>
/// <param name="Facets">Facets set outright, from 0 to 1, over those drawn.</param>
/// <param name="Parts">The parts given in the song (true) or out of it (false) (<see cref="SongParts" />).</param>
/// <param name="DrumSetup">The drums the song plays: the kit, the kit and percussion, or percussion.</param>
/// <param name="DrumPieces">
///     The drums the song plays, by their names (<see cref="Composition.DrumPieces" />), in place of those it draws, which
///     make its setup.
/// </param>
public sealed record SongOverrides(
    Meter? Meter = null,
    double? Base = null,
    ImmutableDictionary<Facet, double>? Facets = null,
    ImmutableDictionary<TrackRole, bool>? Parts = null,
    DrumSetup? DrumSetup = null,
    int? MeterOption = null,
    double? Tempo = null,
    int? Key = null,
    ImmutableHashSet<string>? DrumPieces = null
)
{
    public static SongOverrides None { get; } = new();
}
