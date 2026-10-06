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

    /// <summary>Its repeats palm-muted where its part chugs, a guitar's or a picked bass's.</summary>
    Chugs = 512
}

/// <summary>
///     Every General MIDI instrument's traits, one table, so that an instrument added to a role plays as it is played: by
///     its family, eight programs each, and an instrument's own over its family's where it differs.
/// </summary>
public static class InstrumentTraits
{
    private const InstrumentTrait Guitar = InstrumentTrait.Bends | InstrumentTrait.VibratesWide | InstrumentTrait.SoloBends | InstrumentTrait.Choruses
                                           | InstrumentTrait.Tremolo | InstrumentTrait.Strums | InstrumentTrait.Chugs;

    private const InstrumentTrait Lead = InstrumentTrait.Bends | InstrumentTrait.VibratesWide | InstrumentTrait.SoloBends | InstrumentTrait.Choruses | InstrumentTrait.Sweeps;

    /// <summary>A family's traits, by its first program.</summary>
    private static readonly (int First, InstrumentTrait Traits)[] Families =
    [
        (0, InstrumentTrait.Choruses | InstrumentTrait.Rolls),
        (8, InstrumentTrait.None),
        (16, InstrumentTrait.None),
        (24, Guitar),
        (32, InstrumentTrait.None),
        (40, InstrumentTrait.Bends),
        (48, InstrumentTrait.Bends),
        (56, InstrumentTrait.Bends),
        (64, InstrumentTrait.Bends),
        (72, InstrumentTrait.Bends),
        (80, Lead),
        (88, InstrumentTrait.Choruses | InstrumentTrait.ChorusesClassically | InstrumentTrait.Sweeps),
        (96, InstrumentTrait.None),
        (104, InstrumentTrait.None),
        (112, InstrumentTrait.None),
        (120, InstrumentTrait.None)
    ];

    /// <summary>An instrument's traits where they are not its family's.</summary>
    private static readonly Dictionary<int, InstrumentTrait> Own = new()
    {
        // the electric pianos, chorused and trembling
        [4] = InstrumentTrait.Choruses | InstrumentTrait.ChorusesClassically | InstrumentTrait.Rolls | InstrumentTrait.Tremolo,
        [5] = InstrumentTrait.Choruses | InstrumentTrait.ChorusesClassically | InstrumentTrait.Rolls | InstrumentTrait.Tremolo,
        // the organs but the accordions and the harmonica, trembling and swept
        [16] = InstrumentTrait.Tremolo | InstrumentTrait.Sweeps,
        [17] = InstrumentTrait.Tremolo | InstrumentTrait.Sweeps,
        [18] = InstrumentTrait.Tremolo | InstrumentTrait.Sweeps,
        [19] = InstrumentTrait.Tremolo | InstrumentTrait.Sweeps,
        [20] = InstrumentTrait.Tremolo | InstrumentTrait.Sweeps,
        // the clean guitar's chorus its classic sound
        [27] = Guitar | InstrumentTrait.ChorusesClassically,
        // the picked bass chugging; the fretless and the synth basses bending
        [34] = InstrumentTrait.Chugs,
        [35] = InstrumentTrait.Bends,
        [38] = InstrumentTrait.Bends,
        [39] = InstrumentTrait.Bends,
        // the strings bowed bend, but the harp and the timpani
        [45] = InstrumentTrait.None,
        [46] = InstrumentTrait.None,
        [47] = InstrumentTrait.None,
        // the string ensembles chorused
        [48] = InstrumentTrait.Bends | InstrumentTrait.Choruses,
        [49] = InstrumentTrait.Bends | InstrumentTrait.Choruses,
        [50] = InstrumentTrait.Bends | InstrumentTrait.Choruses,
        [51] = InstrumentTrait.Bends | InstrumentTrait.Choruses
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
}
