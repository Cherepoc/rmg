using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     Generates songs, in stages that each draw from the song's seed in turn: the tracks and their instruments
///     (<see cref="SongTracks" />), the song's harmony and state, its structure of parts and sections, and every
///     section (<see cref="SectionGenerator" />) with its bar state (<see cref="BarStateGenerator" />) and its tracks'
///     patterns (<see cref="PatternGenerator" />). A section that comes back is played as it was first generated.
/// </summary>
public static class SongGenerator
{
    // the song's speed as a multiple of 120 BPM, from 90 to about 175 BPM: 120 itself is the likeliest, and the finer
    // a multiplier's fraction, the less likely it is
    internal static readonly Func<IGenerationContext, double> TempoGenerator = Generators.DyadicMultiplier(
        WeightUtil.CreateGeometricRankWeightFunc(0, 0, 1.0, 0.75),
        4,
        0.75,
        1.5
    );

    public static Song GenerateSong()
    {
        return GenerateSong(Seeds.Random());
    }

    /// <summary>
    ///     Generates a song. The same seed always results in the same song in this version; changes to the generator
    ///     may turn a seed into a different song, which is accepted to keep the generator simple.
    /// </summary>
    public static Song GenerateSong(ulong seed) => GenerateSong(seed, SongOverrides.None);

    /// <summary>
    ///     Generates a song with what is given used as given, such as its unconventionality's base, from 0 for the
    ///     plainest to 1 for the wildest, or the parts it has, and the rest drawn as <see cref="GenerateSong(ulong)" />
    ///     draws it.
    /// </summary>
    public static Song GenerateSong(ulong seed, SongOverrides overrides) => GenerateSong(seed, overrides, CancellationToken.None);

    /// <summary>
    ///     Generates a song as <see cref="GenerateSong(ulong, SongOverrides)" /> does, given up at the next stage once
    ///     nobody waits for it any more.
    /// </summary>
    public static Song GenerateSong(ulong seed, SongOverrides overrides, CancellationToken cancellation) =>
        GenerateSong(seed, overrides, ImmutableHashSet<string>.Empty, cancellation);

    /// <summary>The stages a song may be made without, by their names (<see cref="SongStages" />), as a listening test hears one with and without.</summary>
    public static ImmutableArray<string> SkippableStages { get; } = [..SongStages.All.Where(x => x.CanSkip).Select(x => x.Name)];

    /// <summary>The song of the seed as <see cref="GenerateSong(ulong, SongOverrides, CancellationToken)" /> makes it, the stages named left out.</summary>
    /// <param name="skipped">Stages of <see cref="SkippableStages" /> to leave out, as a listening test compares a song with and without one.</param>
    public static Song GenerateSong(ulong seed, SongOverrides overrides, IReadOnlySet<string> skipped, CancellationToken cancellation) =>
        GenerateSong(seed, overrides, skipped, (_, _) => { }, cancellation);

