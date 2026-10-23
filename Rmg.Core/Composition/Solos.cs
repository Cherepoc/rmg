using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>How several soloists share a solo.</summary>
public enum SoloMode
{
    /// <summary>They take its phrases in turn.</summary>
    Trading,

    /// <summary>They play it together, the others the same line or a similar one a third or a sixth away, all at the one step, some of its notes left out.</summary>
    Together
}

/// <summary>A section's appearance played as a solo: who solos, and how several share it.</summary>
/// <param name="Soloists">The parts that solo; the drums alone for a drum solo, and last where a pitched solo trades with them.</param>
/// <param name="Mode">How several share it.</param>
/// <param name="Step">How many scale steps from the first the others play, playing together.</param>
/// <param name="IsOwn">Whether its line is its own, over the chords, rather than on the melody's.</param>
/// <param name="HitEvery">How many bars apart the band strikes its hits under a drum solo, its melody resting; 0 for none, the band resting.</param>
public sealed record SoloPlan(ImmutableArray<TrackRole> Soloists, SoloMode Mode, int Step, bool IsOwn, int HitEvery)
{
    /// <summary>Whether the drums solo alone, the band resting or striking its hits.</summary>
    public bool IsDrumSolo => Soloists is [TrackRole.Drum];

    /// <summary>Whether a pitched solo trades with the drums, turn by turn, the band laying out for the drums' turns.</summary>
    public bool TradesWithDrums => Soloists.Length > 1 && Soloists.Contains(TrackRole.Drum);

    /// <summary>
    ///     How long a soloist's turn is in a section that trades: its pattern, or half of it in whole bars, a pattern of
    ///     three trading bar by bar, where the section plays it once, a short last bar's cut counted in; and whose turn it is
    ///     at a position, by its place among the soloists.
    /// </summary>
    public static double TurnOf(SectionSpan span) => (int)Math.Round((span.Duration + span.Cut) / span.Meter.PatternDuration) > 1
        ? span.Meter.PatternDuration
        : Math.Max(1, span.Meter.PatternBarCount / 2) * span.Meter.BarDuration;

    public int TurnAt(SectionSpan span, double position) => (int)Math.Floor((position - span.Start) / TurnOf(span) + 1e-9) % Soloists.Length;
}

/// <summary>
///     Solos: a section's later appearance played as a solo now and then, by the form facet: none in the plainest songs,
///     and at the wild end as likely as not, so that a wild song may be solos throughout without having to be. A pitched
///     solo is a line of the solo's own (<see cref="SoloLines" />), on the melody's near the plain end and its own over
///     the chords at the wild, by the melody facet, played by the soloists instead of the melody, the melody resting; several soloists take its phrases in turn or play it together, the others a similar line; the drums solo
///     as <see cref="DrumSolos" /> plans them, gesture by gesture, alone or trading turns with a pitched solo, the last bar
///     a fill. Who solos leans by the melody facet: near the plain end the solo
///     part, a lead of its own, the likeliest, and the melody's instrument, the chords or the riff now and then, and at
///     the wild end any part as likely.
/// </summary>
internal static class Solos
{
    /// <summary>
    ///     The chance a pitched solo trades with the drums, turn by turn, as a jazz band trades fours, by the melody facet:
    ///     never at the plain end, now and then at the wild; a genre's where it names it, Jazz's.
    /// </summary>
    public static ByConvention Trade { get; } = new(0, 0.05, 0.2);

    /// <summary>A pitched solo trading with the drums where the song has them, from a sequence of its own, the solo as it is otherwise.</summary>
    public static SoloPlan? WithTrade(IGenerationContext context, SoloPlan? solo, double melody, IReadOnlyCollection<TrackRole> parts, Genre genre)
    {
        var trades = context.TestProbability(Trade.At(melody, genre.Chance(GenreChoices.Solos, "trades")));
        return trades && solo is { IsDrumSolo: false } && parts.Contains(TrackRole.Drum)
            ? solo with { Soloists = [..solo.Soloists, TrackRole.Drum], Mode = SoloMode.Trading }
            : solo;
    }

