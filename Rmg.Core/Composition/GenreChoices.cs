using System.Collections.Immutable;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     The choices a genre may speak of (<see cref="Genre" />), each one the songs draw already, with its options by stable
///     names, in the order the page shows them and a link writes them; a change to them takes a new settings format.
/// </summary>
public static class GenreChoices
{
    private const string TempoAndFeel = "Tempo and feel";
    private const string Harmony = "Harmony";
    private const string Sound = "Sound";
    private const string PartsAndDrums = "Parts and drums";

    /// <summary>The tempos' bands, by the beats a minute they start at, the slowest first.</summary>
    public static ImmutableArray<(string Name, string Label, double From)> TempoBands { get; } =
    [
        ("slow", "Slow", 0),
        ("moderate", "Moderate", 105),
        ("brisk", "Brisk", 125),
        ("fast", "Fast", 150)
    ];

    /// <summary>The band of a tempo, as a multiple of <see cref="Composition.Meter.BaseTempo" />.</summary>
    public static string TempoBandOf(double tempo) => TempoBands.Last(x => tempo * Composition.Meter.BaseTempo >= x.From - 1e-9).Name;

    /// <summary>The song's tempo, by the bands of the tempos it may draw.</summary>
    public static GenreChoice Tempo { get; } = Pool("tempo", "Tempo", TempoAndFeel,
        TempoBands.Select(band => (band.Name, band.Label, SongGenerator.TempoOptions.Select((x, i) => (x, i)).Where(x => TempoBandOf(x.x) == band.Name).Sum(x => SongGenerator.TempoWeights[x.i]))));

    /// <summary>The meter the song's bars are in (<see cref="Composition.Meter.Options" />).</summary>
    public static GenreChoice Meter { get; } = Pool("meter", "Meter", TempoAndFeel,
        Composition.Meter.Options.Select(x => (Composition.Meter.NameOf(x.Meter), Composition.Meter.NameOf(x.Meter), x.Weight.Tuned)));

    /// <summary>The feel the song plays in, straight, a tuplet or a grouping (<see cref="Feels" />).</summary>
    public static GenreChoice Feel { get; } = Pool("feel", "Feel", TempoAndFeel, Feels.Weights.Select(x => (Feels.NameOf(x.Feel), Feels.Label(x.Feel), x.Weight.Tuned)));

    /// <summary>The chances of the feel's: that the song swings.</summary>
    public static GenreChoice FeelChances { get; } = AsTheyAre("feelChances", "Chances", TempoAndFeel, GenreChoiceKind.Chances, [("swing", "Swing")]);

    /// <summary>The values of the feel's: how much a song that swings swings.</summary>
    public static GenreChoice FeelValues { get; } = AsTheyAre("feelValues", "Amounts", TempoAndFeel, GenreChoiceKind.Values, [("swing", "Swing")]);

    /// <summary>The scale the song starts in (<see cref="Scales" />).</summary>
    public static GenreChoice Scale { get; } = Pool("scale", "Scale", Harmony, Scales.All.Select(x => (Name(x.Name), x.Name, x.Weight.Tuned)));

    /// <summary>
    ///     The chords' shapes (<see cref="ChordShapes" />), as likely as the songs have them in the middle, their levels'
    ///     weights times their shares of them.
    /// </summary>
    public static GenreChoice ChordShape { get; } = Pool("chordShape", "Chord shapes", Harmony,
        ChordShapes.All.Select(x => (Name(x.Name), x.Name,
            ChordShapes.Levels.Single(l => l.Level == x.Unconventionality).Weight.Tuned * x.Weight / ChordShapes.All.Where(y => y.Unconventionality == x.Unconventionality).Sum(y => y.Weight))));

    // the pitched parts' pools, in the order the page shows them
    private static readonly (TrackRole Role, string Label, InstrumentRole Pool)[] InstrumentPools =
    [
        (TrackRole.Melody, "Melody", InstrumentRoles.Melody),
        (TrackRole.Chords, "Chords", InstrumentRoles.Chords),
        (TrackRole.Bass, "Bass", InstrumentRoles.Bass),
        (TrackRole.Pad, "Pad", InstrumentRoles.Pad),
        (TrackRole.CounterMelody, "Counter-melody", InstrumentRoles.CounterMelody),
        (TrackRole.Riff, "Riff", InstrumentRoles.Riff),
        (TrackRole.Rhythm, "Rhythm", InstrumentRoles.Rhythm),
        (TrackRole.Solo, "Soloist", InstrumentRoles.Solo)
    ];

