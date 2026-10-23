using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>A song's swing: how late the second of every pair of notes plays, and how long a pair is, both in beats.</summary>
public readonly record struct Swing(double Delay, double Period)
{
    public static Swing None { get; } = new(0, 1);

    /// <summary>
    ///     Where its pairs are counted from: every section's start and the ending's, and whether the span from it swings,
    ///     as its meter's groups pair the swing's notes, so that a section in a meter of its own neither swings across its
    ///     groups nor shifts the pairs of the sections after it; none for pairs counted from the song's start.
    /// </summary>
    public ImmutableArray<(double Start, bool Swings)> Spans { get; init; } = [];

    /// <summary>
    ///     Where a position plays, swung: every pair's first half stretched and its second squeezed, so that its middle
    ///     plays <see cref="Delay" /> late, and everything in it, whatever the grid, moves in proportion. It keeps the order
    ///     of any two positions and leaves the pairs' ends where they are, so that no note crosses another and a bar
    ///     starts on time.
    /// </summary>
    public double Apply(double position)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (Delay == 0)
            return position;

        var (from, swings) = (0.0, true);
        for (var i = Spans.IsDefault ? -1 : Spans.Length - 1; i >= 0; i--)
            if (Spans[i].Start <= position + 1e-9)
            {
                (from, swings) = Spans[i];
                break;
            }

        if (!swings)
            return position;
        var start = from + Math.Floor((position - from) / Period) * Period;
        var within = position - start;
        var half = Period / 2;
        return start + (within < half
            ? within * (half + Delay) / half
            : half + Delay + (within - half) * (half - Delay) / half);
    }
}

/// <summary>
///     How a song's notes sit off the grid: its swing, a song's own, which leans to swinging the less conventional the
///     song's rhythm, as straight time is the convention of most of what it plays. It swings the finest notes long
///     enough to be heard swung: 16ths at slow tempos, 8ths otherwise, and none where the meter's groups do not pair
///     them. Generation decides it, <c>Render</c> plays it,
///     every track alike.
/// </summary>
internal static class Groove
{
    /// <summary>
    ///     The chance a song swings, by the feel facet of its unconventionality: never the plainest, and as often the
    ///     wildest as a middling one, as a swing is a feel, no stranger than straight, which the wild end strays from by
    ///     its meters and tuplets instead.
    /// </summary>
    public static ByConvention SwingChance { get; } = new(0, 0.2, 0.2);

    /// <summary>The lightest swing, as a part of the triplet's; the heaviest is the triplet's, the second note of a pair at two thirds of the pair.</summary>
    public const double LightestSwing = 0.3;

    /// <summary>
    ///     How far a genre leans a swing's amount: the draw from 0 to 1 to the power of e to this times its shift, at the
    ///     most an amount's draw to the power of about 7, light, or of about 1/7, heavy.
    /// </summary>
    public const double AmountLean = 2;

    /// <summary>The shortest note swung, in seconds: 16ths are swung where they are at least this long, 8ths otherwise.</summary>
    public const double ShortestSwungNote = 0.14;

    /// <summary>
    ///     The shortest a swung pair's second note plays, in seconds, at the song's tempo: a drawn swing lighter where it
    ///     would play it shorter, as a drummer's swing evens out the faster the tempo, nearly straight at the fastest; a
    ///     section at a faster tempo of its own plays it a little shorter.
    /// </summary>
    public const double ShortestSecondNote = 0.1;

    /// <summary>
    ///     How far a genre moves the shortest note swung at its most and its least, as a factor: half as long, 16ths swung up
    ///     to 214 beats a minute, as a house track's shuffle, or twice, 8ths swung down to 53 (<see cref="GenreChoices.FeelValues" />).
    /// </summary>
    public const double GenreSwungNoteReach = 2;

    /// <summary>The pair a song swings at its tempo, in beats: 16ths where one lasts long enough to swing, as a genre moves it, 8ths otherwise.</summary>
    public static double SwungPeriod(double tempo, Genre genre) =>
        Meter.SecondsPerBeat(tempo) / 4 >= ShortestSwungNote * Math.Pow(GenreSwungNoteReach, -genre.Shift(GenreChoices.FeelValues, "sixteenths")) ? 0.5 : 1;