    /// <summary>
    ///     The chance a drum solo plays over the band's hits, the band striking its chord together on a bar's first beat,
    ///     every bar or every second, as stop time is played, the drums soloing between, by the groove facet: never at the
    ///     plain end; a genre's where it names it, a rock, a funk or a Latin band's.
    /// </summary>
    public static ByConvention Hits { get; } = new(0, 0.2, 0.3);

    /// <summary>The chance a drum solo's band strikes its hits every second bar rather than every bar.</summary>
    public const double HitsEverySecond = 0.5;

    /// <summary>A drum solo over the band's hits now and then, from a sequence of its own, every draw made whatever the solo; the solo as it is otherwise.</summary>
    public static SoloPlan? WithHits(IGenerationContext context, SoloPlan? solo, double groove, Genre genre)
    {
        var (hits, second) = (context.TestProbability(Hits.At(groove, genre.Chance(GenreChoices.Solos, "hits"))), context.TestProbability(HitsEverySecond));
        return hits && solo is { IsDrumSolo: true } ? solo with { HitEvery = second ? 2 : 1 } : solo;
    }

    /// <summary>
    ///     The chance a pitched solo is played by two parts together a third apart, a twin-guitar break, by the melody facet:
    ///     the songs' never; a genre's where it names it.
    /// </summary>
    public static ByConvention Twin { get; } = new(0, 0, 0);

    /// <summary>The parts that join a soloist as its twin, the first the song has that does not solo already.</summary>
    public static ImmutableArray<TrackRole> TwinParts { get; } = [TrackRole.Riff, TrackRole.Rhythm, TrackRole.Chords];

    /// <summary>
    ///     A pitched solo played by two parts together a third above or below now and then, a second joining a soloist
    ///     alone, from a sequence of its own; the solo as it is otherwise, where it trades with the drums or has no part to
    ///     join it, and where it has several soloists already, whom the solo's share (<see cref="GenreChoices.SoloShare" />)
    ///     sets trading or together.
    /// </summary>
    public static SoloPlan? WithTwin(IGenerationContext context, SoloPlan? solo, double melody, IReadOnlyCollection<TrackRole> parts, Genre genre)
    {
        var (twins, step) = (context.TestProbability(Twin.At(melody, genre.Chance(GenreChoices.Solos, "twins"))), context.Pick([new Weighted<int>(1, 2), new Weighted<int>(1, -2)]));
        if (!twins || solo is null || solo.IsDrumSolo || solo.TradesWithDrums || solo.Soloists.Length >= 2)
            return solo;
        return TwinParts.Where(x => parts.Contains(x) && !solo.Soloists.Contains(x)).Select(x => (TrackRole?)x).FirstOrDefault() is { } second
            ? solo with { Soloists = [..solo.Soloists, second], Mode = SoloMode.Together, Step = step }
            : solo;
    }

    /// <summary>The chance a later appearance is a solo, by the form facet.</summary>
    public static ByConvention Chance { get; } = new(0, 0.035, 0.5);

    /// <summary>
    ///     How a section's role leans it to a solo: a bridge the most, played as a solo in its place, a verse's changes
    ///     after it, a chorus less, which is sung, and a pre-chorus and a post-chorus the least, too short to solo over; a section with no
    ///     role not at all.
    /// </summary>
    public static ImmutableDictionary<SectionRole, double> RoleLeans { get; } = new Dictionary<SectionRole, double>
    {
        [SectionRole.Free] = 0,
        [SectionRole.Verse] = 0.5,
        [SectionRole.PreChorus] = -1,
        [SectionRole.Chorus] = -0.5,
        [SectionRole.Bridge] = 1,
        [SectionRole.Intro] = -1,
        [SectionRole.Outro] = 0.5,
        [SectionRole.PostChorus] = -1
    }.ToImmutableDictionary();

    /// <summary>The odds a role leaning to a solo has, as the odds of 1.</summary>
    public const double RoleOdds = 3;

