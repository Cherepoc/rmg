using System.Collections.Immutable;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     The choices a genre may speak of (<see cref="Genre" />), each one the songs draw already, with its options by stable
///     names, in the order a link writes them; a change to them takes a new settings format. The page shows them by their
///     names' families: the instruments ("instrument."), the drums' roles ("drumRole."), and every part's rhythm and feel
///     ("rhythm.", "feel.") as tabs of a part each.
/// </summary>
public static class GenreChoices
{

    /// <summary>
    ///     A tempo, as a multiple of <see cref="Composition.Meter.BaseTempo" />, by its amount from 0, 60 beats a minute,
    ///     through 1/2, 120, to 1, 240, every like step a like ratio, as a tempo given is (the page's tempo's steps).
    /// </summary>
    public static double TempoOf(double amount) => Math.Pow(2, 2 * amount - 1);

    /// <summary>A tempo's amount, as a multiple of <see cref="Composition.Meter.BaseTempo" /> (<see cref="TempoOf" />).</summary>
    public static double AmountOfTempo(double tempo) => (Math.Log2(tempo) + 1) / 2;

    /// <summary>
    ///     The song's tempo, a range of them from 60 to 240 beats a minute, as wide as a tempo given may be, starting from the
    ///     tempos the songs draw, from 90 to about 175, and how a song's leans within it, to the slow end or the fast.
    /// </summary>
    public static GenreChoice Tempo { get; } = new("tempo", "Tempo", GenreChoiceKind.Range,
    [
        new("from", "From", Even(AmountOfTempo(SongGenerator.TempoOptions[0]))),
        new("to", "To", Even(AmountOfTempo(SongGenerator.TempoOptions[^1]))),
        new("lean", "Lean", Even(0.5))
    ]);

    /// <summary>The meter the song's bars are in (<see cref="Composition.Meter.Options" />).</summary>
    public static GenreChoice Meter { get; } = Pool("meter", "Meter",
        Composition.Meter.Options.Select(x => (Composition.Meter.NameOf(x.Meter), Composition.Meter.NameOf(x.Meter), x.Weight)));

    /// <summary>The feel the song plays in, straight, a tuplet or a grouping (<see cref="Feels" />).</summary>
    public static GenreChoice Feel { get; } = Pool("feel", "Feel", Feels.Weights.Select(x => (Feels.NameOf(x.Feel), Capitalised(Feels.Label(x.Feel)), x.Weight)));

    /// <summary>The chances of the feel's: that the song swings, and that a section's drums play in half or double time.</summary>
    public static GenreChoice FeelChances { get; } = Chances("feelChances", "Swing and half time",
    [
        ("swing", "Swings", Groove.SwingChance), ("halfTime", "A section's drums in half time", Even(Groove.HalfTimeChance)),
        ("doubleTime", "A section's drums in double time", Even(Groove.DoubleTimeChance))
    ]);

    /// <summary>The values of the feel's: how much a song that swings swings.</summary>
    public static GenreChoice FeelValues { get; } = Values("feelValues", "Swing amount", [("swing", "How much it swings")]);

    /// <summary>The scale the song starts in (<see cref="Scales" />).</summary>
    public static GenreChoice Scale { get; } = Described(Pool("scale", "Scale", Scales.All.Select(x => (Name(x.Name), x.Name, x.Weight))),
        Scales.All.Select(x => $"{x.Description} {NotesOnC(x.Offsets.Select(o => (double)o))}"));

