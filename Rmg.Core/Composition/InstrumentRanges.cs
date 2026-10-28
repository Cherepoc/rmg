namespace Rmg.Core.Composition;

/// <summary>
///     Every General MIDI instrument's range, the lowest and the highest note it sounds as its players play it, one table,
///     as the traits are (<see cref="InstrumentTraits" />): by its family, eight programs each, and an instrument's own over
///     its family's where it differs; the part's register kept within it (<see cref="Registers" />).
/// </summary>
public static class InstrumentRanges
{
    /// <summary>How many programs a General MIDI family holds.</summary>
    public const int FamilySize = 8;

    /// <summary>A family's range, by its first program.</summary>
    private static readonly (int First, int Low, int High)[] Families =
    [
        (0, 36, 96), // the pianos
        (8, 55, 96), // the chromatic percussion
        (16, 36, 96), // the organs
        (24, 40, 84), // the guitars
        (32, 28, 60), // the basses
        (40, 36, 96), // the strings
        (48, 36, 96), // the ensembles
        (56, 40, 84), // the brass
        (64, 48, 84), // the reeds
        (72, 60, 96), // the pipes
        (80, 36, 96), // the synth leads
        (88, 36, 96), // the synth pads
        (96, 36, 96), // the synth effects
        (104, 48, 88), // the world's
        (112, 48, 84), // the percussive
        (120, 36, 96) // the sound effects
    ];

    /// <summary>An instrument's range where it is not its family's.</summary>
    private static readonly Dictionary<int, (int Low, int High)> Own = new()
    {
        [9] = (72, 108), // glockenspiel
        [12] = (45, 96), // marimba
        [13] = (65, 108), // xylophone
        [14] = (60, 77), // tubular bells
        [15] = (48, 84), // dulcimer
        [21] = (53, 89), // accordion
        [22] = (60, 84), // harmonica
        [23] = (53, 89), // tango accordion
        [29] = (40, 88), // overdriven guitar, its lead to the top fret's E
        [30] = (40, 88), // distortion guitar
        [40] = (55, 100), // violin
        [41] = (48, 88), // viola
        [42] = (36, 76), // cello
        [43] = (28, 60), // contrabass
        [46] = (24, 103), // harp
        [47] = (40, 57), // timpani
        [56] = (55, 84), // trumpet
        [57] = (40, 72), // trombone
        [58] = (28, 58), // tuba
        [59] = (55, 82), // muted trumpet
        [60] = (41, 77), // French horn
        [62] = (36, 96), // synth brass
        [63] = (36, 96),
        [64] = (56, 88), // soprano sax
        [65] = (49, 81), // alto sax
        [66] = (44, 76), // tenor sax
        [67] = (36, 69), // baritone sax
        [68] = (58, 91), // oboe
        [69] = (52, 81), // English horn
        [70] = (34, 72), // bassoon
        [71] = (50, 91), // clarinet
        [72] = (74, 108), // piccolo
        [77] = (55, 84), // shakuhachi
        [78] = (72, 96), // whistle
        [79] = (60, 84), // ocarina
        [87] = (28, 72), // bass and lead
        [108] = (60, 84), // kalimba
        [109] = (60, 81), // bagpipe
        [110] = (55, 96), // fiddle
        [111] = (60, 88), // shanai
        [114] = (52, 81) // steel drums
    };

    /// <summary>An instrument's lowest and highest note, by its General MIDI program.</summary>
    public static (int Low, int High) Of(int program) => program is >= 0 and < 128
        ? Own.TryGetValue(program, out var own) ? own : Families.Last(x => x.First <= program) is var family ? (family.Low, family.High) : default
        : throw new ArgumentOutOfRangeException(nameof(program), program, "No General MIDI program.");
}
