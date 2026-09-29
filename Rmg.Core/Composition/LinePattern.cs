using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     The melody's part of a section: the state its bar patterns' notes carry for <see cref="Line" /> to place
///     them by, the end of its phrases, and the notes placed once its bars are made.
/// </summary>
internal sealed class LinePattern
{
    private readonly double _stepwiseness;

    // how the line moves, which its notes' steps are drawn by
    private readonly LineProfile _profile;

    // the bar pattern's seed, which names its notes for the echoes
    private readonly int _motif;

    /// <param name="barPattern">The bar pattern's state, over the melody track's.</param>
    /// <param name="profile">How the line moves (<see cref="LineProfile" />).</param>
    public LinePattern(StateMap barPattern, LineProfile profile)
    {
        _profile = profile;
        _stepwiseness = barPattern.GetStateValue(CompositionStateKinds.LineStepwiseness);
        _motif = barPattern.GetStateValue(CompositionStateKinds.ValueSeed);
    }

    /// <summary>
    ///     A note's state: where it means to go, drawn from the bar pattern's own sequence, so the bar's shape comes back
    ///     with the bar, how stepwise its bar is, for a mutation to draw it again, and which note of its figure it is
    ///     (<see cref="GetNoteKey" />).
    /// </summary>
    public StateMapBuilder AddNoteState(StateMapBuilder builder, KeptBeat beat)
    {
        var (stepwiseness, profile) = (_stepwiseness, _profile);
        return builder
            .Add(context =>
                {
                    var (step, turn) = profile.GenerateStep(context, stepwiseness);
                    return StateMap.FromStates(
                        [
                            CompositionStateKinds.LineStep.CreateState(step),
                            CompositionStateKinds.LineTurn.CreateState(turn),
                            CompositionStateKinds.LineStepwiseness.CreateState(stepwiseness)
                        ]
                    );
                }
            )
            .Add(CompositionStateKinds.NoteKey, GetNoteKey(_motif, beat));
    }

