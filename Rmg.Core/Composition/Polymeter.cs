using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A section's polymeter: a cycle of a length of its own, in 16ths, that divides no bar, played again and again across
///     the bar lines, as a guitar and a kick run against the hi-hat and the snare, and meeting the bar
///     again where every pattern, or every second at the wilder, starts it afresh. A cycle is a cell, or a cell
///     played again and then a closing one of another length, as 7, 7 and 9 make 23; each cell plays the same motif, cut
///     to its length. The riff plays it, or the bass where the song has no riff, the bass with the riff now and then, and
///     the kick with them, the oftener the wilder, and the snare on its cells' starts against the hi-hat's pulse; each
///     part repeats its own first notes of the cycle's start, or its first note after it on every cell's start where it
///     has none there, so that its line, placed after, follows the chords where they land. By the feel facet of the
///     section's unconventionality:
///     never at the plain end, rarely at the middle, and at the wild end in half the sections, of any length as likely.
/// </summary>
internal static class Polymeter
{
    public static ByConvention Chance { get; } = new(0, 0.01, 0.5);

    // the states that are a chord's shape, which a note takes from its place
    private static readonly IStateKind[] ShapeKinds = [StateKinds.ChordNotePitchOffsets, StateKinds.ChordVoicingFixed];

    /// <summary>
    ///     The figure's length in 16ths, three against four, a dotted 8th's, and a dotted quarter's, the most common and the
    ///     plainest, which a genre playing a polymeter at its plain end plays, and any other wilder.
    /// </summary>
    public static ImmutableArray<(int Sixteenths, ByConvention Weight)> Lengths { get; } =
    [
        (3, new ByConvention(1, 0.3, 1)),
        (6, new ByConvention(1, 0.3, 1)),
        (5, new ByConvention(0, 0.1, 1)),
        (7, new ByConvention(0, 0.1, 1)),
        (9, new ByConvention(0, 0.04, 1)),
        (10, new ByConvention(0, 0.04, 1)),
        (11, new ByConvention(0, 0.03, 1)),
        (13, new ByConvention(0, 0.03, 1)),
        (15, new ByConvention(0, 0.03, 1)),
        (23, new ByConvention(0, 0.03, 1)),
        // a genre's alone, as a band built on long odd cycles plays
        (17, new ByConvention(0, 0, 0))
    ];

    /// <summary>A cell's length as a genre names it, "sixteenths13".</summary>
    public static string NameOf(int sixteenths) => $"sixteenths{sixteenths}";

    /// <summary>
    ///     The chance the bass, with a riff, plays the cycle with it, in unison: always at the plain end, which only a genre
    ///     playing a polymeter there reaches, as a band built on such cycles does, and the wilder, the oftener.
    /// </summary>
    public static ByConvention BassChance { get; } = new(1, 0.75, 1);

    /// <summary>
    ///     The chance the snare, where the kick plays the cycle, strikes every cell's start in place of its own beats, the
    ///     hi-hat keeping the pulse the cycle runs against: the wilder, the oftener.
    /// </summary>
    public static ByConvention SnareChance { get; } = new(0, 0.5, 1);

    /// <summary>
    ///     The chance the kick plays the cycle with the part that leads it, as it would always in a band built on such cycles:
    ///     always at the plain end, which only a genre playing a polymeter there reaches, and the wilder, the oftener.
    /// </summary>
    public static ByConvention KickChance { get; } = new(1, 0.85, 1);

    /// <summary>
    ///     The chance the chords join the cycle, by their rhythm, their voicing kept, leaned to longer cells by
    ///     <see cref="ChordCellOdds" />, as a band's whole unison on a short cell turns to mud: the wilder, the oftener.
    /// </summary>
    public static ByConvention ChordsChance { get; } = new(0, 0.3, 0.6);

    /// <summary>How much likelier the chords join a cycle whose cells are 6 sixteenths longer on average, as the odds of 1, from 6.</summary>
    public const double ChordCellOdds = 3;

    /// <summary>The chance the cycle runs on through the whole section, never starting afresh within it: the wilder, the oftener.</summary>
    public static ByConvention RunsOnChance { get; } = new(0, 0.1, 0.4);

    /// <summary>
    ///     The chance a cycle of one cell grows a sixteenth every time round, 3, 4, 5, up to a sixteenth short of twice its length and from it again,
    ///     its motif cut to each: the wilder, the oftener.
    /// </summary>
    public static ByConvention GrowChance { get; } = new(0, 0.1, 0.3);