    /// <summary>A count of soloists as a genre names it: "one", "two" or "three".</summary>
    public static string CountName(int count) => count switch { 1 => "one", 2 => "two", 3 => "three", _ => throw new ArgumentOutOfRangeException(nameof(count), count, null) };

    /// <summary>How many parts solo at once, a genre's pool where it names one, a thrash band's duel of two (<see cref="GenreChoices.SoloCount" />).</summary>
    public static ImmutableArray<(int Count, ByConvention Weight)> Counts { get; } =
    [
        (1, new ByConvention(1, 0.85, 1)),
        (2, new ByConvention(0, 0.12, 1)),
        (3, new ByConvention(0, 0.03, 1))
    ];

    /// <summary>Who solos, and how likely each is.</summary>
    public static ImmutableArray<(TrackRole Part, ByConvention Weight)> Soloists { get; } =
    [
        (TrackRole.Solo, new ByConvention(4, 0.45, 1)),
        (TrackRole.Melody, new ByConvention(1, 0.2, 1)),
        (TrackRole.Chords, new ByConvention(1, 0.13, 1)),
        (TrackRole.Riff, new ByConvention(1, 0.1, 1)),
        (TrackRole.Rhythm, new ByConvention(1, 0.07, 1)),
        (TrackRole.CounterMelody, new ByConvention(0, 0.08, 1)),
        (TrackRole.Bass, new ByConvention(0, 0.05, 1)),
        (TrackRole.Pad, new ByConvention(0, 0.02, 1)),
        (TrackRole.Drum, new ByConvention(0, 0.05, 1))
    ];

    /// <summary>A soloist as a genre's page shows it.</summary>
    public static string LabelOf(TrackRole part) => part switch
    {
        TrackRole.Solo => "The solo part",
        _ => TrackRoles.LabelOf(part)
    };

    public static ImmutableArray<(SoloMode Mode, ByConvention Weight)> Modes { get; } =
    [
        (SoloMode.Trading, new ByConvention(1, 0.6, 1)),
        (SoloMode.Together, new ByConvention(1, 0.4, 1))
    ];

    /// <summary>
    ///     The chance a solo's line is its own rather than on the melody's, by the melody facet: never at the plain end, always
    ///     at the wild; a genre's where it names it, a folk tune's break on its melody the likelier.
    /// </summary>
    public static ByConvention OwnChance { get; } = new(0, 0.5, 1);

    /// <summary>The steps from the first soloist the others play together: the same notes, a third or a sixth away.</summary>
    public static ImmutableArray<int> Steps { get; } = [0, 2, -2, 5, -5];

    /// <summary>The chance a note of a soloist harmonising the first is left out, which tells their lines apart; a unison leaves none out.</summary>
    public const double LeftOutChance = 0.2;

    /// <summary>
    ///     How long a note the others harmonise with the first lasts at least, to its next, in beats: an 8th, a run's
    ///     quick notes left to the first, as a second guitar harmonises a lead's held notes; a unison plays them all.
    /// </summary>
    public const double TogetherShortest = 0.5;

    /// <summary>How much louder a soloist plays than the loudest part under it, on the average of their velocities as rendered.</summary>
    public const double Margin = 0.06;