    /// <summary>
    ///     The song of the seed, the stages given left out (<see cref="SongStages" />), as a test measures what one does, and
    ///     the song after each stage observed, as a test checks it.
    /// </summary>
    /// <param name="overrides">What is set in place of the song's own draws, each checked (<see cref="SongOverrides.Check" />).</param>
    /// <param name="cancellation">Gives the song up between its stages, which draw nothing to check it.</param>
    internal static Song GenerateSong(ulong seed, SongOverrides overrides, IReadOnlySet<string> skipped, Action<string, SongDraft> observe, CancellationToken cancellation)
    {
        if (skipped.FirstOrDefault(x => !SkippableStages.Contains(x)) is { } unknown)
            throw new ArgumentOutOfRangeException(nameof(skipped), unknown, $"A stage left out is one of {string.Join(", ", SkippableStages)}.");
        overrides.Check();

        var song = SongDecisions.Draw(seed, overrides, cancellation) with { StopTimeSkipped = skipped.Contains("Stop time") };
        var (genre, tracks, form, songUnconventionality) = (song.Genre, song.Tracks, song.Form, song.Facets);
        IGenerationContext Stream(SongStream stream) => CreateStream(seed, stream);
        // the song put together as planned, then every stage after it in its order
        var staged = SongStages.Run(song, new SongDraft(form.Edits.ApplyTo(form.Blocks.Unroll()), null, [], [], ImmutableDictionary<int, ImmutableDictionary<FigurePlayer, string>>.Empty, []), skipped, observe, cancellation);
        var (songTrackNoteTimelineMap, notes) = (staged.Timeline, staged.Notes!);

        // the drum kit's sound, the standard kit's, or one of a genre's pool of them, from a stream of its own; drums marking the
        // form on a kit that colours, of the genre's pool, or the orchestra's where it has none (DrumMarks.Kits)
        var pool = genre.Pool<int>(GenreChoices.Kit, GenreChoices.KitNameOf);
        if (song.DrumJob == DrumJob.Marks)
            pool = pool is { } named && DrumMarks.Kits.Any(x => named(x) is not null)
                ? program => DrumMarks.Kits.Contains(program) ? named(program) : null
                : program => program == DrumMarks.Kits[0] ? new ByConvention(1, 1, 1) : null;
        var kit = pool is { } kits
            ? Stream(SongStream.Kit).Pick(ByConvention.Weigh(GenreChoices.Kits.Select(x => (x.Program, x.Program == 0 ? new ByConvention(1, 1, 1) : default)), songUnconventionality[Facet.Sound], kits))
            : 0;
        // and its reverb's room, one of a genre's pool of them, the player's own where it names none, from a stream of its own
        var room = genre.Pool<int>(GenreChoices.Room, GenreChoices.RoomNameOf) is { } rooms
            ? Stream(SongStream.Room).Pick(ByConvention.Weigh(GenreChoices.Rooms.Select(x => (x.Macro, default(ByConvention))), songUnconventionality[Facet.Sound], rooms))
            : (int?)null;
        // and the effect it plays one part through, if any, from a stream of its own
        var insertion = Insertions.Draw(Stream(SongStream.Insertion), songUnconventionality[Facet.Sound], genre,
            tracks.Definitions.Values.OfType<PitchInstrumentTrack>().Where(x => !song.Absent.Contains(x.Role)).Select(x => (x.Role, x.InstrumentCode)));

        return new Song(songTrackNoteTimelineMap.Duration, song.Meter, tracks.Definitions, songTrackNoteTimelineMap, form.Map, notes, new SongDraws(songUnconventionality,
            [..SongParts.All.Except(song.Absent)], tracks.DrumSetup, [..tracks.SongDrums.Select(DrumPieces.NameOf)], song.Scale, song.Swing, kit, room, insertion,
            [..form.Map.Sections.Select((x, i) => (song.Structure.Roles[x.SectionId], song.Solos.TryGetValue(i, out var solo) ? solo.Soloists : []))], song.Mood));
    }

    /// <summary>The random sequence a stage of the song of the given seed draws from.</summary>
    internal static IGenerationContext CreateStream(ulong seed, SongStream stream)
    {
        return new GenerationContext(Seeds.Derive(seed, (int)stream));
    }

    /// <summary>A stream's own sequence for a place in it, such as a facet of the song's unconventionality, a part or an appearance.</summary>
    internal static IGenerationContext CreateStream(ulong seed, SongStream stream, long place)
    {
        return new GenerationContext(Seeds.Derive(Seeds.Derive(seed, (int)stream), place));
    }

    /// <summary>The tempos a song may play at, as multiples of <see cref="Meter.BaseTempo" />, slowest first: every one the draw can land on.</summary>
    public static ImmutableArray<double> TempoOptions { get; } =
    [
        ..Enumerable.Range(0, 5)
            .SelectMany(rank => DyadicRankDistribution.GetMultiplierDistributionSlice(rank, 0.75, 1.5))
            .Select(x => x.Position)
            .Distinct()
            .Order()
    ];

    /// <summary>How far a range's lean of 1 skews a draw across it: its place to the power of e to the minus this.</summary>
    public const double RangeLeanSkew = 1;

    /// <summary>
    ///     A tempo in a range of amounts (<see cref="GenreChoices.TempoOf" />), leaning to its middle, as the mean of two
    ///     draws across it does, or to an end by its lean, at a whole number of beats a minute.
    /// </summary>
    internal static double TempoIn(IGenerationContext context, (double From, double To, double Lean) range)
    {
        var tempo = GenreChoices.TempoOf(Across(context, range));
        return Math.Round(tempo * Meter.BaseTempo) / Meter.BaseTempo;
    }

    /// <summary>
    ///     The bands of a genre's tempo range, its slow, middle and fast thirds, a song's drawn in one of them where the genre
    ///     weighs them, as a jazz record is a ballad, a medium or a burner and seldom between: a genre's alone.
    /// </summary>
    public static ImmutableArray<(TempoBand Band, ByConvention Weight)> TempoBands { get; } =
        [(TempoBand.Slow, new(0, 0, 0)), (TempoBand.Middle, new(0, 0, 0)), (TempoBand.Fast, new(0, 0, 0))];

    /// <summary>A range of amounts narrowed to one of its thirds, its lean kept.</summary>
    internal static (double From, double To, double Lean) InBand((double From, double To, double Lean) range, TempoBand band) =>
        (range.From + (range.To - range.From) * (int)band / 3, range.From + (range.To - range.From) * ((int)band + 1) / 3, range.Lean);

