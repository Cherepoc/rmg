using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A line doubled in a section now and then: by another part, note for note, the other part's own notes there giving
///     way: the counter-melody a third or a sixth above or below the melody, as a second voice harmonises a tune, and the
///     bass with the riff, in its own register, as a band plays a riff together. A doubled note keeps its step above its
///     chord's root, moved by the interval, so that it stays in the scale over the same chords; a part plays it from its own
///     first note in the section to its last, and a section where either part rests is not doubled; but under a riff on a
///     driven guitar the bass plays every phrase of a section it plays in, the riff's where it would rest. A twin, the riff's,
///     plays the whole line wherever it plays, in every section, a third above or a sixth below, having no notes of its
///     own; or by its own part an octave apart, the melody's, the counter-melody's or the riff's, the likelier the louder
///     the section, and in a plain song as in a wild one, a part playing one doubling at a time. Neither plainer nor
///     stranger, a doubling by another part does not lean by the song's unconventionality, but the twin's
///     interval, by the song's melody facet: a third or a sixth in a middling song, and the wilder the song, the oftener a
///     fourth, a fifth, a second or a seventh.
/// </summary>
internal static class LineDoubling
{
    /// <summary>Where the octaves' sequences are keyed, by their part, past any pair's.</summary>
    private const int OctavePlaces = 1000;

    /// <summary>
    ///     The steps a twin plays apart from the riff: a third above and a sixth below, the plainest, and a fourth above, a
    ///     fifth below, a second above and a seventh below, the wilder.
    /// </summary>
    public static ImmutableArray<(int Step, ByConvention Weight)> TwinSteps { get; } =
    [
        (2, new ByConvention(1, 0.55, 0.2)),
        (-5, new ByConvention(1, 0.35, 0.2)),
        (3, new ByConvention(0, 0.05, 0.2)),
        (-4, new ByConvention(0, 0.05, 0.2)),
        (1, new ByConvention(0, 0, 0.1)),
        (-6, new ByConvention(0, 0, 0.1))
    ];

    /// <summary>The chance the counter-melody harmonises the melody in a section, a third or a sixth apart.</summary>
    public static ByConvention HarmonyChance { get; } = new(0.25, 0.25, 0.25);

    /// <summary>
    ///     The chance a section's harmonised melody plays in a block, three voices, as a horn section plays a soli: the
    ///     counter-melody's note stacking its chord's under it, from a minor third down, where its note is the chord's, and
    ///     alone where not, as a passing note is (<see cref="CompositionStateKinds.LineVoices" />); a genre's alone.
    /// </summary>
    public static ByConvention BlockChance { get; } = new(0, 0, 0);

    /// <summary>The notes a block's counter-melody stacks, its own and its chord's under it: with the melody's, three voices.</summary>
    private const int BlockVoices = 2;

    /// <summary>The chance the bass plays the riff with it in a section, as a band plays a riff together.</summary>
    public static ByConvention RiffChance { get; } = new(0.35, 0.35, 0.35);

    /// <summary>
    ///     Who doubles whom, by how many scale steps, weighed by the song's melody facet, the chance of a section, alike at
    ///     every end, the option of <see cref="GenreChoices.Doubling" /> it is a genre's by, none for a twin, which doubles
    ///     wherever it plays, and whether the target is the source's twin.
    /// </summary>
    public static ImmutableArray<(TrackRole Source, TrackRole Target, ImmutableArray<(int Step, ByConvention Weight)> Steps, ByConvention Chance, string? Option, bool IsTwin)> Pairs { get; } =
    [
        (TrackRole.Melody, TrackRole.CounterMelody, [(2, Any), (-2, Any), (5, Any), (-5, Any), (3, Never), (-3, Never), (4, Never), (-4, Never)], HarmonyChance, "harmony", false),
        (TrackRole.Riff, TrackRole.Bass, [(0, Any)], RiffChance, "riff", false),
        (TrackRole.Riff, TrackRole.RiffTwin, TwinSteps, Any, null, true)
    ];


    /// <summary>The chance a counter-melody doubling the melody plays it in the melody's sound, twin leads a third apart.</summary>
    public const double TwinTimbreChance = 0.5;

    /// <summary>A step a doubling plays whatever the song's melody facet.</summary>
    private static ByConvention Any => new(1, 1, 1);

    /// <summary>A step the songs' doubling never plays, a genre's alone: the melody harmonised in fourths or fifths, an organum's.</summary>
    private static ByConvention Never => new(0, 0, 0);

    /// <summary>
    ///     The kinds of interval a doubling plays, by how many scale steps apart, as a genre names them
    ///     (<see cref="GenreChoices.HarmonyInterval" />): thirds and sixths, fourths and fifths, and seconds and sevenths, each
    ///     with its inversion.
    /// </summary>
    public static string IntervalOf(int step) => step.Mod(Scales.StepCount) switch
    {
        2 or 5 => "thirds",
        3 or 4 => "fourths",
        1 or 6 => "seconds",
        _ => "unison"
    };

