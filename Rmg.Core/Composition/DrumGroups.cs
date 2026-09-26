using System.Collections.Immutable;

namespace Rmg.Core.Composition;

public static class DrumGroups
{
    public const int FirstTrackNumber = 100;

    /// <summary>The chance that a section may groove on the toms, which play mostly in fills.</summary>
    private const double TomsGrooveChance = 0.25;

    /// <summary>The chance that a section may groove on the cymbal, which marks mostly where a section lands.</summary>
    private const double AccentsGrooveChance = 0.12;

    /// <summary>Chance of a song having a sidestick along with an acoustic or an electric snare.</summary>
    private const double CrossStickWithSnareProbability = 0.15;

    public static DrumGroup Kick { get; } = new(
        nameof(Kick),
        [DrumDefinitions.Kick],
        1.0,
        1,
        true
    );

    // a song has one main snare, that is either an acoustic or an electric snare, a clap or a sidestick.
    // acoustic and electric snares can also be joined by a sidestick, but clap can't. the drums of the group
    // never play together in a section, so a section has either a snare or a sidestick
    public static DrumGroup Snare { get; } = new(
        nameof(Snare),
        [
            DrumDefinitions.CrossStick,
            DrumDefinitions.AcousticSnare,
            DrumDefinitions.ElectricSnare,
            DrumDefinitions.Clap
        ],
        1.0,
        1,
        true,
        // backbeat: a cycle of half a bar, shifted by half of it, puts the main hits on beats 2 and 4, and with no
        // weaker hits of its own the snare plays nothing else unless the rhythm layers add ghost notes
        builder => builder
            .Add(CompositionStateKinds.Rhythm.Period.Power, -1)
            .Add(CompositionStateKinds.Rhythm.Phase.Rank, 1)
            .Add(CompositionStateKinds.Rhythm.MaxRank, -2)
            // and keeps its figure, bar after bar
            .Add(CompositionStateKinds.Rhythm.Variation, -0.3),
        new SongDrumRule(
            [
                SongDrumRule.OneOf(
                    DrumDefinitions.AcousticSnare,
                    DrumDefinitions.ElectricSnare,
                    DrumDefinitions.Clap,
                    DrumDefinitions.CrossStick
                ),
                SongDrumRule.Optional(DrumDefinitions.CrossStick, CrossStickWithSnareProbability, DrumDefinitions.Clap)
            ]
        )
    );

    public static DrumGroup Timekeepers { get; } = new(
        nameof(Timekeepers),
        [
            DrumDefinitions.HiHat,
            DrumDefinitions.Ride,
            DrumDefinitions.Tambourine,
            DrumDefinitions.Cabasa,
            DrumDefinitions.Maracas
        ],
        1.0,
        1
    );

    // the toms play mostly in fills; now and then a section grooves on them, as on a floor tom or in a tribal beat
    public static DrumGroup Toms { get; } = new(
        nameof(Toms),
        [DrumDefinitions.Tom],
        0.5,
        1,
        grooveChance: TomsGrooveChance
    );

    // a crash marks where a section lands, so it rarely plays in a groove, and then only on the downbeats, as a crash
    // ride in a loud section
    public static DrumGroup Accents { get; } = new(
        nameof(Accents),
        [DrumDefinitions.Cymbal],
        0.4,
        1,
        configureStateMap: builder => builder.Add(CompositionStateKinds.Rhythm.MaxRank, -2),
        grooveChance: AccentsGrooveChance
    );

    // a song has its own few percussion instruments, or none
    private static readonly ImmutableArray<PercussionInstrumentDefinition> PercussionDrums =
    [
        DrumDefinitions.Bongo,
        DrumDefinitions.Conga,
        DrumDefinitions.Timbale,
        DrumDefinitions.Agogo,
        DrumDefinitions.Cowbell,
        DrumDefinitions.Claves,
        DrumDefinitions.WoodBlock,
        DrumDefinitions.Guiro,
        DrumDefinitions.Triangle,
        DrumDefinitions.Cuica,
        DrumDefinitions.Whistle,
        DrumDefinitions.Vibraslap
    ];

    public static DrumGroup Percussion { get; } = new(
        nameof(Percussion),
        PercussionDrums,
        0.4,
        2,
        songRule: new SongDrumRule([SongDrumRule.Pool(0, 4, [..PercussionDrums])])
    );

    public static ImmutableArray<DrumGroup> All { get; } =
    [
        Kick,
        Snare,
        Timekeepers,
        Toms,
        Accents,
        Percussion
    ];

    public static ImmutableArray<PercussionInstrumentDefinition> AllDrums { get; } =
        [..All.SelectMany(x => x.Drums)];

    private static readonly ImmutableDictionary<PercussionInstrumentDefinition, int> TrackNumbers =
        AllDrums
            .Select((drum, index) => (drum, index))
            .ToImmutableDictionary(x => x.drum, x => FirstTrackNumber + x.index);

    public static int GetTrackNumber(PercussionInstrumentDefinition drum)
    {
        return TrackNumbers[drum];
    }
}