    /// <summary>
    ///     How many patterns of four bars the cycle drifts over before it starts afresh on a downbeat, a power of two, and how
    ///     likely: a pattern at the plain end, and the longer drifts, 8, 16 or 32 bars, the wilder, as a band built on such
    ///     cycles lets its riff run against a steady bar until the bar line is lost and the return lands as an arrival.
    /// </summary>
    public static ImmutableArray<(int Patterns, ByConvention Weight)> Frames { get; } =
        [(1, new ByConvention(1, 0.7, 0.3)), (2, new ByConvention(0, 0.3, 0.6)), (4, new ByConvention(0, 0.1, 0.4)), (8, new ByConvention(0, 0.05, 0.2))];

    /// <summary>The chance a cycle closes on a cell of another length, its first played once to three times before it.</summary>
    public static ByConvention TailChance { get; } = new(0, 0.3, 0.7);

    /// <summary>How many times a cycle with a closing cell plays its first before it.</summary>
    public static ImmutableArray<int> Repeats { get; } = [1, 2, 3];

    private const double Sixteenth = 0.25;

    /// <summary>
    ///     A section's polymeter: its cycle's cells' lengths in 16ths, in turn, the parts that play it, and how many patterns
    ///     it runs over before starting afresh; none for a section in its bars.
    /// </summary>
    /// <param name="Patterns">How many patterns the cycle runs over before starting afresh, the whole section's where it runs on.</param>
    /// <param name="Grows">Whether its one cell grows a sixteenth every time round.</param>
    public sealed record Plan(ImmutableArray<int> Cells, ImmutableArray<TrackRole> Parts, bool Kick, int Patterns, bool Snare, bool Grows)
    {
        /// <summary>The cycle's length in 16ths, its first time round.</summary>
        public int Sixteenths => Cells.Sum();

        /// <summary>The length of its cell played the given time from a frame's start, in 16ths.</summary>
        public int CellAt(int time) => Grows ? Cells[0] + time % Cells[0] : Cells[time % Cells.Length];

        /// <summary>Its longest cell, in 16ths, which its motif is taken over.</summary>
        public int Longest => Grows ? 2 * Cells[0] - 1 : Cells.Max();

        /// <summary>The whole section's, for a cycle that runs on through it.</summary>
        public const int Section = int.MaxValue;
    }

    /// <summary>Every section's polymeter, by its id, each drawing from a sequence of its own; none for one it does not play.</summary>
    /// <param name="sections">A section's draws, by its id.</param>
    /// <param name="feel">A section's feel facet, by its id.</param>
    /// <param name="meters">A section's meter, by its id, whose bar the cycle's length must not divide.</param>
    /// <param name="absent">The parts the song leaves out.</param>
    /// <param name="genre">The genre whose chance of a polymeter a section draws (<see cref="GenreChoices.Polymeter" />).</param>
    /// <param name="pinned">The parts the song pins to their figures, which play them every bar and join no cycle.</param>
    public static ImmutableDictionary<int, Plan> Generate(
        Func<int, IGenerationContext> sections,
        Func<int, double> feel,
        IReadOnlyDictionary<int, Meter> meters,
        IEnumerable<int> sectionIds,
        ImmutableHashSet<TrackRole> absent,
        Genre genre,
        ImmutableHashSet<RhythmPart> pinned
    )
    {
        var plans = ImmutableDictionary.CreateBuilder<int, Plan>();
        foreach (var id in sectionIds.Distinct())
        {
            var context = sections(id);
            var unconventionality = feel(id);
            if (!context.TestProbability(Chance.At(unconventionality, genre.Chance(GenreChoices.Polymeter, "sections"))))
                continue;

            var bar = meters[id].Groups.Sum();
            // the lengths as the genre weighs them, where it names them
            var pool = genre.Pool<int>(GenreChoices.PolymeterCell, NameOf);
            var weighed = pool is null ? Lengths : [..Lengths.Select(x => (x.Sixteenths, Weight: pool(x.Sixteenths) ?? new ByConvention(0, 0, 0)))];
            // the lengths that divide no bar, the songs' where the genre names none of them, as a meter of its own may leave
            var lengths = weighed.Where(x => bar % x.Sixteenths != 0 && x.Weight is not { Plain: 0, Tuned: 0, Wild: 0 }).ToArray() is { Length: > 0 } kept
                ? kept
                : [..Lengths.Where(x => bar % x.Sixteenths != 0)];
            // a bar both plain lengths divide, as 3/4's and 6/8's, plays the middle's at the plain end
            var length = context.Pick(ByConvention.WeighFilled(lengths, unconventionality));
            var hasRiff = !absent.Contains(TrackRole.Riff);
            var bassJoins = context.TestProbability(BassChance.At(unconventionality));
            var kick = context.TestProbability(KickChance.At(unconventionality)) && !absent.Contains(TrackRole.Drum);
            ImmutableArray<TrackRole> parts = hasRiff ? bassJoins ? [TrackRole.Riff, TrackRole.Bass] : [TrackRole.Riff] : [TrackRole.Bass];
            // drawn after the rest, so that it stays as it was: the frame, and a closing cell of another length, the cycle as
            // a whole dividing no bar
            var frame = context.Pick(ByConvention.Weigh(Frames, unconventionality));
            var tails = context.TestProbability(TailChance.At(unconventionality));
            var repeats = context.Pick(Repeats.Select(x => new Weighted<int>(1, x)).ToImmutableArray());
            // the lengths the section's end allows, none at the plain end but the plainest
            var closing = weighed.Where(x => x.Sixteenths != length && Drifts(length * repeats + x.Sixteenths, bar) && x.Weight.At(unconventionality) > 0).ToArray();
            var tail = closing.Length > 0 ? context.Pick(ByConvention.Weigh(closing, unconventionality)) : 0;
            ImmutableArray<int> cells = tails && tail > 0 ? [..Enumerable.Repeat(length, repeats), tail] : [length];
            var snare = context.TestProbability(SnareChance.At(unconventionality)) && kick;
            var chords = context.TestProbability(Tilt.Of(ChordCellOdds, (cells.Average() - 6) / 6).Chance(ChordsChance.At(unconventionality), 1)) && !absent.Contains(TrackRole.Chords);
            if (chords)
                parts = [..parts, TrackRole.Chords];
            var runsOn = context.TestProbability(RunsOnChance.At(unconventionality));
            var grows = context.TestProbability(GrowChance.At(unconventionality)) && cells.Length == 1;
            // but a part the song pins, which keeps its figure, drawn for all the same so that the draws are as many
            parts = [..parts.Where(x => GenreRhythm.PartOf(x) is not { } part || !pinned.Contains(part))];
            if (parts.IsEmpty)
                continue;
            var cycleKick = kick && !pinned.Contains(RhythmPart.Ground);
            plans[id] = new Plan(cells, parts, cycleKick, runsOn ? Plan.Section : frame, snare && cycleKick && !pinned.Contains(RhythmPart.Backbeat), grows);
        }

        return plans.ToImmutable();
    }