    /// <summary>
    ///     A line placed over the song put together: every note by the rules of <see cref="Line" />, in the song's order,
    ///     over the chord it plays as <c>Realizer</c> works it out, the next section's too, kept as its scale step above
    ///     its chord's root; a bar's first note landing where its bar asks (<see cref="CompositionStateKinds.LineLanding" />),
    ///     a phrase starting afresh at its aim or going on from the note before as its first note says (<see cref="CompositionStateKinds.LinePhraseStart" />),
    ///     and the last note before a change of chord on a bar line, its next one of the new chord's, leading into it as
    ///     its bar has it (<see cref="CompositionStateKinds.LineApproach" />), a pickup added for it kept only there
    ///     (<see cref="CompositionStateKinds.LinePickup" />). One line for the whole song, so that it goes
    ///     on from section to section, and a note echoes one heard anywhere before it; the notes the song's form cleared,
    ///     such as an intro's, are not there to place.
    /// </summary>
    public static TrackEventStateTimelineMap<StateMap> Place(
        TrackEventStateTimelineMap<StateMap> song,
        int trackNumber,
        PitchInstrumentTrack definition,
        LineProfile profile
    )
    {
        var (low, high) = Realizer.GetRange(definition);
        var line = new Line(profile, low, high);
        var track = song.TrackTimelineMap[trackNumber];
        var allNotes = LinePlacement.GetNotes(track, definition, song.CommonStateTimelineMap);
        int Bar(LineNote note) => (int)Math.Floor(note.Position / Meter.BarDuration);
        // a pickup stays where the line leads into a new chord: the next note on the bar line, over another chord
        ImmutableArray<LineNote> lineNotes =
        [
            ..allNotes.Where((x, i) =>
                x.State.GetStateValue(CompositionStateKinds.LinePickup) == 0
                || i + 1 < allNotes.Length
                && Math.Abs(allNotes[i + 1].Position - (Bar(x) + 1) * Meter.BarDuration) < 1e-9
                && !allNotes[i + 1].Classes.SetEquals(x.Classes)
            )
        ];
        var notes = lineNotes
            .Select((x, i) =>
                {
                    var isFirstInBar = i == 0 || Bar(lineNotes[i - 1]) != Bar(x);
                    var rank = x.State.GetStateValue(StateKinds.HeldDuration) > 0 ? 0 : x.State.GetStateValue(CompositionStateKinds.BeatRank);
                    var pitch = line.Place(
                        x.Chord,
                        x.Classes,
                        rank,
                        x.State.GetStateValue(CompositionStateKinds.LineStep),
                        x.State.GetStateValue(CompositionStateKinds.LineTurn),
                        x.State.GetStateValue(CompositionStateKinds.LineRegister),
                        x.State.GetStateValue(CompositionStateKinds.NoteKey),
                        isFirstInBar ? (ChordArrival)x.State.GetStateValue(CompositionStateKinds.LineLanding) : ChordArrival.Free,
                        (PhraseStart)x.State.GetStateValue(CompositionStateKinds.LinePhraseStart)
                    );
                    return (Note: x, Rank: rank, Pitch: pitch);
                }
            )
            .ToArray();

        for (var i = 0; i + 1 < notes.Length; i++)
        {
            // a change of chord on a bar line, its first note one of the new chord's, led into as the bar before has it
            var (last, next) = (notes[i], notes[i + 1]);
            var bar = next.Note.Position / Meter.BarDuration;
            var isBarLine = Math.Abs(bar - Math.Round(bar)) < 1e-9 && Bar(last.Note) < Bar(next.Note);
            var approach = (ChordApproach)last.Note.State.GetStateValue(CompositionStateKinds.LineApproach);
            if (!isBarLine || approach == ChordApproach.None || last.Note.Classes.SetEquals(next.Note.Classes) || !next.Note.Classes.Contains(next.Pitch.Mod(12)))
                continue;

            notes[i] = last with { Pitch = line.Approach(approach, last.Note.Chord, last.Note.Classes, last.Rank, last.Pitch, next.Note.Chord, next.Pitch) };
        }

        var placed = LinePlacement.Store(track.EventTimeline.Duration, notes.Select(x => (x.Note, x.Pitch)));
        return song.MapTrackEvents(new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>> { [trackNumber] = _ => placed });
    }

    /// <summary>
    ///     How each bar of the 4-bar pattern leads into the next, the same in the question and in its answer: by a scale
    ///     step, by the section's chance of leading into a chord change within a phrase (<see cref="MelodyLayers.Leading" />),
    ///     or not at all; never into the phrase's first bar, which its last bar's held note ends as a cadence.
    /// </summary>
    public static ImmutableArray<ChordApproach> DrawApproaches(IGenerationContext context, double leading)
    {
        return
        [
            ..Enumerable.Range(0, Progressions.BarCount)
                .Select(bar => context.TestProbability(leading) && bar < Progressions.BarCount - 1 ? ChordApproach.ScaleStep : ChordApproach.None)
        ];
    }

    /// <summary>
    ///     A section's melody as a question and its answer: its 4 bars, and the answer's, of the same bar patterns, whose
    ///     later bars' rhythm is varied (<see cref="PatternGenerator.GenerateBars" />) and whose notes there are mutated
    ///     (<see cref="Mutate" />, <see cref="MelodyLayers.AnswerBars" />), so that they are placed by the rules where
    ///     they echoed the question.
    /// </summary>
    /// <param name="bars">The section's bars, its 4-bar pattern's question and answer.</param>
    /// <param name="seed">The seed of the answer's mutations.</param>
    /// <param name="amount">The chance a note of the answer's later bars is mutated.</param>
    public static TrackEventStateTimelineMap<StateMap> Answer(TrackEventStateTimelineMap<StateMap> bars, int trackNumber, LineProfile profile, int seed, double amount)
    {
        return Mutate(
            bars,
            trackNumber,
            profile,
            seed,
            bar => bar < Progressions.BarCount ? 0 : amount * MelodyLayers.AnswerBars[bar - Progressions.BarCount]
        );
    }

