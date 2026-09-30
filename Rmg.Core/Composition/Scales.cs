using System.Collections.Immutable;
using System.Diagnostics;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>A scale, as the semitones of its notes above its first note, which the song's key moves.</summary>
/// <param name="Weight">How likely a song is to be in the scale, by the scale facet of its unconventionality.</param>
[DebuggerDisplay("Scale {Name}")]
public sealed record Scale(string Name, ImmutableArray<int> Offsets, ByConvention Weight)
{
    /// <summary>How bright the scale sounds: the sum of its offsets, lydian's the highest and phrygian's the lowest.</summary>
    public int Brightness => Offsets.Sum();

    /// <summary>How many of its notes differ from another scale of as many notes, on the same first note.</summary>
    public int Distance(Scale other)
    {
        return Offsets.Zip(other.Offsets).Count(x => x.First != x.Second);
    }
}

/// <summary>
///     The scales a song can be in. They all have seven notes, since the chord root moves by scale steps and chord
///     heights are placed between the qualities a 7-note scale gives; scales of other sizes need more than that.
/// </summary>
internal static class Scales
{
    /// <summary>How many notes every scale has.</summary>
    public const int StepCount = 7;

    public static Scale NaturalMinor { get; } = new("Natural minor", [0, 2, 3, 5, 7, 8, 10], new ByConvention(1, 0.3, 0));

    public static Scale Major { get; } = new("Major", [0, 2, 4, 5, 7, 9, 11], new ByConvention(1, 0.3, 0));

    public static Scale Dorian { get; } = new("Dorian", [0, 2, 3, 5, 7, 9, 10], new ByConvention(0, 0.12, 0.3));

    public static Scale Mixolydian { get; } = new("Mixolydian", [0, 2, 4, 5, 7, 9, 10], new ByConvention(0, 0.12, 0.3));

    public static Scale HarmonicMinor { get; } = new("Harmonic minor", [0, 2, 3, 5, 7, 8, 11], new ByConvention(0, 0.06, 1));

    public static Scale Phrygian { get; } = new("Phrygian", [0, 1, 3, 5, 7, 8, 10], new ByConvention(0, 0.05, 1));

    public static Scale Lydian { get; } = new("Lydian", [0, 2, 4, 6, 7, 9, 11], new ByConvention(0, 0.05, 1));

    public static Scale MelodicMinor { get; } = new("Melodic minor", [0, 2, 3, 5, 7, 9, 11], new ByConvention(0, 0.015, 1));

    public static Scale HarmonicMajor { get; } = new("Harmonic major", [0, 2, 4, 5, 7, 8, 11], new ByConvention(0, 0.015, 1));

    public static Scale Locrian { get; } = new("Locrian", [0, 1, 3, 5, 6, 8, 10], new ByConvention(0, 0.015, 1));

    public static Scale PhrygianDominant { get; } = new("Phrygian dominant", [0, 1, 4, 5, 7, 8, 10], new ByConvention(0, 0.015, 1));

    public static Scale HungarianMinor { get; } = new("Hungarian minor", [0, 2, 3, 6, 7, 8, 11], new ByConvention(0, 0.015, 1));

    public static Scale DoubleHarmonicMajor { get; } = new("Double harmonic major", [0, 1, 4, 5, 7, 8, 11], new ByConvention(0, 0.015, 1));

    /// <summary>
    ///     Most songs are in minor or major, some in Dorian or Mixolydian, close to them and common in rock, folk and
    ///     blues, and a few in a stranger one; the plainest in minor or major, the wildest in any of the stranger ones, harmonic
    ///     minor, Phrygian, Lydian, melodic minor, harmonic major, Locrian, Phrygian dominant, Hungarian minor or double
    ///     harmonic major, and now and then Dorian or Mixolydian.
    /// </summary>
    public static ImmutableArray<Scale> All { get; } =
    [
        NaturalMinor, Major, Dorian, Mixolydian, HarmonicMinor, Phrygian, Lydian,
        MelodicMinor, HarmonicMajor, Locrian, PhrygianDominant, HungarianMinor, DoubleHarmonicMajor
    ];

    /// <summary>
    ///     The chance a section is in another scale than the song's, by the scale facet of its unconventionality: never at
    ///     the plain end, always at the wild, and as often as the corpus's sections were between.
    /// </summary>
    public static ByConvention SectionChange { get; } = new(0, 0.16, 1);

    /// <summary>How much less likely a scale is for every note it changes beyond the first.</summary>
    public const double Closeness = 0.3;

    /// <summary>A song's scale, by the scale facet of its unconventionality.</summary>
    public static Scale Pick(IGenerationContext context, double unconventionality)
    {
        return context.Pick(ByConvention.Weigh(All.Select(x => (x, x.Weight)), unconventionality));
    }

    /// <summary>
    ///     A section's scale, on the song's tonic, by the scale facet of the section's unconventionality: the song's, or
    ///     another by <see cref="SectionChange" />, the closer to the song's and the heavier at the facet the likelier,
    ///     and leaning brighter or darker by the tilt, such as the section's energy; one the facet allows, and none it
    ///     does not; a section that contrasts, such as a chorus, changing the likelier by its contrast.
    /// </summary>
    public static Scale PickSection(IGenerationContext context, Scale song, double unconventionality, Tilt tilt, Tilt contrast)
    {
        if (!context.TestProbability(contrast.Chance(SectionChange.At(unconventionality), 1)))
            return song;

        var options = tilt.Weigh(
            ByConvention.Weigh(All.Where(x => x != song).Select(x => (x, x.Weight)), unconventionality)
                .Select(x => x with { Weight = x.Weight * Math.Pow(Closeness, x.Value.Distance(song) - 1) }),
            x => Math.Sign(x.Brightness - song.Brightness)
        );
        return context.Pick(options);
    }
}