    /// <summary>Whether a cycle drifts against the bar: neither a whole number of bars nor dividing one.</summary>
    private static bool Drifts(int cycle, int bar) => bar % cycle != 0 && cycle % bar != 0;

    /// <summary>
    ///     The song with every section's polymeter played: each part's notes of every frame, a pattern or two, its first
    ///     cell's played cell by cell from the frame's start to its end, each cut to its cell's length, its own others there
    ///     given up, in the bars it plays; the drums bound to the kick, doubling or accenting it, with it.
    /// </summary>
    /// <param name="bindings">The drums bound to a lead in a section, by its id: their tracks and their leads'.</param>
    /// <param name="stopped">Whether the form stops a track at a position, which no cycle plays into.</param>
    public static TrackEventStateTimelineMap<StateMap> Apply(
        TrackEventStateTimelineMap<StateMap> song,
        IReadOnlyDictionary<int, TrackRole> roles,
        IEnumerable<int> kickTracks,
        IEnumerable<int> snareTracks,
        SongMap map,
        ImmutableDictionary<int, Plan> plans,
        Func<int, IReadOnlyDictionary<int, int>> bindings,
        Func<int, double, bool> stopped
    )
    {
        var trackOf = roles.TrackOf();
        var kicks = kickTracks.ToArray();
        var snares = snareTracks.ToArray();
        // the chord's shape at a place, as the pitched parts' notes there have it, all alike, so that a note played again at
        // another place takes its chord's rather than the one it was struck in: the latest struck at or before it in its
        // chord's span, as a chord held over bars may change its shape at a bar line, or the first after it where none was
        var changes = song.CommonStateTimelineMap.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position).ToArray();
        var shaped = song.TrackTimelineMap.Where(x => roles.TryGetValue(x.Key, out var role) && role != TrackRole.Drum).SelectMany(x => x.Value.EventTimeline)
            .Where(x => !x.Value.GetStateValue(StateKinds.ChordNotePitchOffsets).IsDefaultOrEmpty).OrderBy(x => x.Position).ToArray();
        StateMap? ShapeAt(double position)
        {
            var from = changes.LastOrDefault(x => x <= position + 1e-9);
            var to = changes.FirstOrDefault(x => x > position + 1e-9, double.PositiveInfinity);
            var within = shaped.Where(x => x.IsWithin(from, to)).ToArray();
            if (within.Length == 0)
                return null;
            var before = Array.FindLastIndex(within, x => x.Position <= position + 1e-9);
            return within[Math.Max(before, 0)].Value.Subset(ShapeKinds);
        }