    /// <summary>
    ///     A melody's notes mutated, a decision at a time: a mutated note draws afresh where it means to go, as it was
    ///     drawn (<see cref="LineProfile.GenerateStep" />), and plays no note heard before, so that it is placed by the rules. Whether a note is mutated, and how, is
    ///     drawn from a sequence of the note's own, by its key (<see cref="CompositionStateKinds.NoteKey" />), so that the
    ///     notes of a figure that comes back mutate alike, and take a key of their own, so that they come back alike.
    /// </summary>
    /// <param name="seed">The seed of the mutations.</param>
    /// <param name="chance">The chance a note is mutated, by the bar it is in.</param>
    public static TrackEventStateTimelineMap<StateMap> Mutate(
        TrackEventStateTimelineMap<StateMap> bars,
        int trackNumber,
        LineProfile profile,
        int seed,
        Func<int, double> chance
    )
    {
        var track = bars.TrackTimelineMap[trackNumber].EventTimeline;
        var mutated = track.Select(note =>
            {
                var noteChance = chance((int)Math.Floor(note.Position / Meter.BarDuration));
                var noteKey = note.Value.GetStateValue(CompositionStateKinds.NoteKey);
                if (noteChance <= 0)
                    return note;

                var context = new GenerationContext(Seeds.Derive(seed, noteKey));
                if (!context.TestProbability(noteChance))
                    return note;

                var (step, turn) = profile.GenerateStep(context, note.Value.GetStateValue(CompositionStateKinds.LineStepwiseness));
                var key = Seeds.Derive(noteKey, seed);
                return note.Value
                    .With(CompositionStateKinds.LineStep, step)
                    .With(CompositionStateKinds.LineTurn, turn)
                    .With(CompositionStateKinds.NoteKey, key == 0 ? 1 : key)
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
        var end = context.Pick(ends);
        return new MelodyAnswer(rhythm, endsAfresh ? end : null);
    }

    /// <summary>
    ///     The key of a beat's note in its figure: its bar pattern's, its cycle's draw and its place in the cycle, so
    ///     that a bar pattern that comes back, and a cycle that repeats the one before, play their beats' notes again.
    /// </summary>
    internal static int GetNoteKey(int barPattern, KeptBeat beat)
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
            .With(StateKinds.HeldDuration, holdEnd - last.Position)
            .ToTimelineItem(last.Position);
        return EventTimeline.Create(notes.Duration, kept);
    }
}

/// <summary>
///     A section's line before it is placed: its bars, the question and its answer, and what varies them every time the
///     section plays, so that every appearance brings its own bars for the song to place (<see cref="LinePattern.Place" />).
/// </summary>
/// <param name="Bars">The section's 8 bars, the answer's mutated.</param>
/// <param name="BuildBars">The section's 8 bars built afresh, the answer's mutated, every bar drawing its rhythm afresh by its key, 0 for none.</param>
/// <param name="Letters">Every bar's letter in the section's phrase scheme.</param>
/// <param name="Approaches">How each bar of the 4-bar pattern leads into the next (<see cref="LinePattern.DrawApproaches" />).</param>
/// <param name="Landings">What the first note of each bar of the 4-bar pattern lands on.</param>
/// <param name="RegisterFreedom">The chance a phrase of the line starts afresh at where it aims, rather than going on from the note before.</param>
/// <param name="Seed">The seed of its appearances' draws.</param>
internal sealed record SectionLine(
    int Track,
    LineProfile Profile,
    TrackEventStateTimelineMap<StateMap> Bars,
    Func<ImmutableArray<int>, TrackEventStateTimelineMap<StateMap>> BuildBars,
    ImmutableArray<int> Letters,
    ImmutableArray<ChordApproach> Approaches,
    ImmutableArray<ChordArrival> Landings,
    double RegisterFreedom,
    int Seed
)
{
    // the streams of an appearance's rhythm and of where its phrases start afresh, apart from its notes'
    private const int RhythmStream = 1;
    private const int ResetStream = 2;

    /// <summary>
    ///     The line's bars as the section plays them the given time, from 0, unplaced: the first time as they were made,
    ///     and every later time improvised by the amount given (<see cref="MelodyLayers.Improvisation" />), each appearance
    ///     from the first, not from the one before, so that the section keeps its tune: the bars of a letter draw their
    ///     rhythm afresh, alike, where a phrase varies (<see cref="MelodyLayers.AnswerBars" />), by the amount times
    ///     <see cref="MelodyLayers.ImprovisedRhythm" />, and its notes are mutated by the amount. Every note carries how its
    ///     bar leads out and lands, and a phrase's first note whether it starts afresh, drawn for every appearance.
    /// </summary>
    public TrackEventStateTimelineMap<StateMap> Appear(int appearance, double amount)
    {
        var bars = Bars;
        var seed = Seeds.Derive(Seed, appearance);
        // the line's share of the song's improvisation
        amount *= Profile.ImprovisationShare;
        if (appearance > 0 && amount > 0)
        {
            var context = new GenerationContext(Seeds.Derive(seed, RhythmStream));
            // a draw per letter, which its bars redraw their rhythm below, the likelier where a phrase varies
            var draws = Enumerable.Range(0, Letters.Max() + 1).Select(_ => context.GenerateDouble()).ToArray();
            ImmutableArray<int> rhythmKeys =
            [
                ..Enumerable.Range(0, 2 * Letters.Length).Select(bar =>
                    {
                        var chance = amount * MelodyLayers.ImprovisedRhythm * MelodyLayers.AnswerBars[bar % Letters.Length];
                        return draws[Letters[bar % Letters.Length]] < chance ? appearance : 0;
                    }
                )
            ];
            if (rhythmKeys.Any(x => x != 0))
                bars = BuildBars(rhythmKeys);
            bars = LinePattern.Mutate(bars, Track, Profile, seed, _ => amount);
        }

        // whether each phrase, the question and the answer, starts afresh, drawn for the appearance
        var resetContext = new GenerationContext(Seeds.Derive(seed, ResetStream));
        bool[] resets = [..Enumerable.Range(0, 2).Select(_ => resetContext.TestProbability(RegisterFreedom))];
        var track = bars.TrackTimelineMap[Track].EventTimeline;
        var firsts = Enumerable.Range(0, 2)
            .Select(phrase => track.Where(x => x.Position >= phrase * Meter.PatternDuration).Select(x => (double?)x.Position).FirstOrDefault())
            .ToArray();
        var marked = track.Select(note =>
            {
                var bar = (int)Math.Floor(note.Position / Meter.BarDuration).Mod(Progressions.BarCount);
                var phrase = (int)Math.Floor(note.Position / Meter.PatternDuration);
                var value = note.Value
                    .With(CompositionStateKinds.LineApproach, (int)Approaches[bar])
                    .With(CompositionStateKinds.LineLanding, (int)Landings[bar]);
                var start = resets[phrase] ? PhraseStart.Afresh : PhraseStart.GoesOn;
                return (firsts[phrase] == note.Position ? value.With(CompositionStateKinds.LinePhraseStart, (int)start) : value)
                    .ToTimelineItem(note.Position);
            }
        );
        var timeline = EventTimeline.Create(track.Duration, marked);
        return bars.MapTrackEvents(new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>> { [Track] = _ => timeline });
    }
}

/// <summary>How a melody's answer differs from its question (<see cref="LinePattern.DrawAnswer" />).</summary>
/// <param name="RedrawsRhythm">Whether every bar of the answer draws its rhythm afresh.</param>
/// <param name="PhraseEnd">Where the answer's phrase ends in its last bar; none for where the question's does.</param>
public sealed record MelodyAnswer(ImmutableArray<bool> RedrawsRhythm, int? PhraseEnd);
