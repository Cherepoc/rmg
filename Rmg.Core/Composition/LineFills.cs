using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A line filling with the drums into a change of section: where the drums play a run, the line walks too now and
///     then, the likelier into a louder section, on the run's rhythm, a note an 8th apart at the most, by the scale's
///     steps from the note it played before to a step from the note the next section starts on, where it lands: the bass
///     in place of its own notes there, and the melody into its rest, a pickup of a few notes leading into the section,
///     as a singer leads into a chorus. Made once the lines are placed, the walk's notes each kept as a line's are, a
///     scale step above the chord it plays over.
/// </summary>
internal static class LineFills
{
    /// <summary>How a line fills: how likely into a section of as much energy, whether only where it rests, at most how many notes, and how near before its walk its note before is, in bars.</summary>
    public sealed record Way(double Chance, bool IntoRest, int MostNotes, int ReachBars);

    /// <summary>The bass, walking with half the runs in place of its notes, a bar from its note before.</summary>
    public static Way Bass { get; } = new(0.5, false, int.MaxValue, 1);

    /// <summary>The melody, leading into a section with a third of the runs where it rests, three notes at most, two bars from its note before.</summary>
    public static Way Melody { get; } = new(0.3, true, 3, 2);

    /// <summary>The counter-melody, as the melody, where the melody does not.</summary>
    public static Way CounterMelody { get; } = Melody;

    /// <summary>How finely a run's line keys its stream: a 96th of a beat.</summary>
    private const int PlacesPerBeat = 96;

    /// <summary>The shortest a walk's note is, in beats: an 8th.</summary>
    public const double ShortestNote = 0.5;

    /// <param name="streams">A run's own sequence, by its line's place (<see cref="SongMap.PlaceKey" />), so that the runs before it leave it.</param>
    /// <param name="taken">The lines another line walks into, which this one leaves to it.</param>
    /// <returns>The song, and the lines this line walks into.</returns>
    public static (TrackEventStateTimelineMap<StateMap> Song, ImmutableHashSet<double> Walked) Apply(
        TrackEventStateTimelineMap<StateMap> song,
        int lineTrack,
        PitchInstrumentTrack definition,
        IReadOnlyList<PlayedRun> runs,
        Func<long, IGenerationContext> streams,
        SongMap map,
        Way way,
        IReadOnlySet<double> taken
    )
    {
        var track = song.TrackTimelineMap[lineTrack];
        var common = song.CommonStateTimelineMap;
        var (low, high) = Realizer.GetRange(definition);
        var walkedInto = new HashSet<double>();
        var events = track.EventTimeline.ToList();
        foreach (var run in runs)
        {
            if (!streams(map.PlaceKey(run.Line, PlacesPerBeat)).TestProbability(Tilt.Of(SectionEnergy.HighOdds, run.Lift).Chance(way.Chance, 1)))
                continue;

            var positions = Thin(run.Positions).TakeLast(way.MostNotes).ToArray();
            var from = positions[0];
            var before = events.FindLastIndex(x => x.Position < from - 1e-9);
            var after = events.FindIndex(x => x.Position >= run.Line - 1e-9);
            // where the line plays on either side, not into a section it rests in or out of one, and into its own rest
            // where it walks only there
            if (before < 0 || after < 0 || events[after].Position > run.Line + map.MeterAt(run.Line).BarDuration || events[before].Position < from - way.ReachBars * map.MeterAt(from).BarDuration
                || way.IntoRest && events.Any(x => x.IsWithin(from, run.Line)) || taken.Contains(run.Line))
                continue;

            var start = Pitch(events[before]);
            var target = Pitch(events[after]);
            var scale = ScaleNotes(events[before], low, high);
            var walk = Walk(scale, start, target, positions.Length);
            var walked = positions.Zip(walk, (position, pitch) => Place(events[before].Value, position, pitch)).ToArray();
            walkedInto.Add(run.Line);
            StateTrace.RecordSection(TracePoints.LineFill, lineTrack, 0, $"{positions.Length} notes into {run.Line}", (run.Line, positions));
            // a note held into the rest let go where the walk starts, a line singing one note at a time
            var held = events[before].Value.GetStateValue(StateKinds.HeldDuration);
            if (held > from - events[before].Position)
                events[before] = events[before].Value.With(StateKinds.HeldDuration, from - events[before].Position).ToTimelineItem(events[before].Position);
            events.RemoveAll(x => x.IsWithin(from, run.Line));
            events.AddRange(walked);
            events.Sort((x, y) => x.Position.CompareTo(y.Position));
        }

        var filled = EventTimeline.Create(track.EventTimeline.Duration, events);
        return (song.MapTrackEvents(new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>> { [lineTrack] = _ => filled }), [..walkedInto]);

        // the chord a note plays over, as Realizer reads its state there: its own, its track's, its definition's and the song's
        ChordContext Chord(TimelineItem<StateMap> note) => Realizer.ChordAt(note.Value, note.Position, track, definition, common);

        int Pitch(TimelineItem<StateMap> note) => LinePlacement.PitchOf(Chord(note), note.Value);

        // the scale's notes in the bass's range, of the chord the note before plays over
        int[] ScaleNotes(TimelineItem<StateMap> note, int from, int to)
        {
            var chord = Chord(note);
            return [..Enumerable.Range(-4 * Scales.StepCount, 12 * Scales.StepCount).Select(chord.GetPitch).Where(x => x >= from && x <= to).Distinct().Order()];
        }

        // a note of the walk at its place, as a line's note: the step above the chord there nearest the walk's pitch, on the
        // scale there, as a raised step may have changed it; a bass's note plays its step, not the chord's shape
        TimelineItem<StateMap> Place(StateMap before, double position, int pitch)
        {
            var state = before.Except([StateKinds.HeldDuration, CompositionStateKinds.LinePickup, StateKinds.ChordNotePitchOffsets, StateKinds.ChordVoicingFixed]);
            var chord = Chord(state.ToTimelineItem(position));
            return state.With(StateKinds.ScaleStep, LinePlacement.GetScaleStep(chord, pitch)).With(StateKinds.Alteration, 0).With(CompositionStateKinds.Walk, 1).ToTimelineItem(position);
        }
    }

    /// <summary>The run's notes, an 8th apart at the most, from its last back, so that the walk ends on the run's last.</summary>
    private static double[] Thin(IReadOnlyList<double> positions)
    {
        var kept = new List<double>();
        foreach (var position in positions.Reverse())
            if (kept.Count == 0 || kept[^1] - position >= ShortestNote - 1e-9)
                kept.Add(position);
        kept.Reverse();
        return [..kept];
    }

    /// <summary>
    ///     The walk's pitches, as many as asked: by the scale's notes from the one after the start to the one a step from
    ///     the target on the start's side, spread over the walk where it has fewer notes than the way, and held where more.
    /// </summary>
    internal static int[] Walk(IReadOnlyList<int> scale, int start, int target, int count)
    {
        var direction = target >= start ? 1 : -1;
        var approach = direction > 0 ? scale.LastOrDefault(x => x < target, target) : scale.FirstOrDefault(x => x > target, target);
        int[] way = [..scale.Where(x => direction > 0 ? x > start && x <= approach : x < start && x >= approach)];
        if (direction < 0)
            way = [..way.Reverse()];
        if (way.Length == 0)
            way = [approach];

        return [..Enumerable.Range(0, count).Select(k => way[count == 1 ? way.Length - 1 : (int)Math.Round(k * (way.Length - 1) / (double)(count - 1))])];
    }
}