    /// <summary>
    ///     The song's solos, by its appearances' places, with one of the soloist's where the song has the soloist and the
    ///     melody: a later appearance drawn by how its role leans to a solo, a bridge the likeliest, played by the soloist
    ///     alone, on the melody's line or its own by the melody facet, in place of the solo it drew; as they are where the
    ///     place drawn is a drum solo. Drawn from a sequence of its own, whatever it turns out to be, and by the song's parts
    ///     alone, so that what enters late and the drums leave it as it is.
    /// </summary>
    /// <param name="solos">Every appearance's solo, none for no solo.</param>
    /// <param name="roles">Every appearance's role in the song's form.</param>
    /// <param name="melody">Every appearance's melody facet.</param>
    /// <param name="parts">The parts the song has.</param>
    /// <param name="keepsOut">Whether each appearance plays no solo, a breakdown (<see cref="Breakdowns" />) or a calm section.</param>
    /// <param name="genre">The genre, whose chance of a solo of its own the solo takes where it names it.</param>
    public static ImmutableArray<SoloPlan?> WithSoloist(IGenerationContext context, IReadOnlyList<SoloPlan?> solos, IReadOnlyList<SectionRole> roles, IReadOnlyList<double> melody,
        IReadOnlyCollection<TrackRole> parts, IReadOnlyList<bool> keepsOut, Genre genre)
    {
        var own = context.GenerateDouble();
        // a later appearance by its role's lean, never one that plays no solo
        Weighted<int>[] places = [..Enumerable.Range(1, Math.Max(0, solos.Count - 1)).Where(i => !keepsOut[i]).Select(i => new Weighted<int>(Math.Pow(RoleOdds, RoleLeans[roles[i]]), i))];
        var place = places.Length > 0 ? context.Pick([..places]) : 0;
        if (place == 0 || !parts.Contains(TrackRole.Solo) || !parts.Contains(TrackRole.Melody) || solos[place]?.IsDrumSolo == true)
            return [..solos];

        return [..solos.Select((x, i) => i == place ? new SoloPlan([TrackRole.Solo], SoloMode.Trading, 0, own < OwnChance.At(melody[i], genre.Chance(GenreChoices.Solos, "own")), 0) : x)];
    }

    /// <summary>An appearance's solo, drawn whatever it turns out to be, so that the draws stay as they are; none for no solo.</summary>
    /// <param name="index">The appearance's place among the song's sections; the first is never a solo.</param>
    /// <param name="role">The section's role in the song's form, which leans it to a solo.</param>
    /// <param name="parts">The parts the song has.</param>
    /// <param name="genre">The genre whose chance of a solo and whose soloists the solos take, where it names them.</param>
    public static SoloPlan? Draw(IGenerationContext context, int index, SectionRole role, Unconventionality facets, IReadOnlyCollection<TrackRole> parts, Genre genre)
    {
        var isSolo = context.TestProbability(Tilt.Of(RoleOdds, RoleLeans[role]).Chance(Chance.At(facets[Facet.Form], genre.Chance(GenreChoices.Solos, "sections")), 1));
        var pool = genre.Pool<TrackRole>(GenreChoices.Soloists);
        var melody = facets[Facet.Melody];
        var count = context.Pick(ByConvention.Weigh(Counts, melody, genre.Pool<int>(GenreChoices.SoloCount, CountName)));
        var mode = context.Pick(ByConvention.Weigh(Modes, melody, genre.Pool<SoloMode>(GenreChoices.SoloShare)));
        var step = Steps[context.GenerateInt(0, Steps.Length)];
        // whether the solo is the soloist's own line, drawn before the soloists, whose draws a part held back changes
        var isOwn = context.TestProbability(OwnChance.At(melody, genre.Chance(GenreChoices.Solos, "own")));
        var candidates = Soloists.Where(x => parts.Contains(x.Part)).ToList();
        var soloists = new List<TrackRole>();
        // a part a genre's pool leaves out never solos, and a song with none of its soloists has no solo
        bool MaySolo((TrackRole Part, ByConvention Weight) x) => (pool is null ? x.Weight : pool(x.Part))?.At(melody) > 0;
        for (var i = 0; i < count && candidates.Any(MaySolo); i++)
        {
            var soloist = context.Pick(ByConvention.Weigh(candidates, melody, pool));
            soloists.Add(soloist);
            candidates.RemoveAll(x => x.Part == soloist);
        }

        if (!isSolo || index == 0 || soloists.Count == 0)
            return null;
        // the drums solo where they alone are drawn, and leave the solo to the others where others are
        if (soloists is [TrackRole.Drum])
            return new SoloPlan([TrackRole.Drum], SoloMode.Trading, 0, false, 0);
        soloists.Remove(TrackRole.Drum);
        return parts.Contains(TrackRole.Melody) ? new SoloPlan([..soloists], mode, step, isOwn, 0) : null;
    }