        TimelineItem<StateMap> PlayedAt(StateMap note, double position) =>
            (ShapeAt(position) is { } shape && !note.GetStateValue(StateKinds.ChordNotePitchOffsets).IsDefaultOrEmpty ? note.Except(ShapeKinds).MergeWith(shape) : note).ToTimelineItem(position);

        // the sections as the song plays them, an intro's window and a hook intro as their sources, each cycle from its start
        foreach (var span in map.Plays.Where(x => !x.IsEnding).Select(x => x.Span))
        {
            if (!plans.TryGetValue(span.SectionId, out var plan))
                continue;

            var bound = bindings(span.SectionId).Where(x => kicks.Contains(x.Value)).Select(x => x.Key).ToHashSet();
            var tracks = plan.Parts.Where(trackOf.ContainsKey).Select(x => trackOf[x]).Concat(plan.Kick ? kicks.Concat(bound) : []).Distinct().Where(song.TrackTimelineMap.ContainsKey);
            var longest = plan.Longest * Sixteenth;
            var shortest = plan.Cells.Min() * Sixteenth;
            var frame = plan.Patterns == Plan.Section ? span.Duration : span.Meter.PatternDuration * plan.Patterns;

            // every frame's cells across the section: the frame's start and end, and the cell's start and length
            IEnumerable<(double Start, double End, double At, double Length)> Cells()
            {
                for (var start = span.Start; start < span.End - 1e-9; start += frame)
                {
                    var end = Math.Min(start + frame, span.End);
                    var cell = 0;
                    for (var at = start; at < end - 1e-9; at += plan.CellAt(cell++) * Sixteenth)
                        yield return (start, end, at, plan.CellAt(cell) * Sixteenth);
                }
            }

            int Bar(double position) => span.BarOf(position);
            // the notes before and after the section as they are, and the section's as played
            EventTimeline<StateMap> Within(EventTimeline<StateMap> notes, IEnumerable<TimelineItem<StateMap>> played) =>
                EventTimeline.Create(notes.Duration, notes.RemoveSpan(span.Start, span.End).Concat(played).OrderBy(x => x.Position));

            song = song.MapTrackEvents(tracks.ToDictionary(x => x, track => (Func<EventTimeline<StateMap>, EventTimeline<StateMap>>)(notes =>
            {
                // in the bars the part plays, none where it sits out
                var plays = notes.Where(x => x.IsWithin(span.Start, span.End)).Select(x => Bar(x.Position)).ToHashSet();
                var played = new List<TimelineItem<StateMap>>();
                foreach (var frameCells in Cells().GroupBy(c => (c.Start, c.End)))
                {
                    var (start, end) = frameCells.Key;
                    // a part with no note of its own in the first cell, a bass under a busy riff or one coming in late, starts
                    // every cell with its first note in the frame, so that it plays the cycle rather than nothing, as where its
                    // first note falls past every cell's end; a drum accenting the kick too, on the cell's start, where the
                    // kick's cycle strikes
                    var first = notes.Where(x => x.IsWithin(start, start + longest)).ToArray();
                    if (!first.Any(x => x.Position - start < shortest - 1e-9))
                        first = [..notes.Where(x => x.IsWithin(start, end)).Take(1).Select(x => x.Value.ToTimelineItem(start))];
                    foreach (var (_, _, at, length) in frameCells)
                        played.AddRange(first.Where(x => x.Position - start < length - 1e-9).Select(x => PlayedAt(x.Value, x.Position - start + at))
                            .Where(x => x.Position < end - 1e-9 && plays.Contains(Bar(x.Position)) && !stopped(track, x.Position)));
                }

                return Within(notes, played);
            })));

            // the snare on every cell's start, as its first note there plays, in the bars it plays, and the drums bound to it
            // with it
            var onSnare = bindings(span.SectionId).Where(x => snares.Contains(x.Value)).Select(x => x.Key);
            if (plan.Snare)
                song = song.MapTrackEvents(snares.Concat(onSnare).Distinct().Where(song.TrackTimelineMap.ContainsKey).ToDictionary(x => x, track => (Func<EventTimeline<StateMap>, EventTimeline<StateMap>>)(notes =>
                {
                    var inSpan = notes.Where(x => x.IsWithin(span.Start, span.End)).ToArray();
                    if (inSpan.Length == 0)
                        return notes;

                    var plays = inSpan.Select(x => Bar(x.Position)).ToHashSet();
                    return Within(notes, Cells().Where(c => plays.Contains(Bar(c.At)) && !stopped(track, c.At)).Select(c => PlayedAt(inSpan[0].Value, c.At)));
                })));
        }

        return song;
    }
}