    /// <summary>Every pitched part's instruments, by its role, its pool's (<see cref="InstrumentRoles" />).</summary>
    public static ImmutableDictionary<TrackRole, GenreChoice> Instruments { get; } = InstrumentPools.ToImmutableDictionary(
        x => x.Role,
        x => Pool($"instrument.{Name(x.Role.ToString())}", x.Label, Sound, x.Pool.Instruments.Select(i => (Name(i.Name), i.Name, i.Weight)))
    );

    /// <summary>The chances of the parts a song may leave out.</summary>
    public static GenreChoice Parts { get; } = AsTheyAre("parts", "Parts", PartsAndDrums, GenreChoiceKind.Chances,
    [
        ..new (TrackRole Role, string Label)[]
        {
            (TrackRole.Pad, "Pad"), (TrackRole.CounterMelody, "Counter-melody"), (TrackRole.Drum, "Drums"), (TrackRole.Riff, "Riff"),
            (TrackRole.RiffTwin, "Riff's twin"), (TrackRole.Rhythm, "Rhythm")
        }.Select(x => (Name(x.Role.ToString()), x.Label))
    ]);

    /// <summary>The drums the song plays: the kit, the kit and percussion, or percussion (<see cref="DrumSetups" />).</summary>
    public static GenreChoice DrumSetup { get; } = Pool("drumSetup", "Drum setup", PartsAndDrums,
        DrumSetups.All.Select(x => (NameOf(x.Setup), x.Setup switch
        {
            Composition.DrumSetup.Kit => "Drum kit",
            Composition.DrumSetup.KitAndPercussion => "Kit and percussion",
            _ => "Percussion"
        }, x.Weight)));

    /// <summary>The drum pieces the song may draw (<see cref="DrumPieces" />).</summary>
    public static GenreChoice DrumPieces { get; } = Pool("drumPieces", "Drum pieces", PartsAndDrums,
        DrumGroups.AllDrums.Select(x => (Composition.DrumPieces.NameOf(x), x.Name, x.Weight)));

    /// <summary>Every choice, in the order the page shows them.</summary>
    public static ImmutableArray<GenreChoice> All { get; } =
    [
        Tempo, Meter, Feel, FeelChances, FeelValues, Scale, ChordShape,
        ..InstrumentPools.Select(x => Instruments[x.Role]),
        Parts, DrumSetup, DrumPieces
    ];

    /// <summary>A pool's choice, every option's default its weight against the heaviest's.</summary>
    private static GenreChoice Pool(string name, string label, string group, IEnumerable<(string Name, string Label, double Weight)> options)
    {
        var all = options.ToArray();
        var heaviest = all.Max(x => x.Weight);
        return new GenreChoice(name, label, group, GenreChoiceKind.Pool, [..all.Select(x => new GenreOption(x.Name, x.Label, x.Weight / heaviest))]);
    }

    /// <summary>A choice of chances or values, every one's default as it is.</summary>
    private static GenreChoice AsTheyAre(string name, string label, string group, GenreChoiceKind kind, IEnumerable<(string Name, string Label)> options) =>
        new(name, label, group, kind, [..options.Select(x => new GenreOption(x.Name, x.Label, 0.5))]);

    /// <summary>
    ///     A name as a genre gives it: in camel case, its words run together, "harmonicMinor", "electricBassFinger",
    ///     "counterMelody".
    /// </summary>
    public static string Name(string text)
    {
        var words = text.Split([' ', '-', '(', ')', '.', '/'], StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(words.Select((word, i) => (i == 0 ? char.ToLowerInvariant(word[0]) : char.ToUpperInvariant(word[0])) + word[1..]));
    }

    /// <summary>The name of a drum setup's option.</summary>
    public static string NameOf(Composition.DrumSetup setup) => setup switch
    {
        Composition.DrumSetup.Kit => "kit",
        Composition.DrumSetup.KitAndPercussion => "kitAndPercussion",
        _ => "percussion"
    };
}
