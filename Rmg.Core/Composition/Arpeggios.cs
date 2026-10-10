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
    UpTwoOctaves,

    /// <summary>The lowest note in turn with each of the others upwards, 1–3–1–5–1–8, a metal guitar's broken chord.</summary>
    Pedal
}

/// <summary>A tone laid on a broken chord's notes for the part breaking it alone, which the band's chord leaves out.</summary>
public enum ChordColour
{
    None,
    Ninth,
    MajorSeventh,
    FlatSecond,
    SharpFourth
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
        (ArpeggioPattern.UpTwoOctaves, new ByConvention(0, 0.005, 1)),
        (ArpeggioPattern.Pedal, new ByConvention(0, 0.01, 1))
    ];

    /// <summary>The colours a part breaking its chord lays on it, a genre's, which the songs never lay.</summary>
    public static ImmutableArray<(ChordColour Colour, ByConvention Weight)> Colours { get; } =
    [
        (ChordColour.None, new ByConvention(1, 1, 1)),
        (ChordColour.Ninth, new ByConvention(0, 0, 0)),
        (ChordColour.MajorSeventh, new ByConvention(0, 0, 0)),
        (ChordColour.FlatSecond, new ByConvention(0, 0, 0)),
        (ChordColour.SharpFourth, new ByConvention(0, 0, 0))
    ];

    /// <summary>A colour as a genre's page shows it.</summary>
    public static string LabelOf(ChordColour colour) => colour switch
    {
        ChordColour.None => "None",
        ChordColour.Ninth => "A ninth",
        ChordColour.MajorSeventh => "A major seventh",
        ChordColour.FlatSecond => "A flat second",
        _ => "A sharp fourth"
    };

    /// <summary>A colour's semitones above the chord's root.</summary>
    public static int SemitonesOf(ChordColour colour) => colour switch
    {
        ChordColour.Ninth => 2,
        ChordColour.MajorSeventh => 11,
        ChordColour.FlatSecond => 1,
        ChordColour.SharpFourth => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(colour), colour, "No colour has no semitones.")
    };

    /// <summary>A section's colour of a part breaking its chord, by the chords facet, among a genre's pool where it names one.</summary>
    public static ChordColour DrawColour(IGenerationContext context, double unconventionality, Genre genre) =>
        context.Pick(ByConvention.Weigh(Colours, unconventionality, genre.Pool<ChordColour>(GenreChoices.Colour, x => GenreChoices.Name(x.ToString()))));

    /// <summary>A voiced chord with its colour laid on it, the colour's tone the nearest above its lowest note.</summary>
    public static ImmutableArray<int> Coloured(ImmutableArray<int> voiced, int root, ChordColour colour)
    {
        if (colour == ChordColour.None || voiced.IsEmpty)
            return voiced;
        var lowest = voiced.Min();
        var tone = lowest + ((root + SemitonesOf(colour) - lowest) % 12 + 12) % 12;
        return voiced.Contains(tone) ? voiced : [..voiced, tone];
    }

    /// <summary>An arpeggio as a genre's page shows it.</summary>
    public static string LabelOf(ArpeggioPattern pattern) => pattern switch
    {
        ArpeggioPattern.None => "The whole chord",
        ArpeggioPattern.UpDown => "Up and down",
        ArpeggioPattern.DownUp => "Down and up",
        ArpeggioPattern.RootFifth => "Root and fifth",
        ArpeggioPattern.UpTwoOctaves => "Up two octaves",
        ArpeggioPattern.Pedal => "Pedal point",
        _ => pattern.ToString()
    };

    /// <summary>A section's pattern, by the chords facet of its unconventionality, among a genre's pool where it names one.</summary>
    public static ArpeggioPattern Draw(IGenerationContext context, double unconventionality, Genre genre)
    {
        return context.Pick(ByConvention.Weigh(Patterns, unconventionality, genre.Pool<ArpeggioPattern>(GenreChoices.Arpeggio, x => GenreChoices.Name(x.ToString()))));
    }

    /// <summary>The note of a voiced chord, low to high, that its note of the given place since the chord came in plays.</summary>
    /// <param name="root">The chord's root, any octave of it, which the root and fifth and the pedal play on, as the voicing may put another note lowest.</param>
    /// <param name="order">The seed a chord broken in no pattern's order draws its notes by (<see cref="CompositionStateKinds.ArpeggioOrder" />).</param>
    public static int Pick(ArpeggioPattern pattern, ImmutableArray<int> voiced, int place, double position, int root, long order)
    {
        var notes = voiced.Order().ToArray();
        var count = notes.Length;
        if (count == 1 || pattern == ArpeggioPattern.None)
            return notes[0];

        // the root's lowest note in the voicing, the lowest note where it has none, and the fifth above it, the middle note
        // where it has none
        var low = Array.FindIndex(notes, x => (x - root).Mod(12) == 0) is var r and >= 0 ? r : 0;
        var fifth = Array.FindIndex(notes, x => x > notes[low] && (x - notes[low]).Mod(12) == 7) is var f and >= 0 ? f : count / 2;
        int[] others = [..Enumerable.Range(0, count).Where(x => x != low)];

        var cycle = Math.Max(1, 2 * count - 2);
        return pattern switch
        {
            ArpeggioPattern.Up => notes[place % count],
            ArpeggioPattern.Down => notes[count - 1 - place % count],
            ArpeggioPattern.UpDown => notes[place % cycle < count ? place % cycle : cycle - place % cycle],
            ArpeggioPattern.DownUp => notes[count - 1 - (place % cycle < count ? place % cycle : cycle - place % cycle)],
            ArpeggioPattern.Alberti => notes[(place % 4) switch { 0 => 0, 2 => count / 2, _ => count - 1 }],
            ArpeggioPattern.Pairs => notes[place % 2 == 0 ? 0 : count - 1],
            ArpeggioPattern.RootFifth => notes[place % 2 == 0 ? low : fifth],
            // an order of no pattern, the section's drawn, the same for the same place
            ArpeggioPattern.Random => notes[(int)(Probabilities.Seeds.Derive((ulong)order, Probabilities.Seeds.Key((long)Math.Round(position * 1000), place)) % (ulong)count)],
            ArpeggioPattern.UpTwoOctaves => notes[place % count] + 12 * (place / count % 2),
            ArpeggioPattern.Pedal => notes[place % 2 == 0 ? low : others[place / 2 % others.Length]],
            _ => throw new ArgumentOutOfRangeException(nameof(pattern), pattern, null)
        };
    }
}
