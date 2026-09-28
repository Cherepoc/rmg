using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     The melody's part of a section: the state its bar patterns' notes carry for <see cref="MelodyLine" /> to place
///     them by, the end of its phrases, and the notes placed once its bars are made.
/// </summary>
internal sealed class MelodyPattern
{
    private readonly double _stepwiseness;

    // the bar pattern's seed, which names its notes for the echoes
    private readonly int _motif;

    /// <param name="barPattern">The bar pattern's state, over the melody track's.</param>
    public MelodyPattern(StateMap barPattern)
    {
        _stepwiseness = barPattern.GetStateValue(CompositionStateKinds.MelodyStepwiseness);
        _motif = barPattern.GetStateValue(CompositionStateKinds.ValueSeed);
    }

    /// <summary>
    ///     A note's state: where it means to go, drawn from the bar pattern's own sequence, so the bar's shape comes back
    ///     with the bar, and the note it plays again (<see cref="GetEcho" />).
    /// </summary>
    public StateMapBuilder AddNoteState(StateMapBuilder builder, KeptBeat beat)
    {
        var stepwiseness = _stepwiseness;
        return builder
            .Add(context =>
                {
                    var (step, turn) = MelodyLayers.GenerateStep(context, stepwiseness);
                    return StateMap.FromStates([CompositionStateKinds.MelodyStep.CreateState(step), CompositionStateKinds.MelodyTurn.CreateState(turn)]);
                }
            )
            .Add(CompositionStateKinds.Echo, GetEcho(_motif, beat));
    }

    /// <summary>
    ///     A section's melody placed, once its bars are made: every note by the rules of <see cref="MelodyLine" />, in
    ///     order over the section's bars, over the chord it plays as <c>Realizer</c> works it out, kept as its scale
    ///     step above the chord's root, so that the section plays it the same wherever it plays. A note that ends a
    ///     phrase lands on the chord as one on a strong beat does. The line starts afresh in every section, on the note
    ///     nearest where its phrase aims, and plays where it was placed.
    /// </summary>
    /// <param name="barStates">The section's state that changes by bar, such as its chords and its phrases' registers.</param>
    /// <param name="key">The song's key.</param>
    public static TrackEventStateTimelineMap<StateMap> Place(
        TrackEventStateTimelineMap<StateMap> bars,
        int trackNumber,
        PitchInstrumentTrack definition,
        StateTimelineMap barStates,
        int key
    )
    {
        var (low, high) = Realizer.GetRange(definition);
        var line = new MelodyLine(low, high);
        var track = bars.TrackTimelineMap[trackNumber];
        var states = Realizer.GetNoteStates(
            track.MergeStateMap(StateMap.FromStates([StateKinds.KeyOffset.CreateState(key)])),
            definition,
            barStates
        );
        var placed = track.EventTimeline.Zip(states).Select(x =>
            {
                var (note, state) = x;
                var (chord, _, classes) = Realizer.GetChordNotes(state.Value);
                var pitch = line.Place(
                    chord,
                    classes,
                    state.Value.GetStateValue(StateKinds.HeldDuration) > 0 ? 0 : state.Value.GetStateValue(CompositionStateKinds.BeatRank),
                    state.Value.GetStateValue(CompositionStateKinds.MelodyStep),
                    state.Value.GetStateValue(CompositionStateKinds.MelodyTurn),
                    state.Value.GetStateValue(CompositionStateKinds.MelodyRegister),
                    state.Value.GetStateValue(CompositionStateKinds.Echo)
                );
                var step = MelodyLine.GetScaleStep(chord, pitch);
                if (chord.GetPitch(step) != pitch)
                    throw new InvalidOperationException($"The melody's note {pitch} is not on a step of its chord's scale.");
                return note.Value.MergeWith(StateMap.FromStates([StateKinds.ScaleStep.CreateState(step)])).ToTimelineItem(note.Position);
            }
        );
        var timeline = EventTimeline.Create(track.EventTimeline.Duration, placed);
        return bars.MapTrackEvents(new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>> { [trackNumber] = _ => timeline });
    }