    /// <summary>The parts that play their line in octaves now and then.</summary>
    public static ImmutableArray<TrackRole> OctaveRoles { get; } = [TrackRole.Melody, TrackRole.CounterMelody, TrackRole.Riff];

    /// <summary>
    ///     The chance a part plays a section's line in octaves, by the song's sound facet: a conventional doubling, a plain
    ///     song's too, and a little oftener in a wild one.
    /// </summary>
    public static ByConvention OctaveChance { get; } = new(0.07, 0.11, 0.15);

    /// <summary>How far the section's energy leans the octaves, as a doubled line lifts a loud section.</summary>
    public const double OctaveCoupling = 0.4;

    /// <summary>The song with its lines doubled where each section draws a doubling, from a sequence of the pair's own for each section.</summary>
    /// <param name="streams">A pair's sequence for a section, by the pair's place and the section's.</param>
    /// <param name="melody">The song's melody facet, which the steps lean by.</param>
    /// <param name="sound">The song's sound facet, which the octaves lean by.</param>
    /// <param name="energies">Every appearance's energy, by its place, which the octaves lean by too.</param>
    /// <param name="riffPowerChords">Whether an appearance's riff plays in power chords or chugs, which may make it power chords, by its place, which plays no twin.</param>
    /// <param name="drivenRiff">Whether the riff plays on a driven guitar, whose phrases the bass plays wherever it would rest in them.</param>
    /// <param name="blocks">A section's sequence of whether its harmonised melody plays in a block, by the section's place.</param>
    /// <param name="genre">The genre whose chances of the doublings a section draws (<see cref="GenreChoices.Doubling" />).</param>
    public static TrackEventStateTimelineMap<StateMap> Apply(
        TrackEventStateTimelineMap<StateMap> song,
        IReadOnlyDictionary<int, TrackRole> roles,
        SongMap map,
        double melody,
        double sound,
        IReadOnlyList<double> energies,
        Func<int, int, IGenerationContext> streams,
        Func<int, IGenerationContext> timbres,
        Func<int, IGenerationContext> blocks,
        Func<int, bool> riffPowerChords,
        bool drivenRiff,
        Genre genre
    )
    {
        var trackOf = roles.GroupBy(x => x.Value).ToDictionary(x => x.Key, x => x.First().Key);
        // the parts that double or are doubled in a section, by its place, which play no other doubling there
        var doubling = new HashSet<(int Track, int Section)>();
        foreach (var ((source, target, steps, chance, option, isTwin), pair) in Pairs.Select((x, i) => (x, i)))
        {
            if (!trackOf.TryGetValue(source, out var sourceTrack) || !trackOf.TryGetValue(target, out var targetTrack))
                continue;

            // a twin plays over the source's own state, its chords and its bars', having none of its own
            if (isTwin)
            {
                var twin = EventStateTimelineMap.Create(song.Duration, EventTimeline.Create<StateMap>(song.Duration), song.TrackTimelineMap[sourceTrack].StateTimelineMap);
                song = TrackEventStateTimelineMap.Create(song.Duration, song.TrackTimelineMap.SetItem(targetTrack, twin), song.CommonStateTimelineMap);
            }

            foreach (var (span, index) in map.Sections.Select((x, i) => (x, i)))
            {
                var context = streams(pair, index);
                var isDoubled = context.TestProbability(chance.At(melody, option is null ? null : genre.Chance(GenreChoices.Doubling, option)));
                // the bass with the riff in unison, which no interval of a genre's moves
                var step = context.Pick(ByConvention.Weigh(steps, melody, steps.Any(x => x.Step != 0) ? genre.Pool<int>(GenreChoices.HarmonyInterval, IntervalOf) : null));
                var events = song.TrackTimelineMap;
                bool Plays(int track) => events[track].EventTimeline.Any(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9);
                // a riff in power chords plays no twin, whose third over a distorted fifth is mud
                if (!isDoubled || !Plays(sourceTrack) || !isTwin && !Plays(targetTrack) || isTwin && riffPowerChords(index))
                    continue;

                StateTrace.RecordSection(TracePoints.LineDoubling, targetTrack, span.SectionId, $"{source} {step}", (index, source, target, step));
                doubling.Add((sourceTrack, index));
                doubling.Add((targetTrack, index));
                // where the doubling part plays in the section, from its first note to its last, so that it joins no earlier
                // than it would, as into an intro that brings it in
                var own = events[isTwin ? sourceTrack : targetTrack].EventTimeline.Where(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9).Select(x => x.Position).ToArray();
                var (from, to) = (own.Min(), own.Max());
                // a counter-melody harmonising the melody in the melody's own sound now and then, as twin leads play, and in
                // a block now and then, its notes stacking their chord's
                var inTimbre = source == TrackRole.Melody && !isTwin && timbres(index).TestProbability(TwinTimbreChance);
                var block = source == TrackRole.Melody && !isTwin && blocks(index).TestProbability(BlockChance.At(melody, genre.Chance(GenreChoices.Doubling, "block")));
                if (block)
                    StateTrace.RecordSection(TracePoints.BlockHarmony, targetTrack, span.SectionId, $"{step}", index);
                // in every pattern the doubled part plays, its own kept where a texture rests the doubled part
                var patterns = Enumerable.Range(0, (int)Math.Ceiling(span.Duration / span.Meter.PatternDuration - 1e-9))
                    .Select(k => (Start: span.Start + k * span.Meter.PatternDuration, End: Math.Min(span.End, span.Start + (k + 1) * span.Meter.PatternDuration)))
                    .Where(p => events[sourceTrack].EventTimeline.Any(x => x.Position >= p.Start - 1e-9 && x.Position < p.End - 1e-9))
                    .ToArray();
                bool Doubles(double position) => position >= from - 1e-9 && position <= to + 1e-9 && patterns.Any(p => position >= p.Start - 1e-9 && position < p.End - 1e-9);
                var doubled = events[sourceTrack].EventTimeline
                    .Where(x => Doubles(x.Position))
                    .Select(x => x.Value.With(StateKinds.ScaleStep, x.Value.GetStateValue(StateKinds.ScaleStep) + step).With(StateKinds.Alteration, 0)
                        .With(CompositionStateKinds.SoundsAs, inTimbre ? (int)TrackRole.Melody + 1 : 0).ToTimelineItem(x.Position))
                    .Select(x => block ? x.Value.With(CompositionStateKinds.LineVoices, Math.Max(x.Value.GetStateValue(CompositionStateKinds.LineVoices), BlockVoices)).ToTimelineItem(x.Position) : x);
                song = song.MapTrackEvents(new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>>
                {
                    [targetTrack] = notes => EventTimeline.Create(
                        notes.Duration,
                        notes.Where(x => !Doubles(x.Position)).Concat(doubled).OrderBy(x => x.Position)
                    )
                });
            }
        }

        // and the bass under a driven riff, as a band's plays under its guitars: in a section it plays in, every phrase the
        // riff plays without it it plays with it, in unison, as the pair does; but in the song's opening, which a riff may
        // start alone, and in a section it sits out whole, left to the guitars
        if (drivenRiff && trackOf.TryGetValue(TrackRole.Riff, out var riffTrack) && trackOf.TryGetValue(TrackRole.Bass, out var bassTrack))
            foreach (var (span, index) in map.Sections.Select((x, i) => (x, i)).Skip(1))
            {
                var events = song.TrackTimelineMap;
                TimelineItem<StateMap>[] In(int track, double start, double end) => [..events[track].EventTimeline.Where(x => x.Position >= start - 1e-9 && x.Position < end - 1e-9)];
                if (In(bassTrack, span.Start, span.End).Length == 0)
                    continue;

                var filled = new List<TimelineItem<StateMap>>();
                for (var start = span.Start; start < span.End - 1e-9; start += span.Meter.PatternDuration)
                {
                    var end = Math.Min(start + span.Meter.PatternDuration, span.End);
                    if (In(bassTrack, start, end).Length > 0 || In(riffTrack, start, end) is not { Length: > 0 } riff)
                        continue;

                    StateTrace.RecordSection(TracePoints.RiffBass, bassTrack, span.SectionId, $"{start}", (index, start));
                    filled.AddRange(riff.Select(x => x.Value.With(StateKinds.Alteration, 0).With(CompositionStateKinds.SoundsAs, 0).ToTimelineItem(x.Position)));
                }

                if (filled.Count > 0)
                    song = song.MapTrackEvents(new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>>
                    {
                        [bassTrack] = notes => EventTimeline.Create(notes.Duration, notes.Concat(filled).OrderBy(x => x.Position))
                    });
            }

        // and a line in octaves by its own part, each part and section from a sequence of its own, by the part, apart from
        // the pairs'
        foreach (var (role, place) in OctaveRoles.Select(x => (x, OctavePlaces + (int)x)))
        {
            // a riff harmonised by its twin is doubled already, everywhere
            if (!trackOf.TryGetValue(role, out var track) || role == TrackRole.Riff && trackOf.ContainsKey(TrackRole.RiffTwin))
                continue;

            foreach (var (span, index) in map.Sections.Select((x, i) => (x, i)))
            {
                var context = streams(place, index);
                var inOctaves = context.TestProbability(SectionEnergy.Tilt(energies[index], OctaveCoupling).Chance(OctaveChance.At(sound, genre.Chance(GenreChoices.Doubling, "octaves")), 1));
                bool Plays() => song.TrackTimelineMap[track].EventTimeline.Any(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9);
                if (doubling.Contains((track, index)) || !Plays())
                    continue;

                StateTrace.RecordSection(TracePoints.Octave, track, span.SectionId, $"{role} {inOctaves} {energies[index]:F2}", (index, role, energies[index], inOctaves));
                if (!inOctaves)
                    continue;

                song = song.MapTrackEvents(new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>>
                {
                    [track] = notes => EventTimeline.Create(
                        notes.Duration,
                        notes.Select(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9
                            ? x.Value.With(CompositionStateKinds.Octave, 1).ToTimelineItem(x.Position)
                            : x)
                    )
                });
            }
        }

        return song;
    }
}
