using System.Collections.Immutable;

namespace Rmg.Core.Composition;

/// <summary>What a General MIDI instrument does as a player plays it, each a bit (<see cref="InstrumentTraits" />).</summary>
[Flags]
public enum InstrumentTrait
{
    None = 0,

    /// <summary>Its notes bent and slid, as a voice, a string, a wind or a synth lead's are; not a struck or plucked key's.</summary>
    Bends = 1,

    /// <summary>A wide vibrato, a guitar's or a synth lead's, rather than a singer's.</summary>
    VibratesWide = 2,

    /// <summary>A soloist's bends its own, a guitarist's or a synth lead's.</summary>
    SoloBends = 4,

    /// <summary>A chorus suits it.</summary>
    Choruses = 8,

    /// <summary>A chorus is its classic sound, an electric piano's, a clean guitar's, a pad's, the likelier.</summary>
    ChorusesClassically = 16,

    /// <summary>A tremolo suits it, an electric piano's, an organ's, a guitar's.</summary>
    Tremolo = 32,

    /// <summary>A filter's sweep suits it, an organ's or a synth's.</summary>
    Sweeps = 64,

    /// <summary>Its chords strummed, down and up, a guitar's.</summary>
    Strums = 128,

    /// <summary>Its held chords rolled, a piano's.</summary>
    Rolls = 256,

    /// <summary>Its repeats palm-muted where its part chugs, a driven guitar's or a picked bass's.</summary>
    Chugs = 512,

    /// <summary>A driven guitar's distortion, which a band's bass plays under and a mix doubles from side to side.</summary>
    Driven = 1024,

    /// <summary>A filter's opening and closing suits it, a synth's: a lead's, a pad's, a synth bass's, strings' or brass's.</summary>
    Filters = 2048,

    /// <summary>A wah pedal suits it, a guitar's or a clavinet's.</summary>
    Wahs = 4096,

    /// <summary>Its note dies away as it rings, a struck or plucked string's or a bar's, so that a chord held long is struck again.</summary>
    Decays = 8192,

    /// <summary>A phaser suits its held sound: strings', a pad's, an electric piano's or a guitar's.</summary>
    Phases = 16384
}

/// <summary>
///     Every General MIDI instrument's traits, one table, so that an instrument added to a role plays as it is played: by
///     its family, eight programs each, and an instrument's own over its family's where it differs.
/// </summary>
public static class InstrumentTraits
{
    private const InstrumentTrait Guitar = InstrumentTrait.Bends | InstrumentTrait.VibratesWide | InstrumentTrait.SoloBends | InstrumentTrait.Choruses
                                           | InstrumentTrait.Tremolo | InstrumentTrait.Strums | InstrumentTrait.Wahs | InstrumentTrait.Decays | InstrumentTrait.Phases;

    private const InstrumentTrait Lead = InstrumentTrait.Bends | InstrumentTrait.VibratesWide | InstrumentTrait.SoloBends | InstrumentTrait.Choruses | InstrumentTrait.Sweeps
                                         | InstrumentTrait.Filters;

    /// <summary>A family's traits, by its first program.</summary>
    private static readonly (int First, InstrumentTrait Traits)[] Families =
    [
        (0, InstrumentTrait.Choruses | InstrumentTrait.Rolls | InstrumentTrait.Decays),
        (8, InstrumentTrait.Decays),
        (16, InstrumentTrait.None),
        (24, Guitar),
        (32, InstrumentTrait.Decays),
        (40, InstrumentTrait.Bends),
        (48, InstrumentTrait.Bends),
        (56, InstrumentTrait.Bends),
        (64, InstrumentTrait.Bends),
        (72, InstrumentTrait.Bends),
        (80, Lead),
        (88, InstrumentTrait.Choruses | InstrumentTrait.ChorusesClassically | InstrumentTrait.Sweeps | InstrumentTrait.Filters | InstrumentTrait.Phases),
        (96, InstrumentTrait.Choruses | InstrumentTrait.ChorusesClassically | InstrumentTrait.Sweeps),
        (104, InstrumentTrait.Decays),
        (112, InstrumentTrait.Decays),
        (120, InstrumentTrait.None)
    ];

