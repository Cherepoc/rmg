using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     The drums. A weight is how likely the drum is to be chosen among the other drums it competes with, both
///     when choosing the drums of a song and the drums of a section; a loudness leans a section's choice by its energy:
///     the crash and the ride loud, the tambourine a little, the shakers a little quiet and the cross-stick quiet.
/// </summary>
public static class DrumDefinitions
{
    /// <summary>The kick's part in a groove: it repeats its figure, the base of the groove.</summary>
    public static StateMapBuilder GroundsTheGroove(this StateMapBuilder builder)
    {
        return builder.Add(CompositionStateKinds.Rhythm.Variation, -0.4);
    }

    /// <summary>
    ///     The hi-hat's part in a groove: it plays faster than the other drums, and like the ride keeps time, full and
    ///     steady.
    /// </summary>
    public static StateMapBuilder KeepsTime(this StateMapBuilder builder)
    {
        return builder
            .Add(CompositionStateKinds.Rhythm.Period.Power, -1)
            .Add(CompositionStateKinds.Rhythm.Fullness, 0.35)
            .Add(CompositionStateKinds.Rhythm.Variation, -0.5);
    }

    /// <summary>
    ///     The snare's part in a groove, the backbeat: a cycle of half a bar, shifted by half of it, puts the main hits
    ///     on beats 2 and 4, and with no weaker hits of its own it plays nothing else unless the rhythm layers add ghost
    ///     notes; and it keeps its figure, bar after bar.
    /// </summary>
    public static StateMapBuilder PlaysTheBackbeat(this StateMapBuilder builder)
    {
        return builder
            .Add(CompositionStateKinds.Rhythm.Period.Power, -1)
            .Add(CompositionStateKinds.Rhythm.Phase.Rank, 1)
            .Add(CompositionStateKinds.Rhythm.MaxRank, -2)
            .Add(CompositionStateKinds.Rhythm.Variation, -0.3);
    }

    public static PercussionInstrumentDefinition Kick { get; } = new("Kick", [35, 36], 1.0, builder => builder.GroundsTheGroove());

    public static PercussionInstrumentDefinition CrossStick { get; } = new("Snare Cross Stick", [37], 0.2, loudness: -1);

    public static PercussionInstrumentDefinition AcousticSnare { get; } = new("Acoustic Snare", [38], 1.0);

    public static PercussionInstrumentDefinition ElectricSnare { get; } = new("Electric Snare", [40], 0.7);

    public static PercussionInstrumentDefinition Clap { get; } = new("Clap", [39], 0.3);

    public static PercussionInstrumentDefinition HiHat { get; } = new(
        "Hi-Hat",
        // closed, pedal, open: the pedal quieter and the open louder, both played less than the closed in runs
        [new DrumSound(42), new DrumSound(44, 0.3, -0.5), new DrumSound(46, 0.5, 0.5)],
        1.0,
        builder => builder.KeepsTime()
    );

    public static PercussionInstrumentDefinition Ride { get; } = new(
        "Ride",
        // the ride, its bell, louder, and the second ride
        [new DrumSound(51), new DrumSound(53, 0.4, 0.5), new DrumSound(59, 0.6)],
        0.5,
        builder => builder
            .Add(CompositionStateKinds.Rhythm.Fullness, 0.35)
            .Add(CompositionStateKinds.Rhythm.Variation, -0.5),
        1
    );

    public static PercussionInstrumentDefinition Tambourine { get; } = new("Tambourine", [54], 0.2, loudness: 0.5);

    public static PercussionInstrumentDefinition Cabasa { get; } = new("Cabasa", [69], 0.15, loudness: -0.5);

    public static PercussionInstrumentDefinition Maracas { get; } = new("Maracas", [70], 0.15, loudness: -0.5);

    public static PercussionInstrumentDefinition Tom { get; } = new("Tom", [41, 43, 45, 47, 48, 50], 1.0);

    public static PercussionInstrumentDefinition Cymbal { get; } = new(
        "Cymbal",
        // the crashes, and the china and the splash, which mark a downbeat less, the splash quieter
        [new DrumSound(49), new DrumSound(52, 0.25, 0.3), new DrumSound(55, 0.25, -0.5), new DrumSound(57)],
        1.0,
        loudness: 1
    );

    public static PercussionInstrumentDefinition Vibraslap { get; } = new("Vibraslap", [58], 0.1);

    // the percussion stands in for the drum kit by its register, and plays its part in a groove: the low drums ground
    // it as the kick does, the dry high ones play the backbeat as the snare does, and the bells, the bongos and the
    // guiro keep time as the hi-hat does
    public static PercussionInstrumentDefinition Bongo { get; } = new("Bongo", [60, 61], 0.15, builder => builder.KeepsTime());

    public static PercussionInstrumentDefinition Conga { get; } = new("Conga", [62, 63, 64], 0.15, builder => builder.GroundsTheGroove());

    public static PercussionInstrumentDefinition Timbale { get; } = new("Timbale", [65, 66], 0.1, builder => builder.GroundsTheGroove());

    public static PercussionInstrumentDefinition Agogo { get; } = new("Agogo", [67, 68], 0.1, builder => builder.KeepsTime());

    public static PercussionInstrumentDefinition Cowbell { get; } = new("Cowbell", [56], 0.1, builder => builder.KeepsTime());

    public static PercussionInstrumentDefinition Claves { get; } = new("Claves", [75], 0.1, builder => builder.PlaysTheBackbeat());

    public static PercussionInstrumentDefinition WoodBlock { get; } = new("Wood Block", [76, 77], 0.1, builder => builder.PlaysTheBackbeat());

    public static PercussionInstrumentDefinition Guiro { get; } = new("Guiro", [73, 74], 0.1, builder => builder.KeepsTime());

    public static PercussionInstrumentDefinition Triangle { get; } = new("Triangle", [80, 81], 0.1);

    public static PercussionInstrumentDefinition Cuica { get; } = new("Cuica", [78, 79], 0.05);

    public static PercussionInstrumentDefinition Whistle { get; } = new("Whistle", [71, 72], 0.05);
}
