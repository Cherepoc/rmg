using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>How a section's chords are broken, one note of the chord at a time, as a picked guitar or a piano's left hand plays them.</summary>
public enum ArpeggioPattern
{
    /// <summary>The whole chord at once.</summary>
    None,

    /// <summary>From the lowest note up, and again.</summary>
    Up,

    /// <summary>From the highest note down, and again.</summary>
    Down,

    /// <summary>Up to the highest and back down.</summary>
    UpDown,

    /// <summary>Down to the lowest and back up.</summary>
    DownUp,

    /// <summary>The lowest, the highest, the middle, the highest, as a classical left hand plays.</summary>
    Alberti,

    /// <summary>The lowest and the highest in turn.</summary>
    Pairs,

    /// <summary>The root and the fifth in turn, as a bass and a hand of a stride pianist.</summary>
    RootFifth,

    /// <summary>The chord's notes in an order of no pattern.</summary>
    Random,

    /// <summary>From the lowest note up over two octaves, the chord again an octave higher.</summary>
    UpTwoOctaves
}

/// <summary>
///     Arpeggios: a section's chords broken now and then, each note of the chords part one note of its chord, chosen by
///     the pattern from its place since the chord came in, in the part's own rhythm. By the chords facet: at the plain
///     end the whole chord most often and the common patterns, up, up and down and Alberti, now and then; at the wild end
///     every pattern and the whole chord alike.
/// </summary>
internal static class Arpeggios
{
    public static ImmutableArray<(ArpeggioPattern Pattern, ByConvention Weight)> Patterns { get; } =
    [
        (ArpeggioPattern.None, new ByConvention(0.7, 0.8, 1)),
        (ArpeggioPattern.Up, new ByConvention(0.1, 0.05, 1)),
        (ArpeggioPattern.UpDown, new ByConvention(0.1, 0.04, 1)),
        (ArpeggioPattern.Alberti, new ByConvention(0.1, 0.03, 1)),
        (ArpeggioPattern.Down, new ByConvention(0, 0.02, 1)),
        (ArpeggioPattern.DownUp, new ByConvention(0, 0.015, 1)),
        (ArpeggioPattern.Pairs, new ByConvention(0, 0.02, 1)),
        (ArpeggioPattern.RootFifth, new ByConvention(0, 0.015, 1)),
        (ArpeggioPattern.Random, new ByConvention(0, 0.005, 1)),
        (ArpeggioPattern.UpTwoOctaves, new ByConvention(0, 0.005, 1))
    ];

    /// <summary>A section's pattern, by the chords facet of its unconventionality.</summary>
    public static ArpeggioPattern Draw(IGenerationContext context, double unconventionality)
    {
        return context.Pick(ByConvention.Weigh(Patterns, unconventionality));
    }

    /// <summary>The note of a voiced chord, low to high, that its note of the given place since the chord came in plays.</summary>
    public static int Pick(ArpeggioPattern pattern, ImmutableArray<int> voiced, int place, double position)
    {
        var notes = voiced.Order().ToArray();
        var count = notes.Length;
        if (count == 1 || pattern == ArpeggioPattern.None)
            return notes[0];

        var cycle = Math.Max(1, 2 * count - 2);
        return pattern switch
        {
            ArpeggioPattern.Up => notes[place % count],
            ArpeggioPattern.Down => notes[count - 1 - place % count],
            ArpeggioPattern.UpDown => notes[place % cycle < count ? place % cycle : cycle - place % cycle],
            ArpeggioPattern.DownUp => notes[count - 1 - (place % cycle < count ? place % cycle : cycle - place % cycle)],
            ArpeggioPattern.Alberti => notes[(place % 4) switch { 0 => 0, 2 => count / 2, _ => count - 1 }],
            ArpeggioPattern.Pairs => notes[place % 2 == 0 ? 0 : count - 1],
            ArpeggioPattern.RootFifth => notes[place % 2 == 0 ? 0 : count / 2],
            // an order of no pattern, the same for the same place, as a hash of it
            ArpeggioPattern.Random => notes[(int)(Probabilities.Seeds.Mix((ulong)(position * 1000) + (ulong)place) % (ulong)count)],
            ArpeggioPattern.UpTwoOctaves => notes[place % count] + 12 * (place / count % 2),
            _ => throw new ArgumentOutOfRangeException(nameof(pattern), pattern, null)
        };
    }
}