    /// <summary>A length in a range of amounts (<see cref="GenreChoices.LengthOf" />), in seconds, drawn as a tempo is in its range.</summary>
    internal static double LengthIn(IGenerationContext context, (double From, double To, double Lean) range) => GenreChoices.LengthOf(Across(context, range));

    /// <summary>A mood in a range of amounts (<see cref="Mood.OfAmount" />), drawn as a tempo is in its range.</summary>
    internal static double MoodIn(IGenerationContext context, (double From, double To, double Lean) range) => Mood.OfAmount(Across(context, range));

    /// <summary>An amount in a range, leaning to its middle, as the mean of two draws across it does, or to an end by its lean.</summary>
    private static double Across(IGenerationContext context, (double From, double To, double Lean) range) =>
        range.From + (range.To - range.From) * Math.Pow((context.GenerateDouble() + context.GenerateDouble()) / 2, Math.Exp(-RangeLeanSkew * range.Lean));
}

/// <summary>
///     The random sequences of a song's stages, each derived from the song's seed by its number, which must stay as it
///     is: a new stage takes a new number.
/// </summary>
internal enum SongStream
{
    Rhythm = 1,
    Drums = 2,
    Harmony = 3,
    SongState = 4,
    Melody = 5,
    Common = 6,
    Structure = 7,
    Sections = 8,
    Form = 9,
    Fills = 10,
    Dynamics = 11,
    Percussion = 12,
    DrumStrokes = 13,
    DrumRoles = 14,
    DrumSetup = 15,
    MelodyImprovisation = 16,
    Intro = 17,
    Groove = 18,
    Panning = 19,
    SongForm = 20,
    KeyChange = 22,
    BassFills = 24,
    Meter = 25,
    Unconventionality = 26,
    Feel = 27,
    Parts = 28,
    LineDoubling = 30,
    Solos = 31,
    SoloLines = 32,
    InstrumentsOverTime = 33,
    Expression = 34,
    PartSound = 35,
    SectionKey = 36,
    SectionTempo = 38,
    SectionMeter = 39,
    Polymeter = 40,
    Effects = 41,
    Soloist = 43,
    DrumSolo = 44,
    SoloChords = 45,
    LateEntries = 46,
    LineChords = 47,
    SoloAnswers = 48,
    MelodyFills = 49,
    KeyPreparation = 50,
    SoloMotifs = 51,
    TempoRamps = 52,
    DrumSoloStacks = 53,
    CounterFills = 54,
    MelodyChords = 55,
    LastRepeat = 56,
    InstrumentFamilies = 57,
    TwinTimbres = 58,
    SongEnds = 59,
    DrumCoverage = 60,
    UnheardLandings = 61,
    Kit = 62,
    PartFeel = 63,
    SoloistSolo = 64,
    DrumSoloFeet = 65,
    DrumSoloPlayers = 66,
    GenreLength = 67,
    SectionPhrases = 68,
    HookIntro = 69,
    Strum = 70,
    Drops = 71,
    StopTime = 72,
    Reharmonisation = 73,
    Pushes = 74,
    Legato = 75,
    Roll = 76,
    SectionPedal = 77,
    InnerLines = 78,
    Pins = 79,
    HatRolls = 80,
    Stabs = 81,
    DrumMarks = 82,
    Ornaments = 84,
    Room = 85,
    Insertion = 86,
    DrumJob = 87,
    Figures = 88,
    StrongEffects = 89,
    Tone = 90,
    Tape = 91,
    MixFilter = 92,
    Pinch = 93,
    Mutations = 94,
    DrumSoloOstinato = 95,
    DrumSoloStickings = 96,
    DropKind = 97,
    DrumSoloMotifs = 98,
    SoloTrades = 99,
    OpeningAlone = 100,
    Ghosts = 101,
    SoloTwins = 102,
    PartTracks = 104,
    BlockHarmony = 105,
    DrumOrnaments = 106,
    SectionPhraseBars = 107,
    ShortBars = 108,
    DrumGroup = 109,
    LetRing = 110,
    CallAndResponse = 111,
    SoloAnswerVoicings = 112,
    SoloAnswerMotifs = 113,
    EnsembleHits = 114,
    Darkness = 115,
    Intensity = 116,
    BreakdownChorus = 117,
    CounterRuns = 118,
    FrameArpeggio = 119,
    DeadNotes = 120,
    TempoBand = 121,
    LooseFigures = 122
}

/// <summary>A third of a genre's tempo range (<see cref="SongGenerator.TempoBands" />).</summary>
public enum TempoBand
{
    Slow,
    Middle,
    Fast
}