    /// <summary>
    ///     The song with every pitched solo's line (<see cref="SoloLines" />) played by its soloists instead of the melody:
    ///     in turns, no phrase running over its turn's end, or together, the others a step away with some of its notes left
    ///     out, each note a line's, whatever its part plays otherwise, an accompanist that trades back on its own part
    ///     between its turns, and every soloist a margin over the loudest part playing under it.
    /// </summary>
    /// <param name="definitions">Every track's definition, the soloists' ranges.</param>
    /// <param name="soloists">Every part's soloist, whose vocabulary its solos are played in.</param>
    /// <param name="solos">Every solo, by its appearance's place among the song's sections.</param>
    /// <param name="streams">A solo's sequence, by its appearance's place and its soloist's, 0 the first's.</param>
    /// <param name="voicings">A solo's sequence of the gestures its soloist stacks, by its appearance's place and its soloist's.</param>
    /// <param name="motifs">A solo's sequence of the phrases opening on the phrase before's motif, by its appearance's place and its soloist's.</param>
    public static TrackEventStateTimelineMap<StateMap> Apply(
        TrackEventStateTimelineMap<StateMap> song,
        IReadOnlyDictionary<int, TrackRole> roles,
        IReadOnlyDictionary<int, IInstrumentTrack> definitions,
        SongMap map,
        IReadOnlyDictionary<TrackRole, Soloist> soloists,
        IReadOnlyDictionary<int, SoloPlan> solos,
        Func<int, int, IGenerationContext> streams,
        Func<int, int, IGenerationContext> voicings,
        Func<int, int, IGenerationContext> motifs,
        Func<int, int, IGenerationContext> pickups
    )
    {
        var trackOf = roles.TrackOf();
        foreach (var (index, solo) in solos.Where(x => !x.Value.IsDrumSolo).OrderBy(x => x.Key))
        {
            var span = map.Sections[index];
            var context = streams(index, 0);
            bool IsIn(TimelineItem<StateMap> note) => note.IsWithin(span.Start, span.End);
            var melody = song.TrackTimelineMap[trackOf[TrackRole.Melody]].EventTimeline.Where(IsIn).ToArray();
            StateTrace.RecordSection(TracePoints.SoloLine, trackOf[TrackRole.Melody], span.SectionId, $"{melody.Length} notes", (index, melody.Length));
            // a melody with no notes there leaves nothing to solo on, and the parts play as they would
            if (melody.Length == 0)
                continue;

            // a soloist with no notes of its own, such as the solo part, plays over the melody's state, whose line it takes over
            foreach (var soloist in solo.Soloists.Select(x => trackOf[x]).Where(x => !song.TrackTimelineMap.ContainsKey(x)))
                song = TrackEventStateTimelineMap.Create(song.Duration, song.TrackTimelineMap.SetItem(soloist, EventStateTimelineMap.Create(
                    song.Duration, EventTimeline.Create<StateMap>(song.Duration), song.TrackTimelineMap[trackOf[TrackRole.Melody]].StateTimelineMap)), song.CommonStateTimelineMap);

            // the solo's line, in its soloist's vocabulary and range, on the melody only where an appearance before it
            // sang that melody, which the solo is then heard to be of, and its own where none did: the tune as the first such
            // appearance sang it, not as this one would vary it, its notes moved to the solo, each its pitch as sung, moved
            // with the key from there to here, and none past the solo's end
            var first = trackOf[solo.Soloists[0]];
            var singer = song.TrackTimelineMap[trackOf[TrackRole.Melody]];
            bool Sings(SectionSpan x, int j) => x.SectionId == span.SectionId && !solos.ContainsKey(j) && singer.EventTimeline.Any(n => n.IsWithin(x.Start, x.End));
            var sungAt = map.Sections.Take(index).Select((x, j) => (x, j)).Where(x => Sings(x.x, x.j)).Select(x => x.x).FirstOrDefault();
            int Key(double position) => song.CommonStateTimelineMap.GetEffectiveStateMapAt(position).GetStateValue(StateKinds.KeyOffset);
            (double Position, int Pitch)[] tune = sungAt is null ? [] :
            [
                ..singer.EventTimeline.Where(n => n.IsWithin(sungAt.Start, sungAt.End))
                    .Select(n => (Position: n.Position - sungAt.Start + span.Start,
                        Pitch: LinePlacement.PitchOf(Realizer.ChordAt(n.Value, n.Position, singer, definitions[trackOf[TrackRole.Melody]], song.CommonStateTimelineMap), n.Value) + Key(span.Start) - Key(sungAt.Start)))
                    .Where(x => x.Position < span.End - 1e-9)
            ];
            var isOwn = solo.IsOwn || tune.Length == 0;
            var chordsHeld = song.TrackTimelineMap.TryGetValue(trackOf[TrackRole.Chords], out var chords) ? chords.EventTimeline.Where(IsIn).ToArray() : [];
            // where the band strikes, the rhythm part's onsets, or the chords' where it plays none, which a riff strikes on
            var rhythm = trackOf.TryGetValue(TrackRole.Rhythm, out var rhythmTrack) && song.TrackTimelineMap.TryGetValue(rhythmTrack, out var comping) ? comping.EventTimeline.Where(IsIn).Select(x => x.Position).ToArray() : [];
            double[] groove = rhythm.Length > 0 ? rhythm : [..chordsHeld.Select(x => x.Position)];
            var turn = solo.Mode == SoloMode.Trading && solo.Soloists.Length > 1 ? SoloPlan.TurnOf(span) : span.Duration;
            // a soloist's line in its own vocabulary and range, from sequences of its own but the first's, the solo's; the
            // first's traced, as the solo's plan is
            (TimelineItem<StateMap>[] Line, SoloLines.Segment[] Segments) LineOf(TrackRole soloist, int place)
            {
                using var _ = place == 0 ? null : StateTrace.Pause();
                return SoloLines.Generate(place == 0 ? context : streams(index, place), voicings(index, place), motifs(index, place), pickups(index, place), span, turn, span.Start, soloists[soloist], isOwn, isOwn,
                    melody, tune, chordsHeld, groove, song.TrackTimelineMap[trackOf[soloist]], (PitchInstrumentTrack)definitions[trackOf[soloist]], song.CommonStateTimelineMap);
            }
            var (line, segments) = LineOf(solo.Soloists[0], 0);
            StateTrace.RecordSection(TracePoints.SoloGestures, first, span.SectionId, $"{(isOwn ? "own" : "on the melody")}, {segments.Length} segments", (index, isOwn, segments));

            // as loud as the band's loudest part there and a margin over it, the melody resting and the drums apart
            var band = song.TrackTimelineMap
                .Where(x => definitions[x.Key] is PitchInstrumentTrack && x.Key != trackOf[TrackRole.Melody] && !solo.Soloists.Any(s => trackOf[s] == x.Key))
                .Select(x => Loudness(Realizer.GetNoteStates(x.Value, definitions[x.Key], song.CommonStateTimelineMap).Where(IsIn), 0))
                .Where(x => !double.IsNaN(x))
                .DefaultIfEmpty(double.NaN)
                .Max();
            // where no part plays under it, as loud as the melody it takes the place of, and as it is where that rests too
            if (double.IsNaN(band))
                band = Loudness(Realizer.GetNoteStates(song.TrackTimelineMap[trackOf[TrackRole.Melody]], definitions[trackOf[TrackRole.Melody]], song.CommonStateTimelineMap).Where(IsIn), 0) - Margin;
            var moves = new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>>();
            // the drums' turns the drums' own (DrumSolos)
            foreach (var (soloist, place) in solo.Soloists.Select((x, i) => (x, i)).Where(x => x.x != TrackRole.Drum))
            {
                // a part trading its turns, its own line there, as a duel's players each play their own; a part together with
                // the first: a unison doubling the line whole, its runs and its altered notes too, as two players doubling a line
                // play it; a harmony leaving some out and the quick ones to the first, drawn all the same
                TimelineItem<StateMap>[] notes = solo.Mode == SoloMode.Trading
                    ? [..(place == 0 ? line : LineOf(soloist, place).Line).Where(x => solo.TurnAt(span, x.Position) == place)]
                    : place == 0
                        ? line
                        : line.Select((x, i) => (x, Kept: !context.TestProbability(LeftOutChance))).ToArray() is var drawn && solo.Step == 0
                            ? line
                            : [..drawn.Where((x, i) => x.Kept && (i + 1 >= line.Length || line[i + 1].Position - x.x.Position >= TogetherShortest - 1e-9))
                                .Select(x => x.x.Value.With(StateKinds.ScaleStep, x.x.Value.GetStateValue(StateKinds.ScaleStep) + solo.Step).With(StateKinds.Alteration, 0).ToTimelineItem(x.x.Position))];
                // over the band, however loud its part and its draws there, as a pad that solos is not comping any more
                var track = song.TrackTimelineMap[trackOf[soloist]];
                var heard = Realizer.GetNoteStates(
                    EventStateTimelineMap.Create(song.Duration, EventTimeline.Create(song.Duration, notes), track.StateTimelineMap),
                    definitions[trackOf[soloist]],
                    song.CommonStateTimelineMap
                ).ToArray();
                var lift = double.IsNaN(band) ? 0 : Lift(heard, band + Margin);
                TimelineItem<StateMap>[] played =
                [
                    // a soloist that plays no chords playing the line's stacked notes alone
                    ..notes.Select(x => x.Value.With(CompositionStateKinds.LineSolo, 1)
                        .With(CompositionStateKinds.LineVoices, soloists[soloist].ChordRate > 0 ? x.Value.GetStateValue(CompositionStateKinds.LineVoices) : 0)
                        .With(StateKinds.Velocity, x.Value.GetStateValue(StateKinds.Velocity) + lift)
                        .ToTimelineItem(x.Position))
                ];
                // an accompanist that trades going back to its own part between its turns, a lead resting, and the counter-melody
                // too, which a solo otherwise rests to make room
                var comps = solo.Mode == SoloMode.Trading && soloist is not (TrackRole.Melody or TrackRole.Solo or TrackRole.CounterMelody);
                moves[trackOf[soloist]] = own => EventTimeline.Create(own.Duration,
                    own.Where(x => !IsIn(x) || comps && solo.TurnAt(span, x.Position) != place).Concat(played).OrderBy(x => x.Position));
            }

            // the melody rests where it is not a soloist
            if (!solo.Soloists.Contains(TrackRole.Melody))
                moves[trackOf[TrackRole.Melody]] = own => own.RemoveSpan(span.Start, span.End);
            song = song.MapTrackEvents(moves);
        }

        return song;
    }

    /// <summary>The average velocity of notes as rendered, each moved by a lift; none where there are no notes.</summary>
    internal static double Loudness(IEnumerable<TimelineItem<StateMap>> notes, double lift) =>
        notes.Select(x => Render.ToMidiVelocity(x.Value.GetStateValue(StateKinds.Velocity) + lift)).DefaultIfEmpty(double.NaN).Average();

    /// <summary>
    ///     How much the notes' velocities move so that their average as rendered is the loudness aimed at, or as near the
    ///     loudest as a velocity is where it cannot be.
    /// </summary>
    internal static double Lift(IReadOnlyList<TimelineItem<StateMap>> notes, double loudness)
    {
        if (notes.Count == 0)
            return 0;

        // the lift that renders the loudness aimed at, between a lift that renders less and one past any it can need
        var (low, high) = (-LiftReach, LiftReach);
        for (var i = 0; i < LiftSteps; i++)
        {
            var middle = (low + high) / 2;
            if (Loudness(notes, middle) < loudness)
                low = middle;
            else
                high = middle;
        }

        return high;
    }

    private const double LiftReach = 4;

    private const int LiftSteps = 30;
}
