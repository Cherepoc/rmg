using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Core.Composition;

public static class DrumGroups
{
    public const int FirstTrackNumber = 100;

    /// <summary>The chance that a section may groove on the toms, which play mostly in fills.</summary>
    private const double TomsGrooveChance = 0.25;

    /// <summary>The chance that a section may groove on the cymbal, which marks mostly where a section lands.</summary>
    private const double AccentsGrooveChance = 0.12;

    /// <summary>The chance of a song having the vibraslap, which it plays as a landing, with the cymbal.</summary>
    private const double VibraslapChance = 0.2;


    /// <summary>
    ///     A group's part in a fill's run: its chance of joining one, and how unconventional that is, the power of the
    ///     section's chance scale the chance is multiplied by.
    /// </summary>
    private static StateMapBuilder InRuns(this StateMapBuilder builder, double chance, double unconventionality = 1)
    {
        return builder
            .Add(CompositionStateKinds.Fill.RunChance, chance)
            .Add(CompositionStateKinds.Fill.Unconventionality, unconventionality);
    }

    public static DrumGroup Kick { get; } = new(
        nameof(Kick),
        [DrumDefinitions.Kick],
        1.0,
        1,
        true,
        builder => builder.InRuns(0.04),
        holdsARole: true
    );

    // a song has one main snare, an acoustic or an electric snare, whose strokes are its head and its cross-stick, or a
    // clap
    public static DrumGroup Snare { get; } = new(
        nameof(Snare),
        [
            DrumDefinitions.AcousticSnare,
            DrumDefinitions.ElectricSnare,
            DrumDefinitions.Clap
        ],
        1.0,
        1,
        true,
        // in most runs, as a roll or with the toms
        builder => builder.InRuns(0.5, 0),
        new SongDrumRule([SongDrumRule.OneOf(DrumDefinitions.AcousticSnare, DrumDefinitions.ElectricSnare, DrumDefinitions.Clap)]),
        holdsARole: true
    );

    // the timekeepers are optional, but most sections keep time on them: over 200 corpus songs they groove in 85% of
    // the sections, against 76% at the weight of the other groups
    public static DrumGroup Timekeepers { get; } = new(
        nameof(Timekeepers),
        [
            DrumDefinitions.HiHat,
            DrumDefinitions.Ride,
            DrumDefinitions.Tambourine,
            DrumDefinitions.Cabasa,
            DrumDefinitions.Maracas
        ],
        3.0,
        1,
        configureStateMap: builder => builder.InRuns(0.05),
        holdsARole: true
    );

    // the toms play mostly in fills; now and then a section grooves on them, as on a floor tom or in a tribal beat
    public static DrumGroup Toms { get; } = new(
        nameof(Toms),
        [DrumDefinitions.Tom],
        0.5,
        1,
        configureStateMap: builder => builder.InRuns(0.65, 0),
        grooveChance: TomsGrooveChance
    );

    // a crash marks where a section lands, so it rarely plays in a groove, and then only on the downbeats, as a crash
    // ride in a loud section; some songs have a vibraslap, which lands now and then in the cymbal's place, as light as
    // it is, and all but never grooves
    public static DrumGroup Accents { get; } = new(
        nameof(Accents),
        [DrumDefinitions.Cymbal, DrumDefinitions.Vibraslap],
        0.4,
        1,
        configureStateMap: builder => builder.Add(CompositionStateKinds.Rhythm.MaxRank, -2).InRuns(0.04),
        songRule: new SongDrumRule([SongDrumRule.Always(DrumDefinitions.Cymbal), SongDrumRule.Optional(DrumDefinitions.Vibraslap, VibraslapChance)]),
        grooveChance: AccentsGrooveChance
    );

    // a song has its own few percussion instruments, or none; the triangle, the cuica and the whistle are left out, as
    // they grate in a groove of any length, until a genre calls for them
    private static readonly ImmutableArray<PercussionInstrumentDefinition> PercussionDrums =
    [
        DrumDefinitions.Bongo,
        DrumDefinitions.Conga,
        DrumDefinitions.Timbale,
        DrumDefinitions.Agogo,
        DrumDefinitions.Cowbell,
        DrumDefinitions.Claves,
        DrumDefinitions.WoodBlock,
        DrumDefinitions.Guiro
    ];

    public static DrumGroup Percussion { get; } = new(
        nameof(Percussion),
        PercussionDrums,
        0.4,
        2,
        configureStateMap: builder => builder.InRuns(0.08),
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

    /// <summary>The drum on the given track.</summary>
    public static PercussionInstrumentDefinition GetDrum(int trackNumber)
    {
        return TrackNumbers.Single(x => x.Value == trackNumber).Key;
    }

    /// <summary>The group of the drum on the given track.</summary>
    public static DrumGroup GetGroup(int trackNumber)
    {
        var drum = TrackNumbers.Single(x => x.Value == trackNumber).Key;
        return All.Single(x => x.Drums.Contains(drum));
    }
}
