using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     The drums. A weight is how likely the drum is to be chosen among the other drums it competes with, both
///     when choosing the drums of a song and the drums of a section; a loudness leans a section's choice by its energy:
///     the crash and the ride loud, the tambourine a little, the shakers a little quiet. A drum's roles in the groove
///     (<see cref="DrumRoles" />) set its fixed rhythm.
/// </summary>
public static class DrumDefinitions
{
    // a drum's affinities for the roles, its main one first at 1 and the others as rare as a drummer plays them so, which
    // a wild section reaches for more
    private static ImmutableArray<Weighted<DrumRole>> Plays(DrumRole main, params (DrumRole Role, double Affinity)[] others) =>
        [new(1, main), ..others.Select(x => new Weighted<DrumRole>(x.Affinity, x.Role))];

    public static PercussionInstrumentDefinition Kick { get; } = new("Kick", [35, 36], 1.0, roles: Plays(DrumRole.Ground, (DrumRole.Time, 0.01)));

    // the cross-stick is a stroke of the snare: quieter, and seldom in a run
    private static DrumSound CrossStick { get; } = new(37, 0.1, -1, 0.25);

    public static PercussionInstrumentDefinition AcousticSnare { get; } = new("Acoustic Snare", [new DrumSound(38), CrossStick], 1.0, roles: Plays(DrumRole.Backbeat, (DrumRole.Time, 0.01)));

    public static PercussionInstrumentDefinition ElectricSnare { get; } = new("Electric Snare", [new DrumSound(40), CrossStick], 0.7, roles: Plays(DrumRole.Backbeat, (DrumRole.Time, 0.01)));

    public static PercussionInstrumentDefinition Clap { get; } = new("Clap", [39], 0.1, roles: Plays(DrumRole.Backbeat, (DrumRole.Colour, 0.1)), doubling: 1);

    public static PercussionInstrumentDefinition HiHat { get; } = new(
        "Hi-Hat",
        // closed, pedal, open: the pedal quieter and the open louder, both played less than the closed, the open
        // rarely as the stroke, a loud wash, and now and then as an accent off the beat
        [new DrumSound(42), new DrumSound(44, 0.3, -0.5, 0.15), new DrumSound(46, 0.5, 0.5, 0.05, DrumAccents.OpenHiHat, 1)],
        1.0,
        roles: Plays(DrumRole.Time, (DrumRole.Colour, 0.05), (DrumRole.Backbeat, 0.02))
    );

    public static PercussionInstrumentDefinition Ride { get; } = new(
        "Ride",
        // the ride, its bell, louder, now and then as an accent on the beat, and the second ride
        [new DrumSound(51), new DrumSound(53, 0.4, 0.5, 0.05, DrumAccents.RideBell, -1), new DrumSound(59, 0.6, 0, 0.4)],
        0.5,
        // it keeps time a step slower than the hi-hat
        builder => builder.Add(CompositionStateKinds.Rhythm.Period.Power, 1),
        1,
        roles: Plays(DrumRole.Time, (DrumRole.Colour, 0.15))
    );

    public static PercussionInstrumentDefinition Tambourine { get; } = new(
        "Tambourine",
        [54],
        0.2,
        loudness: 0.5,
        roles: Plays(DrumRole.Time, (DrumRole.Backbeat, 0.3), (DrumRole.Colour, 0.2)),
        doubling: 1
    );

    public static PercussionInstrumentDefinition Cabasa { get; } = new("Cabasa", [69], 0.15, loudness: -0.5, roles: Plays(DrumRole.Time, (DrumRole.Colour, 0.2)), doubling: 0.6);

    public static PercussionInstrumentDefinition Maracas { get; } = new("Maracas", [70], 0.15, loudness: -0.5, roles: Plays(DrumRole.Time, (DrumRole.Colour, 0.2)), doubling: 0.6);

    public static PercussionInstrumentDefinition Tom { get; } = new("Tom", [41, 43, 45, 47, 48, 50], 1.0, walks: true, roles: Plays(DrumRole.Colour, (DrumRole.Ground, 0.1)));

    public static PercussionInstrumentDefinition Cymbal { get; } = new(
        "Cymbal",
        // the crashes, and the china and the splash, which mark a downbeat less, the splash quieter
        [new DrumSound(49), new DrumSound(52, 0.25, 0.3, 0.2), new DrumSound(55, 0.25, -0.5, 0.05), new DrumSound(57, 1, 0, 0.8)],
        1.0,
        loudness: 1,
        roles: Plays(DrumRole.Colour, (DrumRole.Time, 0.05))
    );

    public static PercussionInstrumentDefinition Vibraslap { get; } = new("Vibraslap", [58], 0.1);

    // the percussion stands in for the drum kit by its register, and plays its role in a groove: the low drums ground
    // it as the kick does, the dry high ones play the backbeat as the snare does, and the bells, the bongos and the
    // guiro keep time as the hi-hat does
    public static PercussionInstrumentDefinition Bongo { get; } = new("Bongo", [60, 61], 0.15, walks: true, roles: Plays(DrumRole.Time, (DrumRole.Colour, 0.3), (DrumRole.Backbeat, 0.1)));

    public static PercussionInstrumentDefinition Conga { get; } = new("Conga", [62, 63, 64], 0.15, walks: true, roles: Plays(DrumRole.Ground, (DrumRole.Time, 0.4), (DrumRole.Colour, 0.3)));

    public static PercussionInstrumentDefinition Timbale { get; } = new("Timbale", [65, 66], 0.1, walks: true, roles: Plays(DrumRole.Ground, (DrumRole.Backbeat, 0.3), (DrumRole.Colour, 0.4)));

    public static PercussionInstrumentDefinition Agogo { get; } = new("Agogo", [67, 68], 0.1, walks: true, roles: Plays(DrumRole.Time, (DrumRole.Colour, 0.4)));

    public static PercussionInstrumentDefinition Cowbell { get; } = new("Cowbell", [56], 0.1, roles: Plays(DrumRole.Time, (DrumRole.Backbeat, 0.3), (DrumRole.Colour, 0.3)));

    public static PercussionInstrumentDefinition Claves { get; } = new("Claves", [75], 0.1, roles: Plays(DrumRole.Backbeat, (DrumRole.Time, 0.3), (DrumRole.Colour, 0.3)));

    public static PercussionInstrumentDefinition WoodBlock { get; } = new("Wood Block", [76, 77], 0.1, walks: true, roles: Plays(DrumRole.Backbeat, (DrumRole.Time, 0.3), (DrumRole.Colour, 0.3)));

    public static PercussionInstrumentDefinition Guiro { get; } = new("Guiro", [73, 74], 0.1, walks: true, roles: Plays(DrumRole.Time, (DrumRole.Colour, 0.3)));

    public static PercussionInstrumentDefinition Triangle { get; } = new("Triangle", [80, 81], 0.1);

    public static PercussionInstrumentDefinition Cuica { get; } = new("Cuica", [78, 79], 0.05);

    public static PercussionInstrumentDefinition Whistle { get; } = new("Whistle", [71, 72], 0.05);
}
