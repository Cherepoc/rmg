using System.Collections.Immutable;
using System.Diagnostics;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>A scale, as the semitones of its notes above its first note, which the song's key moves.</summary>
/// <param name="Weight">How likely a song is to be in the scale.</param>
[DebuggerDisplay("Scale {Name}")]
public sealed record Scale(string Name, ImmutableArray<int> Offsets, double Weight);

/// <summary>
///     The scales a song can be in. They all have seven notes, since the chord root moves by scale steps and chord
///     heights are placed between the qualities a 7-note scale gives; scales of other sizes need more than that.
/// </summary>
public static class Scales
{
    public static Scale NaturalMinor { get; } = new("Natural minor", [0, 2, 3, 5, 7, 8, 10], 0.3);

    public static Scale Major { get; } = new("Major", [0, 2, 4, 5, 7, 9, 11], 0.3);

    public static Scale Dorian { get; } = new("Dorian", [0, 2, 3, 5, 7, 9, 10], 0.12);

    public static Scale Mixolydian { get; } = new("Mixolydian", [0, 2, 4, 5, 7, 9, 10], 0.12);

    public static Scale HarmonicMinor { get; } = new("Harmonic minor", [0, 2, 3, 5, 7, 8, 11], 0.06);

    public static Scale Phrygian { get; } = new("Phrygian", [0, 1, 3, 5, 7, 8, 10], 0.05);

    public static Scale Lydian { get; } = new("Lydian", [0, 2, 4, 6, 7, 9, 11], 0.05);

    /// <summary>Most songs are in minor or major, some in a mode close to them, and a few in a stranger one.</summary>
    public static ImmutableArray<Scale> All { get; } =
        [NaturalMinor, Major, Dorian, Mixolydian, HarmonicMinor, Phrygian, Lydian];

    private static readonly Func<IGenerationContext, int> IndexGenerator =
        Generators.WeightedIndex([..All.Select(x => new Weighted<Scale>(x.Weight, x))]);

    /// <summary>A scale, the heavier ones being more likely.</summary>
    public static Scale Pick(IGenerationContext context)
    {
        return All[IndexGenerator(context)];
    }
}