    /// <summary>
    ///     A section's melody as a question and its answer: its 4 bars, and the answer's, of the same bar patterns, whose
    ///     later bars' rhythm is varied (<see cref="PatternGenerator.GenerateBars" />) and whose notes there are mutated, a
    ///     decision at a time (<see cref="MelodyLayers.AnswerBars" />): a note mutated there draws afresh
    ///     whether it goes on or turns back, and plays no note heard before, so that it is placed by the rules where it
    ///     echoed the question. Whether a note is mutated, and how, is drawn from a sequence of the note's own, by the
    ///     key of the note it echoes, so that notes that echo the same one mutate alike and no other draw moves.
    /// </summary>
    /// <param name="bars">The section's bars, its 4-bar pattern's question and answer.</param>
    /// <param name="seed">The seed of the answer's mutations.</param>
    /// <param name="amount">The chance a note of the answer's later bars is mutated.</param>
    public static TrackEventStateTimelineMap<StateMap> Answer(TrackEventStateTimelineMap<StateMap> bars, int trackNumber, int seed, double amount)
    {
        var track = bars.TrackTimelineMap[trackNumber].EventTimeline;
        var mutated = track.Select(note =>
            {
                var bar = (int)Math.Floor(note.Position / Meter.BarDuration) - Progressions.BarCount;
                var echo = note.Value.GetStateValue(CompositionStateKinds.Echo);
                if (bar < 0 || MelodyLayers.AnswerBars[bar] <= 0)
                    return note;

                var context = new GenerationContext(Seeds.Derive(seed, echo));
                if (!context.TestProbability(amount * MelodyLayers.AnswerBars[bar]))
                    return note;

                var turn = context.GenerateDouble();
                var key = Seeds.Derive(echo, seed);
                return note.Value
                    .Except([CompositionStateKinds.MelodyTurn, CompositionStateKinds.Echo])
                    .MergeWith(StateMap.FromStates([CompositionStateKinds.MelodyTurn.CreateState(turn), CompositionStateKinds.Echo.CreateState(key == 0 ? 1 : key)]))
                    .ToTimelineItem(note.Position);
            }
        );
        var timeline = EventTimeline.Create(track.Duration, mutated);
        return bars.MapTrackEvents(new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>> { [trackNumber] = _ => timeline });
    }

    /// <summary>
    ///     How a melody's answer plays, where it differs from its question: whether every bar draws its rhythm afresh, and
    ///     where the phrase ends in its last bar, as <see cref="CompositionStateKinds.MelodyPhraseEnd" /> has it; none for
    ///     where the question's does. Drawn from the answer's own sequence, by the section's amount: the rhythm now and then
    ///     in its later bars, and the end afresh, other than the question's.
    /// </summary>
    public static MelodyAnswer DrawAnswer(IGenerationContext context, double amount, int questionEnd)
    {
        ImmutableArray<bool> rhythm = [..MelodyLayers.AnswerBars.Select(x => context.TestProbability(x * amount * MelodyLayers.AnswerRhythm))];
        var endsAfresh = context.TestProbability(amount);
        ImmutableArray<Weighted<int>> ends = [..MelodyLayers.PhraseEnds.Where(x => x.Value != questionEnd)];
        var end = ends[Generators.WeightedIndex(ends)(context)].Value;
        return new MelodyAnswer(rhythm, endsAfresh ? end : null);
    }

    /// <summary>
    ///     The key of the note a beat plays again: its bar pattern's, its cycle's draw and its place in the cycle, so
    ///     that a bar pattern that comes back, and a cycle that repeats the one before, play their beats' notes again.
    /// </summary>
    internal static int GetEcho(int barPattern, KeptBeat beat)
    {
        var key = Seeds.Derive(Seeds.Derive(barPattern, beat.Source), beat.Slot);
        return key == 0 ? 1 : key;
    }

    /// <summary>
    ///     A melody's bar with its phrase ended: its notes from the given beat on are left out, and the last one left is
    ///     held until the rest before the next phrase. A bar with no note before the beat keeps its first, if it starts
    ///     before the rest. A phrase end of 0 leaves the bar as it is.
    /// </summary>
    /// <param name="end">The beat, from 1, before which the phrase's last note starts; 0 for none.</param>
    public static EventTimeline<StateMap> EndPhrase(EventTimeline<StateMap> notes, int end)
    {
        if (end <= 0)
            return notes;

        var holdEnd = Meter.BarDuration - MelodyLayers.PhraseEndRest;
        var kept = notes.Where(x => x.Position < end).ToList();
        if (kept.Count == 0 && notes.Count > 0 && notes[0].Position < holdEnd)
            kept.Add(notes[0]);
        if (kept.Count == 0)
            return notes;

        var last = kept[^1];
        kept[^1] = last.Value
            .MergeWith(StateMap.FromStates([StateKinds.HeldDuration.CreateState(holdEnd - last.Position)]))
            .ToTimelineItem(last.Position);
        return EventTimeline.Create(notes.Duration, kept);
    }
}

/// <summary>How a melody's answer differs from its question (<see cref="MelodyPattern.DrawAnswer" />).</summary>
/// <param name="RedrawsRhythm">Whether every bar of the answer draws its rhythm afresh.</param>
/// <param name="PhraseEnd">Where the answer's phrase ends in its last bar; none for where the question's does.</param>
public sealed record MelodyAnswer(ImmutableArray<bool> RedrawsRhythm, int? PhraseEnd);
