using System.Collections.Immutable;

namespace Rmg.Core.Songs;

/// <summary>
///     A chord's name by the notes it plays above its root, such as "m7" or "sus4", and its Roman numeral in a key: the
///     chords the songs play, triads, sus and power chords, sixths, sevenths and ninths, and no further, which a name for
///     an agent or a reader does not need.
/// </summary>
public static class ChordNames
{
    private static readonly ImmutableArray<int> MajorOffsets = [0, 2, 4, 5, 7, 9, 11];

    private static readonly string[] Numerals = ["I", "II", "III", "IV", "V", "VI", "VII"];

    /// <summary>What a chord is, by the pitch classes it plays above its root, from 0 to 11.</summary>
    /// <param name="Seventh">Its seventh above the root, 10 or 11, a diminished seventh's 9, or 0 for none.</param>
    public sealed record Quality(bool Major, bool Minor, bool Diminished, bool Augmented, int Seventh, string Suffix);

    /// <summary>A chord's quality and its name's suffix, by the intervals above its root that it plays.</summary>
    public static Quality Of(IReadOnlySet<int> intervals)
    {
        bool Has(int interval) => intervals.Contains(interval);
        var (major, minor) = (Has(4), !Has(4) && Has(3));
        var diminished = minor && Has(6) && !Has(7);
        var augmented = major && Has(8) && !Has(7);
        var seventh = Has(10) ? 10 : Has(11) ? 11 : diminished && Has(9) ? 9 : 0;
        var sixth = Has(9) && seventh == 0;
        var ninth = Has(2) && (major || minor);

        var suffix = (major, minor, diminished, seventh) switch
        {
            (false, false, _, _) => (seventh switch { 11 => "maj7", 10 => "7", _ => "" }) + (Has(5) ? "sus4" : Has(2) ? "sus2" : seventh == 0 ? "5" : "(no3)"),
            (_, _, true, 10) => "m7♭5",
            (_, _, true, 9) => "dim7",
            (_, _, true, _) => "dim",
            (_, true, _, 11) => ninth ? "m(maj9)" : "m(maj7)",
            (_, _, _, 11) when augmented => ninth ? "maj9♯5" : "maj7♯5",
            (_, _, _, 11) => ninth ? "maj9" : "maj7",
            (_, _, _, 10) => (minor ? "m" : augmented ? "aug" : "") + (ninth ? "9" : "7"),
            _ when sixth => (minor ? "m6" : "6") + (ninth ? "/9" : ""),
            _ => (minor ? "m" : augmented ? "aug" : "") + (ninth ? "add9" : "")
        };
        return new Quality(major, minor, diminished, augmented, seventh, suffix);
    }

    /// <summary>
    ///     A chord's Roman numeral on a degree of a scale: the degree's numeral, flat or sharp where the scale's note lies
    ///     off the major scale's, lower case where it is minor or diminished, ° for diminished and + for augmented, and its
    ///     seventh, 7 or maj7, ø7 for a half-diminished one and °7 for a diminished one; one of no third, sus or power,
    ///     in its scale's case, with its name's suffix, such as "V7sus4" or "i5".
    /// </summary>
    /// <param name="degree">The chord's root, as a step of the scale from its tonic, 0 to 6.</param>
    /// <param name="scaleOffsets">The scale's notes above its tonic.</param>
    /// <param name="quality">The chord's quality, as it plays (<see cref="Of" />).</param>
    public static string Numeral(int degree, IReadOnlyList<int> scaleOffsets, Quality quality)
    {
        var accidental = (scaleOffsets[degree] - MajorOffsets[degree]) switch { < 0 => "♭", > 0 => "♯", _ => "" };
        // a chord of no third, sus or power, in its scale's case, its suffix after its seventh
        if (!quality.Major && !quality.Minor)
        {
            var triad = Of(Triad(degree, scaleOffsets));
            return accidental + (triad.Minor ? Numerals[degree].ToLowerInvariant() : Numerals[degree]) + quality.Suffix;
        }

        var numeral = quality.Minor ? Numerals[degree].ToLowerInvariant() : Numerals[degree];
        var mark = (quality.Diminished, quality.Seventh) switch
        {
            (true, 10) => "ø7",
            (true, 9) => "°7",
            (true, _) => "°",
            _ => (quality.Augmented ? "+" : "") + quality.Seventh switch { 11 => "maj7", 10 => "7", _ => "" }
        };
        return accidental + numeral + mark;
    }

    /// <summary>The intervals a scale's triad plays on a degree: its third and fifth above the root, from the scale.</summary>
    public static IReadOnlySet<int> Triad(int degree, IReadOnlyList<int> scaleOffsets)
    {
        int Above(int steps) => (scaleOffsets[(degree + steps) % scaleOffsets.Count] - scaleOffsets[degree] + 12) % 12;
        return new HashSet<int> { 0, Above(2), Above(4) };
    }
}