    /// <summary>
    ///     The chords' shapes (<see cref="ChordShapes" />), as likely as the songs have them, their levels' weights times
    ///     their shares of them.
    /// </summary>
    public static GenreChoice ChordShape { get; } = Described(Pool("chordShape", "Chord shapes",
        ChordShapes.All.Select(x => (Name(x.Name), x.Name, ChordShapes.Levels.Single(l => l.Level == x.Unconventionality).Weight is var level
            && x.Weight / ChordShapes.All.Where(y => y.Unconventionality == x.Unconventionality).Sum(y => y.Weight) is var share
                ? new ByConvention(level.Plain * share, level.Tuned * share, level.Wild * share)
                : default))),
        ChordShapes.All.Select(x => $"{x.Description} {NotesOnC(x.Targets)}"));

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
        x => Pool($"instrument.{Name(x.Role.ToString())}", x.Label, x.Pool.Instruments.Select(i => (Name(i.Name), i.Name, Even(i.Weight))))
    );

    /// <summary>The drum kits' sounds, by their General MIDI programs, the standard one every song's with no genre.</summary>
    public static ImmutableArray<(int Program, string Name)> Kits { get; } =
        [(0, "standard"), (8, "room"), (16, "power"), (24, "electronic"), (25, "tr808"), (32, "jazz"), (40, "brush"), (48, "orchestra")];

    /// <summary>A kit's name, by its program.</summary>
    public static string KitNameOf(int program) => Kits.Single(x => x.Program == program).Name;

    /// <summary>The drum kit's sound (<see cref="GeneralMidi.KitName" />).</summary>
    public static GenreChoice Kit { get; } = Pool("kit", "Drum kit", Kits.Select(x => (x.Name, GeneralMidi.KitName(x.Program), Even(x.Program == 0 ? 1 : 0))));

    /// <summary>
    ///     Every part's rhythm, a pitched part's or the drums' of a role, as values: its speed, how far off the beat it
    ///     plays, how fine it goes, how much weight its weaker beats have, how full it is and how much it repeats its
    ///     cycles (<see cref="GenreRhythm" />), and the melody's busyness.
    /// </summary>
    public static ImmutableDictionary<RhythmPart, GenreChoice> Rhythms { get; } = Enum.GetValues<RhythmPart>().ToImmutableDictionary(
        x => x,
        x => Values($"rhythm.{Name(x.ToString())}", x switch
            {
                RhythmPart.CounterMelody => "Counter-melody",
                RhythmPart.Ground => "Ground (kick)",
                RhythmPart.Backbeat => "Backbeat (snare)",
                RhythmPart.Time => "Time (hi-hat)",
                RhythmPart.Colour => "Colour (toms)",
                _ => x.ToString()
            },
            [..GenreRhythm.Settings, ..x == RhythmPart.Melody ? [("busyness", "Busyness")] : Array.Empty<(string, string)>()])
    );

    /// <summary>
    ///     Every part's own feel, a pitched part's or the drums' of a role, such as a time keeper's grouping of the
    ///     tresillo's 3+3+2, over the song's, drawn only where a genre names it.
    /// </summary>
    public static ImmutableDictionary<RhythmPart, GenreChoice> PartFeels { get; } = Enum.GetValues<RhythmPart>().ToImmutableDictionary(
        x => x,
        x => Pool($"feel.{Name(x.ToString())}", Rhythms[x].Label, Feels.Weights.Select(f => (Feels.NameOf(f.Feel), Capitalised(Feels.Label(f.Feel)), f.Weight)))
    );

    // the drums' roles in the order the page shows them
    private static readonly DrumRole[] RolesInOrder = [DrumRole.Ground, DrumRole.Backbeat, DrumRole.Time, DrumRole.Colour];

    /// <summary>Which drums take a role in the groove, each by its affinity for it as the songs have it (<see cref="DrumRoles" />).</summary>
    public static ImmutableDictionary<DrumRole, GenreChoice> DrumRoles { get; } = RolesInOrder.ToImmutableDictionary(
            x => x,
            x => WithFamilies(Pool($"drumRole.{Name(x.ToString())}", Rhythms[Composition.DrumRoles.PartOf(x)].Label,
                DrumGroups.AllDrums.Select(d => (Composition.DrumPieces.NameOf(d), d.Name, Even(d.AffinityFor(x))))))
        );

    /// <summary>The chances of the parts, every one a song may have or leave out, each as a middling song has it.</summary>
    public static GenreChoice Parts { get; } = WithTwinsRiff(Chances("parts", "Parts playing",
    [
        ..new (TrackRole Role, string Label)[]
        {
            (TrackRole.Melody, "Melody"), (TrackRole.Chords, "Chords"), (TrackRole.Bass, "Bass"), (TrackRole.Pad, "Pad"),
            (TrackRole.CounterMelody, "Counter-melody"), (TrackRole.Drum, "Drums"), (TrackRole.Riff, "Riff"), (TrackRole.RiffTwin, "Riff's twin"),
            (TrackRole.Rhythm, "Rhythm"), (TrackRole.Solo, "Soloist")
        }.Select(x => (Name(x.Role.ToString()), x.Label, SongParts.ChanceOf(x.Role)))
    ]));

    // the riff's twin tagged as playing only with the riff
    private static GenreChoice WithTwinsRiff(GenreChoice parts) =>
        parts with { Options = [..parts.Options.Select(x => x.Name == Name(nameof(TrackRole.RiffTwin)) ? x with { Tags = [$"with:{Name(nameof(TrackRole.Riff))}"] } : x)] };

    /// <summary>The drums the song plays: the kit, the kit and percussion, or percussion (<see cref="DrumSetups" />).</summary>
    public static GenreChoice DrumSetup { get; } = Pool("drumSetup", "Drum setup",
        DrumSetups.All.Select(x => (NameOf(x.Setup), x.Setup switch
        {
            Composition.DrumSetup.Kit => "Kit",
            Composition.DrumSetup.KitAndPercussion => "Both",
            _ => "Percussion"
        }, RhythmicUnconventionality.WeightEnds(x.Weight, x.Lean))));

    /// <summary>The drum pieces the song may draw (<see cref="DrumPieces" />).</summary>
    public static GenreChoice DrumPieces { get; } = WithFamilies(Pool("drumPieces", "Drum pieces",
        DrumGroups.AllDrums.Select(x => (Composition.DrumPieces.NameOf(x), x.Name, Even(x.Weight)))));

    /// <summary>
    ///     A choice of drums, every one tagged with its families, "kit" and "percussion", so that the page offers the setups
    ///     the drums chosen make.
    /// </summary>
    private static GenreChoice WithFamilies(GenreChoice choice) => choice with
    {
        Options = [..choice.Options.Select(option => option with { Tags = FamiliesOf(DrumGroups.AllDrums.Single(x => Composition.DrumPieces.NameOf(x) == option.Name)) })]
    };

    private static ImmutableArray<string> FamiliesOf(PercussionInstrumentDefinition drum) =>
        [..new[] { (DrumFamily.Kit, "kit"), (DrumFamily.Percussion, "percussion") }.Where(x => drum.Family.HasFlag(x.Item1)).Select(x => x.Item2)];

    private static string Capitalised(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>Every choice, in the order a link writes them.</summary>
    public static ImmutableArray<GenreChoice> All { get; } =
    [
        Tempo, Meter, Feel, FeelChances, FeelValues, Scale, ChordShape,
        ..InstrumentPools.Select(x => Instruments[x.Role]),
        Kit,
        Parts, DrumSetup, DrumPieces,
        ..Enum.GetValues<RhythmPart>().Select(x => Rhythms[x]),
        ..Enum.GetValues<RhythmPart>().Select(x => PartFeels[x]),
        ..RolesInOrder.Select(x => DrumRoles[x])
    ];

    /// <summary>A pool's choice, every option's own weights against every end's heaviest, 0 at an end that allows none.</summary>
    private static GenreChoice Pool(string name, string label, IEnumerable<(string Name, string Label, ByConvention Weight)> options)
    {
        var all = options.ToArray();
        var (plain, tuned, wild) = (all.Max(x => x.Weight.Plain), all.Max(x => x.Weight.Tuned), all.Max(x => x.Weight.Wild));
        static double Of(double weight, double heaviest) => heaviest > 0 ? weight / heaviest : 0;
        return new GenreChoice(name, label, GenreChoiceKind.Pool,
            [..all.Select(x => new GenreOption(x.Name, x.Label, new ByConvention(Of(x.Weight.Plain, plain), Of(x.Weight.Tuned, tuned), Of(x.Weight.Wild, wild))))]);
    }

    // a weight drawn alike at every conventionality
    private static ByConvention Even(double weight) => new(weight, weight, weight);

    /// <summary>A choice with its options described, in their order.</summary>
    private static GenreChoice Described(GenreChoice choice, IEnumerable<string> descriptions) =>
        choice with { Options = [..choice.Options.Zip(descriptions, (option, description) => option with { Description = description })] };

    /// <summary>
    ///     Heights in semitones as notes on C, "On C: C E♭ G.", a height between two notes both, "E♭/E", as the scale
    ///     chooses between them.
    /// </summary>
    public static string NotesOnC(IEnumerable<double> heights)
    {
        string[] names = ["C", "D♭", "D", "E♭", "E", "F", "F♯", "G", "A♭", "A", "B♭", "B"];
        string Note(double height) => names[(int)height % 12];
        return $"On C: {string.Join(" ", heights.Select(x => x == Math.Floor(x) ? Note(x) : $"{Note(Math.Floor(x))}/{Note(Math.Ceiling(x))}"))}.";
    }

    /// <summary>A choice of chances, every one's own its chances as the songs have them.</summary>
    private static GenreChoice Chances(string name, string label, IEnumerable<(string Name, string Label, ByConvention Chance)> options) =>
        new(name, label, GenreChoiceKind.Chances, [..options.Select(x => new GenreOption(x.Name, x.Label, x.Chance))]);

    /// <summary>A choice of values, every one's own as it is.</summary>
    private static GenreChoice Values(string name, string label, IEnumerable<(string Name, string Label)> options) =>
        new(name, label, GenreChoiceKind.Values, [..options.Select(x => new GenreOption(x.Name, x.Label, Even(0.5)))]);

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