    /// <param name="meter">The meter the song's bars are in, whose groups a pair must divide to swing, as 6/8's 8ths do not.</param>
    /// <param name="unconventionality">The feel facet of the song's unconventionality.</param>
    /// <param name="given">How much the song swings where it is given, as a part of the triplet's, 0 for straight.</param>
    /// <param name="genre">The genre the chance and the amount keep to, the amount leaning heavier or lighter.</param>
    public static Swing Generate(IGenerationContext context, double tempo, double unconventionality, Meter meter, double? given, Genre genre)
    {
        // drawn when it is given too, so that the rest is as it was
        var swings = context.TestProbability(SwingChance.At(unconventionality, genre.Chance(GenreChoices.FeelChances, "swing")));
        var amount = LightestSwing + (1 - LightestSwing) * Math.Pow(context.GenerateDouble(), Math.Exp(-AmountLean * genre.Shift(GenreChoices.FeelValues, "swing")));
        var period = SwungPeriod(tempo, genre);
        // no heavier than leaves the pair's second note its shortest, its middle a sixth of the pair late at the triplet's
        amount = Math.Clamp(6 * (period / 2 - ShortestSecondNote / Meter.SecondsPerBeat(tempo)) / period, 0, amount);
        if (given is { } amountGiven)
            (swings, amount) = (amountGiven > 0, amountGiven);

        // a triplet's swing plays the pair's second note a sixth of the pair late of its middle, at two thirds
        return swings && meter.Pairs(period) ? new Swing(amount * period / 6, period) : Swing.None;
    }

    /// <summary>
    ///     The chance a song leaves its time at all, its sections then drawing half or double time, by the groove facet:
    ///     never in the plainest songs, which keep their time, and as often in the wildest as in a middling one, as a song
    ///     keeps its feel and a departure is an event of it (<see cref="GenreChoices.TimeChanges" />).
    /// </summary>
    public static ByConvention DepartChance { get; } = new(0, 0.4, 0.4);

    /// <summary>
    ///     The chance a section of a song that leaves its time plays its drums in half time, at a section of middling
    ///     energy: never in the plainest songs, and as often in the wildest as in a middling one.
    /// </summary>
    public static ByConvention HalfTimeChance { get; } = new(0, 0.15, 0.15);

    /// <summary>The chance a section of a song that leaves its time plays its drums in double time, as half time's, rarer, as it is a busy effect, heard as a scramble more often than a lift.</summary>
    public static ByConvention DoubleTimeChance { get; } = new(0, 0.04, 0.04);

    /// <summary>
    ///     How a section's role leans it to leave the song's time: a bridge the most, as a song breaks into half time or
    ///     gallops there, a chorus less, a nu-metal or a pop chorus's half time, and a verse, which carries the song's
    ///     feel, and a pre-chorus, which builds into the chorus, the least.
    /// </summary>
    public static ImmutableDictionary<SectionRole, double> DepartLeans { get; } = new Dictionary<SectionRole, double>
    {
        [SectionRole.Free] = 0,
        [SectionRole.Verse] = -1,
        [SectionRole.PreChorus] = -1,
        [SectionRole.Chorus] = -0.5,
        [SectionRole.Bridge] = 1,
        [SectionRole.Intro] = 0,
        [SectionRole.Outro] = 0,
        [SectionRole.PostChorus] = 0
    }.ToImmutableDictionary();

    /// <summary>The odds a role leaning to leave the song's time has, as the odds of 1.</summary>
    public const double DepartOdds = 3;

    /// <summary>
    ///     How a section's drums keep time: as a step of every drum's period, 1 for half time, the backbeat on the bar's
    ///     third beat and every cycle twice as long, -1 for double time, the backbeat on every beat, and 0 for the song's
    ///     time; half time the likelier the less energy the section has, double time the more, and neither in the plainest
    ///     songs.
    /// </summary>
    /// <param name="groove">The groove facet of the section's unconventionality, by which a genre's chances lean.</param>
    /// <param name="genre">The genre whose chances of half and double time, at a section of middling energy, they take.</param>
    /// <param name="departs">Whether the song leaves its time at all (<see cref="DepartChance" />).</param>
    /// <param name="role">The section's role, which leans it to leave the song's time (<see cref="DepartLeans" />).</param>
    public static int DrawTimeFeel(IGenerationContext context, Tilt energy, double groove, Genre genre, bool departs, SectionRole role)
    {
        double Within(ByConvention chance, string option) => Tilt.Of(DepartOdds, DepartLeans[role]).Chance(chance.At(groove, genre.Chance(GenreChoices.FeelChances, option)), 1);
        var half = energy.Chance(Within(HalfTimeChance, "halfTime"), -1);
        var twice = energy.Chance(Within(DoubleTimeChance, "doubleTime"), 1);
        // drawn all the same in a song that keeps its time, which none of its sections leaves
        var draw = context.GenerateDouble();
        return !departs ? 0 : draw < half ? 1 : draw < half + twice ? -1 : 0;
    }

    public static StateMap ToStateMap(Swing swing)
    {
        return new StateMapBuilder(Layers.Song, perTrack: false)
            .Add(StateKinds.SwingDelay, swing.Delay)
            .Add(StateKinds.SwingPeriod, swing.Period)
            .ToStateMap();
    }
}