    /// <summary>An instrument's traits where they are not its family's.</summary>
    private static readonly Dictionary<int, InstrumentTrait> Own = new()
    {
        // the clavinet, a funk player's wah
        [7] = InstrumentTrait.Choruses | InstrumentTrait.Rolls | InstrumentTrait.Wahs | InstrumentTrait.Decays,
        // the electric pianos, chorused and trembling
        [4] = InstrumentTrait.Choruses | InstrumentTrait.ChorusesClassically | InstrumentTrait.Rolls | InstrumentTrait.Tremolo | InstrumentTrait.Decays | InstrumentTrait.Phases,
        [5] = InstrumentTrait.Choruses | InstrumentTrait.ChorusesClassically | InstrumentTrait.Rolls | InstrumentTrait.Tremolo | InstrumentTrait.Decays | InstrumentTrait.Phases,
        // the organs but the accordions and the harmonica, trembling and swept
        [16] = InstrumentTrait.Tremolo | InstrumentTrait.Sweeps,
        [17] = InstrumentTrait.Tremolo | InstrumentTrait.Sweeps,
        [18] = InstrumentTrait.Tremolo | InstrumentTrait.Sweeps,
        [19] = InstrumentTrait.Tremolo | InstrumentTrait.Sweeps,
        [20] = InstrumentTrait.Tremolo | InstrumentTrait.Sweeps,
        // the harmonica, bent as its notes are blown
        [22] = InstrumentTrait.Bends,
        // the clean guitar's chorus its classic sound
        [27] = Guitar | InstrumentTrait.ChorusesClassically,
        // the driven guitars chugging; the muted guitar is a clean one muted, which never does
        [29] = Guitar | InstrumentTrait.Chugs | InstrumentTrait.Driven,
        [30] = Guitar | InstrumentTrait.Chugs | InstrumentTrait.Driven,
        // the picked bass chugging; the fretless and the synth basses bending
        [34] = InstrumentTrait.Chugs | InstrumentTrait.Decays,
        [35] = InstrumentTrait.Bends | InstrumentTrait.Decays,
        [38] = InstrumentTrait.Bends | InstrumentTrait.Filters | InstrumentTrait.Decays,
        [39] = InstrumentTrait.Bends | InstrumentTrait.Filters | InstrumentTrait.Decays,
        // the strings bowed bend, but the plucked strings and the timpani, which die away
        [45] = InstrumentTrait.Decays,
        [46] = InstrumentTrait.Decays,
        [47] = InstrumentTrait.Decays,
        // the voices bend, but the orchestra hit, a sample struck
        [55] = InstrumentTrait.None,
        // the synth brass chorused, swept and filtered
        [62] = InstrumentTrait.Bends | InstrumentTrait.Choruses | InstrumentTrait.Sweeps | InstrumentTrait.Filters,
        [63] = InstrumentTrait.Bends | InstrumentTrait.Choruses | InstrumentTrait.Sweeps | InstrumentTrait.Filters,
        // the sitar, the koto, the shamisen, the fiddle, the bagpipe and the shanai bend, their players' way
        [104] = InstrumentTrait.Bends | InstrumentTrait.Decays,
        [106] = InstrumentTrait.Bends | InstrumentTrait.Decays,
        [107] = InstrumentTrait.Bends | InstrumentTrait.Decays,
        [109] = InstrumentTrait.Bends,
        [110] = InstrumentTrait.Bends,
        [111] = InstrumentTrait.Bends,
        // the string ensembles chorused and phased
        [48] = InstrumentTrait.Bends | InstrumentTrait.Choruses | InstrumentTrait.Phases,
        [49] = InstrumentTrait.Bends | InstrumentTrait.Choruses | InstrumentTrait.Phases,
        [50] = InstrumentTrait.Bends | InstrumentTrait.Choruses | InstrumentTrait.Filters | InstrumentTrait.Phases,
        [51] = InstrumentTrait.Bends | InstrumentTrait.Choruses | InstrumentTrait.Filters | InstrumentTrait.Phases
    };

    private static readonly ImmutableArray<InstrumentTrait> Table =
        [..Enumerable.Range(0, 128).Select(program => Own.TryGetValue(program, out var own) ? own : Families.Last(x => x.First <= program).Traits)];

    /// <summary>An instrument's traits, by its General MIDI program; none for the drums, which callers give as -1.</summary>
    public static InstrumentTrait Of(int program) => program switch
    {
        -1 => InstrumentTrait.None,
        >= 0 and < 128 => Table[program],
        _ => throw new ArgumentOutOfRangeException(nameof(program), program, "No General MIDI program.")
    };

    /// <summary>Whether an instrument has a trait.</summary>
    public static bool Has(int program, InstrumentTrait trait) => (Of(program) & trait) == trait;

    /// <summary>Whether an instrument is a clean one that dies away, which a held note or a broken chord lets ring.</summary>
    public static bool Rings(int program) => Has(program, InstrumentTrait.Decays) && !Has(program, InstrumentTrait.Driven);
}
