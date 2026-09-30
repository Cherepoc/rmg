using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     The bass filling with the drums into a change of section: where the drums play a run, the bass walks too now and
///     then, the likelier into a louder section, on the run's rhythm, a note an 8th apart at the most, by the scale's
///     steps from the note it played before to a step from the note the next section starts on, where it lands. Made
///     once the lines are placed, the walk's notes each kept as a line's are, a scale step above the chord it plays over.
/// </summary>
internal static class BassFills
{
    /// <summary>The chance the bass walks with a run into a section of as much energy as the one before.</summary>
    public const double Chance = 0.5;

    /// <summary>The shortest a walk's note is, in beats: an 8th.</summary>
    public const double ShortestNote = 0.5;

    public static TrackEventStateTimelineMap<StateMap> Apply(
        TrackEventStateTimelineMap<StateMap> song,
        int bassTrack,
        PitchInstrumentTrack definition,
        IReadOnlyList<PlayedRun> runs,
        IGenerationContext context
    )
    {
        var track = song.TrackTimelineMap[bassTrack];
        var common = song.CommonStateTimelineMap;
        var (low, high) = Realizer.GetRange(definition);
        var events = track.EventTimeline.ToList();
        foreach (var run in runs)
        {
            // drawn for every run, so that the draws are as many whatever the chance
            if (!context.TestProbability(Tilt.Of(SectionEnergy.HighOdds, run.Lift).Chance(Chance, 1)))
                continue;

            var positions = Thin(run.Positions);
            var from = positions[0];
            var before = events.FindLastIndex(x => x.Position < from - 1e-9);
            var after = events.FindIndex(x => x.Position >= run.Line - 1e-9);
            // where the bass plays on either side, not into a section it rests in or out of one
            if (before < 0 || after < 0 || events[after].Position > run.Line + Meter.BarDuration || events[before].Position < from - Meter.BarDuration)
                continue;

            var start = Pitch(events[before]);
            var target = Pitch(events[after]);
            var scale = ScaleNotes(events[before], low, high);
            var walk = Walk(scale, start, target, positions.Length);
            var walked = positions.Zip(walk, (position, pitch) => Place(events[before].Value, position, pitch)).ToArray();
            events.RemoveAll(x => x.Position >= from - 1e-9 && x.Position < run.Line - 1e-9);
            events.AddRange(walked);
            events.Sort((x, y) => x.Position.CompareTo(y.Position));
        }

        var filled = EventTimeline.Create(track.EventTimeline.Duration, events);
        return song.MapTrackEvents(new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>> { [bassTrack] = _ => filled });

        // the chord a note plays over, as Realizer reads its state there: its own, its track's, its definition's and the song's
        ChordContext Chord(TimelineItem<StateMap> note) => Realizer.GetChord(StateMap.Aggregate([
            note.Value,
            track.GetEffectiveStateMapAt(note.Position),
            definition.StateMap.OfScope(StateScope.Render),
            common.GetEffectiveStateMapAt(note.Position)
        ])).Chord;

        int Pitch(TimelineItem<StateMap> note) =>
            Chord(note).GetPitch(note.Value.GetStateValue(StateKinds.ScaleStep)) + note.Value.GetStateValue(StateKinds.Alteration);

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
            return state.With(StateKinds.ScaleStep, LinePlacement.GetScaleStep(chord, pitch)).With(StateKinds.Alteration, 0).ToTimelineItem(position);
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
