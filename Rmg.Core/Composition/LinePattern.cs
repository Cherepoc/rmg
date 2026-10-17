using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A line's part of a section, the melody's, the bass's, the counter-melody's or the riff's: the state its bar patterns' notes carry for <see cref="Line" /> to place
///     them by, the end of its phrases, and the notes placed once its bars are made.
/// </summary>
internal sealed class LinePattern
{
    private readonly double _stepwiseness;

    // how the line moves, which its notes' steps are drawn by
    private readonly LineProfile _profile;

    // the bar pattern's seed, which names its notes for the echoes
    private readonly ulong _motif;

    /// <summary>The stream a chugging note's draw of its pedal derives from its key (<see cref="Line.PedalShare" />).</summary>
    private const int PedalStream = 7;

    /// <summary>The stream a strong beat's note's draw of a tension derives from its key (<see cref="Tensions" />).</summary>
    private const int TensionStream = 8;

    /// <summary>The stream a phrase's held last note's draw of its goal derives from its key (<see cref="Goals" />).</summary>
    private const int GoalStream = 9;

    /// <summary>
    ///     The chance a line's phrase ends on a goal, its held last note on its chord's root or third, as a sung phrase
    ///     closes, rather than wherever its walk lands, by the part: the melody's every phrase at the plain end, most in the
    ///     middle, and none at the wild end, which ends its phrases freely.
    /// </summary>
    public static ImmutableArray<(TrackRole Role, ByConvention Chance)> Goals { get; } =
    [
        (TrackRole.Melody, new ByConvention(1, 0.7, 0))
    ];

    /// <summary>The stream a goal's draw of its root or its third derives from its key (<see cref="GoalRootShare" />).</summary>
    private const int GoalRootStream = 11;

    /// <summary>The stream a strong beat's note's draw of an appoggiatura derives from its key (<see cref="Appoggiaturas" />).</summary>
    private const int AppoggiaturaStream = 12;

    /// <summary>
    ///     The chance a line's note on a strong beat leans on a tension, an appoggiatura, that the next note, on a weaker beat
    ///     over the same chord, resolves by step to one of the chord's, as a singer leans into a word and falls to the chord;
    ///     by the part, the songs' own, never at the plain end and now and then elsewhere, unlike a genre's tension
    ///     (<see cref="Tensions" />), which holds against its chord.
    /// </summary>
    public static ImmutableArray<(TrackRole Role, ByConvention Chance)> Appoggiaturas { get; } =
    [
        (TrackRole.Melody, new ByConvention(0, 0.05, 0.1)),
        (TrackRole.CounterMelody, new ByConvention(0, 0.05, 0.1))
    ];


    /// <summary>How likely a goal is its chord's root rather than its third.</summary>
    public const double GoalRootShare = 0.6;

    /// <summary>
    ///     The chance a line's note on a strong beat lands on a tension, a note of the scale outside its chord, its ninth,
    ///     eleventh or thirteenth, or a mode's flat second, rather than one of the chord's, as a synth over a heavy band
    ///     holds a note against the chord; by the part, the songs' never.
    /// </summary>
    public static ImmutableArray<(TrackRole Role, ByConvention Chance)> Tensions { get; } =
    [
        (TrackRole.Melody, new ByConvention(0, 0, 0)),
        (TrackRole.CounterMelody, new ByConvention(0, 0, 0))
    ];

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
    ///     its chord's root; a chord's first note landing where its chord asks (<see cref="CompositionStateKinds.LineLanding" />),
    ///     a phrase starting afresh at its aim or going on from the note before as its first note says (<see cref="CompositionStateKinds.LinePhraseStart" />),
    ///     and the last note before a change of chord, its next one of the new chord's, leading into it as its chord has
    ///     it (<see cref="CompositionStateKinds.LineApproach" />), a pickup added for it kept only there
    ///     (<see cref="CompositionStateKinds.LinePickup" />). One line for the whole song, so that it goes
    ///     on from section to section, and a note echoes one heard anywhere before it; the notes the song's form cleared,
    ///     such as an intro's, are not there to place.
    /// </summary>
    public static TrackEventStateTimelineMap<StateMap> Place(
        TrackEventStateTimelineMap<StateMap> song,
        int trackNumber,
        PitchInstrumentTrack definition,
        LineProfile profile,
        SongMap map
    )
    {
        var (low, high) = Realizer.GetRange(definition);
        var line = new Line(profile, low, high);
        var track = song.TrackTimelineMap[trackNumber];
        var allNotes = LinePlacement.GetNotes(track, definition, song.CommonStateTimelineMap);
        // where the song's chords change, as its sections have them, and the change a note plays after, -1 before the first
        var changes = song.CommonStateTimelineMap.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position).ToArray();
        int Chord(LineNote note) => Array.FindLastIndex(changes, x => x <= note.Position + 1e-9);
        bool IsOnChange(LineNote note, int chord) => chord >= 0 && Math.Abs(note.Position - changes[chord]) < 1e-9;
        // a pickup stays where the line leads into a new chord: the next note on the next change, over another chord
        ImmutableArray<LineNote> lineNotes =
        [
            ..allNotes.Where((x, i) =>
                x.State.GetStateValue(CompositionStateKinds.LinePickup) == 0
                || i + 1 < allNotes.Length
                && Chord(x) + 1 < changes.Length
                && IsOnChange(allNotes[i + 1], Chord(x) + 1)
                && !allNotes[i + 1].Classes.SetEquals(x.Classes)
            )
        ];
        // a strong beat's note leaning on a tension, where the next note, on a weaker beat over the same chord, resolves it
        var leans = lineNotes.Select((x, i) => i + 1 < lineNotes.Length && Chord(lineNotes[i + 1]) == Chord(x) && Leans(x, lineNotes[i + 1], profile)).ToArray();
        // the notes landing on a tension as they are placed below, a resolution and a lean apart
        var tense = lineNotes.Where((x, i) => !leans[i] && !(i > 0 && leans[i - 1]))
            .Count(x => IsTense(x, x.State.GetStateValue(StateKinds.HeldDuration) > 0 ? 0 : x.State.GetStateValue(CompositionStateKinds.BeatRank), profile));
        StateTrace.RecordSection(TracePoints.LineDevices, trackNumber, 0, $"{leans.Count(x => x)} leaning, {tense} on a tension of {lineNotes.Length}", (leans.Count(x => x), tense, lineNotes.Length));
        var notes = lineNotes
            .Select((x, i) =>
                {
                    var isFirstOfChord = i == 0 || Chord(lineNotes[i - 1]) != Chord(x);
                    var rank = x.State.GetStateValue(StateKinds.HeldDuration) > 0 ? 0 : x.State.GetStateValue(CompositionStateKinds.BeatRank);
                    if (i > 0 && leans[i - 1] && line.Resolve(x.Classes, x.State.GetStateValue(CompositionStateKinds.LineLift)) is { } resolved)
                        return (Note: x, Rank: rank, Pitch: resolved);
                    var pitch = line.Place(
                        x.Chord,
                        IsTense(x, rank, profile) || leans[i] ? TensionsOf(x) : x.Classes,
                        rank,
                        x.State.GetStateValue(CompositionStateKinds.LineStep),
                        x.State.GetStateValue(CompositionStateKinds.LineTurn),
                        x.State.GetStateValue(CompositionStateKinds.LineRegister),
                        x.State.GetStateValue(CompositionStateKinds.NoteKey),
                        // a landing its chord asks for, such as the ending's on its root, over a phrase's goal
                        isFirstOfChord && (ChordArrival)x.State.GetStateValue(CompositionStateKinds.LineLanding) is var landing and not ChordArrival.Free
                            ? landing
                            : GoalOf(x) ?? ChordArrival.Free,
                        (PhraseStart)x.State.GetStateValue(CompositionStateKinds.LinePhraseStart),
                        (LineScale)x.State.GetStateValue(CompositionStateKinds.LineScale),
                        x.State.GetStateValue(CompositionStateKinds.LineLift),
                        // a chugging riff's note's own draw of its pedal, by its key, which draws nothing else
                        x.State.GetStateValue(CompositionStateKinds.LinePedal) > 0 ? Draw(x.State, PedalStream) : 1,
                        x.State.GetStateValue(CompositionStateKinds.LineDrone) > 0
                    );
                    return (Note: x, Rank: rank, Pitch: pitch);
                }
            )
            .ToArray();
        // a note that lands on its chord, as the bass's first of a chord on its root, or a phrase's goal, landing as it does,
        // or resolves a lean, which it keeps resolving
        bool Lands(int i) => GoalOf(lineNotes[i]) is not null
                             || i > 0 && leans[i - 1]
                             || (i == 0 || Chord(lineNotes[i - 1]) != Chord(lineNotes[i]))
                             && (ChordArrival)lineNotes[i].State.GetStateValue(CompositionStateKinds.LineLanding) != ChordArrival.Free;
        Restate(notes, map, profile, Lands);
        Hook(notes, map, profile, Lands);

        for (var i = 0; i + 1 < notes.Length; i++)
        {
            // a change of chord, its first note one of the new chord's, led into as the chord before has it, but from a
            // held note, a phrase's cadence, which rests where it is, and from a lean's resolution
            var (last, next) = (notes[i], notes[i + 1]);
            var nextChord = Chord(next.Note);
            var isChange = IsOnChange(next.Note, nextChord) && Chord(last.Note) < nextChord;
            var approach = (ChordApproach)last.Note.State.GetStateValue(CompositionStateKinds.LineApproach);
            var isHeld = last.Note.State.GetStateValue(StateKinds.HeldDuration) > 0;
            if (!isChange || isHeld || i > 0 && leans[i - 1] || approach == ChordApproach.None || last.Note.Classes.SetEquals(next.Note.Classes) || !next.Note.Classes.Contains(next.Pitch.Mod(12)))
                continue;

            notes[i] = last with { Pitch = line.Approach(approach, last.Note.Chord, last.Note.Classes, last.Rank, last.Pitch, next.Note.Chord, next.Pitch,
                (LineScale)last.Note.State.GetStateValue(CompositionStateKinds.LineScale), last.Note.State.GetStateValue(CompositionStateKinds.LineLift)) };
        }

        var loudness = PhraseArc(notes.Select(x => (x.Note.State, x.Pitch)).ToArray(), profile.PhraseArc);
        var placed = LinePlacement.Store(track.EventTimeline.Duration, notes.Select((x, i) => (
            loudness[i] == 0 ? x.Note : x.Note with { Note = x.Note.Note.Value.With(StateKinds.Velocity, x.Note.Note.Value.GetStateValue(StateKinds.Velocity) + loudness[i]).ToTimelineItem(x.Note.Position) },
            x.Pitch)));
        return song.MapTrackEvents(new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>> { [trackNumber] = _ => placed });
    }

    /// <summary>
    ///     A long pattern's middle phrases, between the first, which states, and the last, which answers
    ///     (<see cref="Answers" />), singing the first's notes again, each where the first played it, where it fits the
    ///     chord there, one of its notes on a strong beat and of its scale on another, and the line's own where it does not,
    ///     so that a blues' second line restates its first over the IV, its notes the same, as a singer's are; but a note
    ///     that lands on its chord, which keeps its landing, as a bass on the IV's root.
    /// </summary>
    /// <param name="lands">Whether a note lands on its chord, as its first asks (<see cref="CompositionStateKinds.LineLanding" />).</param>
    private static void Restate((LineNote Note, int Rank, int Pitch)[] notes, SongMap map, LineProfile profile, Func<int, bool> lands)
    {
        var at = notes.Select((x, i) => (Position: Math.Round(x.Note.Position, 6), i)).GroupBy(x => x.Position).ToDictionary(x => x.Key, x => x.First().i);

        foreach (var span in map.Sections.Where(x => x.Meter.Phrases > 2))
        {
            var phrase = span.Meter.PhraseDuration;
            foreach (var start in span.Patterns.Select(x => x.Start))
            for (var restating = 1; restating < span.Meter.Phrases - 1; restating++)
                for (var i = 0; i < notes.Length; i++)
                {
                    var position = notes[i].Note.Position;
                    if (position < start + restating * phrase - 1e-9 || position >= start + (restating + 1) * phrase - 1e-9 || lands(i))
                        continue;
                    if (at.TryGetValue(Math.Round(position - restating * phrase, 6), out var first) && Fits(notes[i], notes[first].Pitch, profile))
                        notes[i] = notes[i] with { Pitch = notes[first].Pitch };
                }
        }
    }

    /// <summary>
    ///     A hooked section's repeated bars singing their letter's first bar's notes, each where the first played it in
    ///     its pattern and the same note of its figure, where it fits the chord there as a restated phrase's does (<see cref="Restate" />), and a landing
    ///     as it lands: a chorus's figure coming back as a tune, and a riff's as a figure, where
    ///     a bar's figure otherwise comes back placed anew over its chord.
    /// </summary>
    private static void Hook((LineNote Note, int Rank, int Pitch)[] notes, SongMap map, LineProfile profile, Func<int, bool> lands)
    {
        var at = notes.Select((x, i) => (Position: Math.Round(x.Note.Position, 6), i)).GroupBy(x => x.Position).ToDictionary(x => x.Key, x => x.First().i);
        // every pattern's first bar of each letter, by the pattern's start
        var firsts = new Dictionary<(double Pattern, int Letter), int>();
        for (var i = 0; i < notes.Length; i++)
        {
            var letter = notes[i].Note.State.GetStateValue(CompositionStateKinds.LineHookBar);
            if (letter == 0 || map.SectionAt(notes[i].Note.Position) is not { } span)
                continue;
            var position = notes[i].Note.Position;
            var pattern = span.Start + span.PatternOf(position) * span.Meter.PatternDuration;
            var bar = map.PlaceOf(position).PatternBar;
            if (!firsts.TryGetValue((pattern, letter), out var first))
            {
                firsts[(pattern, letter)] = bar;
                continue;
            }
            if (bar == first || lands(i))
                continue;
            // the same note of the figure, so that a riff whose cycle is not its bar's, a polymeter's, keeps to its cycle
            if (at.TryGetValue(Math.Round(position - (bar - first) * span.Meter.BarDuration, 6), out var source)
                && notes[source].Note.State.GetStateValue(CompositionStateKinds.NoteKey) == notes[i].Note.State.GetStateValue(CompositionStateKinds.NoteKey)
                && FitsHook(notes[i], notes[source].Pitch))
                notes[i] = notes[i] with { Pitch = notes[source].Pitch };
        }
    }

    /// <summary>
    ///     A phrase's held last note's goal, its chord's root or third, where it ends on one, by its own draws, from its key,
    ///     which draw nothing else; none for another note, or a phrase ending freely.
    /// </summary>
    private static ChordArrival? GoalOf(LineNote note)
    {
        if (note.State.GetStateValue(StateKinds.HeldDuration) <= 0 || !Drawn(note, CompositionStateKinds.LineGoal, GoalStream))
            return null;
        // its third where its chord has one, a suspended chord's root
        return Draw(note.State, GoalRootStream) < GoalRootShare || !note.Classes.Contains(note.Chord.GetPitch(2).Mod(12)) ? ChordArrival.Root : ChordArrival.Third;
    }

    /// <summary>Whether a note on a strong beat lands on a tension, by its own draw.</summary>
    private static bool IsTense(LineNote note, int rank, LineProfile profile) =>
        rank <= profile.StrongestWeakRank && TensionsOf(note).Count > 0 && Drawn(note, CompositionStateKinds.LineTension, TensionStream);

    /// <summary>Whether a note on a strong beat, not a phrase's held last, leans on a tension the next note resolves, on a weaker beat, by its own draw.</summary>
    private static bool Leans(LineNote note, LineNote next, LineProfile profile) =>
        note.State.GetStateValue(StateKinds.HeldDuration) <= 0
        && note.State.GetStateValue(CompositionStateKinds.BeatRank) is var rank && rank <= profile.StrongestWeakRank
        && next.State.GetStateValue(StateKinds.HeldDuration) <= 0 && next.State.GetStateValue(CompositionStateKinds.BeatRank) > rank
        && TensionsOf(note).Count > 0 && Drawn(note, CompositionStateKinds.LineAppoggiatura, AppoggiaturaStream);

    /// <summary>Whether a note plays a line's device, where it has the device's chance, by its own draw.</summary>
    private static bool Drawn(LineNote note, StateKind<double> kind, int stream) => note.State.GetStateValue(kind) is var chance and > 0 && Draw(note.State, stream) < chance;

    /// <summary>A note's own draw of one of its decisions, from its key, which draws nothing else.</summary>
    private static double Draw(StateMap note, int stream) => new GenerationContext(Seeds.Derive((ulong)note.GetStateValue(CompositionStateKinds.NoteKey), stream)).GenerateDouble();

    /// <summary>A note's tensions: its scale's notes outside its chord, as pitch classes.</summary>
    private static IReadOnlySet<int> TensionsOf(LineNote note) =>
        Enumerable.Range(0, Scales.StepCount).Select(x => note.Chord.GetPitch(x).Mod(12)).Where(x => !note.Classes.Contains(x)).ToHashSet();

    /// <summary>
    ///     Whether a hook's note fits the chord its repeat plays over: of its scale, and, on any beat, no note of the chord a
    ///     semitone from it, so that a strong beat's note becomes the chord's ninth, eleventh or sus note rather than a clash,
    ///     as a hook repeated over its loop's chords sings.
    /// </summary>
    private static bool FitsHook((LineNote Note, int Rank, int Pitch) note, int pitch) =>
        Enumerable.Range(0, Scales.StepCount).Any(x => note.Note.Chord.GetPitch(x).Mod(12) == pitch.Mod(12))
        && !note.Note.Classes.Any(x => (x - pitch).Mod(12) is 1 or 11);

    /// <summary>Whether a pitch fits a note's chord where it plays: one of its notes on a strong beat, of its scale on another.</summary>
    private static bool Fits((LineNote Note, int Rank, int Pitch) note, int pitch, LineProfile profile)
    {
        var isStrong = note.Rank <= profile.StrongestWeakRank && note.Note.Classes.Count > 0;
        return isStrong
            ? note.Note.Classes.Contains(pitch.Mod(12))
            : Enumerable.Range(0, Scales.StepCount).Any(x => note.Note.Chord.GetPitch(x).Mod(12) == pitch.Mod(12));
    }

    /// <summary>
    ///     How much louder each note of a line plays for its height in its phrase, a phrase ending on its held note or where
    ///     the next starts: by the arc given from its lowest note, half of it softer, to its highest, half of it louder, as a
    ///     player leans into a phrase's peak and lets its close fall away; none in a phrase of one pitch.
    /// </summary>
    internal static double[] PhraseArc(IReadOnlyList<(StateMap State, int Pitch)> notes, double arc)
    {
        var phrases = new int[notes.Count];
        for (var i = 1; i < notes.Count; i++)
            phrases[i] = phrases[i - 1] + (notes[i - 1].State.GetStateValue(StateKinds.HeldDuration) > 0
                                           || notes[i].State.GetStateValue(CompositionStateKinds.LinePhraseStart) != (int)PhraseStart.None ? 1 : 0);
        return PhraseArc(phrases, [..notes.Select(x => x.Pitch)], arc);
    }

    /// <summary>How much louder each note plays for its height in its phrase, as <see cref="PhraseArc(IReadOnlyList{ValueTuple{StateMap, int}}, double)" />, the phrases given.</summary>
    /// <param name="phrases">The phrase each note is of, by its number.</param>
    internal static double[] PhraseArc(IReadOnlyList<int> phrases, IReadOnlyList<int> pitches, double arc)
    {
        var loudness = new double[pitches.Count];
        if (arc == 0)
            return loudness;
        foreach (var phrase in Enumerable.Range(0, pitches.Count).GroupBy(i => phrases[i]))
        {
            var (low, high) = (phrase.Min(i => pitches[i]), phrase.Max(i => pitches[i]));
            if (high > low)
                foreach (var i in phrase)
                    loudness[i] = arc * ((pitches[i] - low) / (double)(high - low) - 0.5);
        }

        return loudness;
    }

    /// <summary>
    ///     How each bar of the pattern leads into the next, the same in the question and in its answer: by a scale
    ///     step, by the section's chance of leading into a chord change (<see cref="MelodyLayers.Leading" />), or not at
    ///     all; into the next phrase too, where the phrase runs on into it rather than ending on a held note.
    /// </summary>
    public static ImmutableArray<ChordApproach> DrawApproaches(IGenerationContext context, double leading, int chordCount)
    {
        return [..Enumerable.Range(0, chordCount).Select(_ => context.TestProbability(leading) ? ChordApproach.ScaleStep : ChordApproach.None)];
    }

    /// <summary>
    ///     A section's line as a question and its answer: its pattern's bars, and the answer's, of the same bar patterns, whose
    ///     later bars' rhythm is varied (<see cref="PatternGenerator.GenerateBars" />) and whose notes there are mutated
    ///     (<see cref="Mutate" />, <see cref="MelodyLayers.AnswerShare" />), so that they are placed by the rules where
    ///     they echoed the question.
    /// </summary>
    /// <param name="bars">The section's bars, its pattern's question and answer.</param>
    /// <param name="seed">The seed of the answer's mutations.</param>
    /// <param name="amount">The chance a note of the answer's later bars is mutated.</param>
    /// <param name="meter">The section's meter, whose pattern the question is, and every phrase of the answer after it.</param>
    public static TrackEventStateTimelineMap<StateMap> Answer(TrackEventStateTimelineMap<StateMap> bars, int trackNumber, LineProfile profile, ulong seed, double amount, Meter meter)
    {
        return Mutate(
            bars,
            trackNumber,
            profile,
            seed,
            meter.BarDuration,
            bar => Answers(bar, meter) ? amount * MelodyLayers.AnswerShare(bar, meter.PhraseBars) : 0
        );
    }

    /// <summary>
    ///     Whether a bar of a line answers its question: every bar of the pattern's second play, and, where the pattern is
    ///     several phrases, its last phrase, which answers those before it, as the blues' third line answers its first two.
    /// </summary>
    public static bool Answers(int bar, Meter meter)
    {
        return bar >= meter.PatternBarCount || meter.Phrases > 1 && bar >= meter.PatternBarCount - meter.PhraseBars;
    }

    /// <summary>
    ///     A line's notes mutated, a decision at a time: a mutated note draws afresh where it means to go, as it was
    ///     drawn (<see cref="LineProfile.GenerateStep" />), and plays no note heard before, so that it is placed by the rules. Whether a note is mutated, and how, is
    ///     drawn from a sequence of the note's own, by its key (<see cref="CompositionStateKinds.NoteKey" />), so that the
    ///     notes of a figure that comes back mutate alike, and take a key of their own, so that they come back alike.
    /// </summary>
    /// <param name="seed">The seed of the mutations.</param>
    /// <param name="barDuration">How long a bar is, in beats.</param>
    /// <param name="chance">The chance a note is mutated, by the bar it is in.</param>
    public static TrackEventStateTimelineMap<StateMap> Mutate(
        TrackEventStateTimelineMap<StateMap> bars,
        int trackNumber,
        LineProfile profile,
        ulong seed,
        double barDuration,
        Func<int, double> chance
    )
    {
        var track = bars.TrackTimelineMap[trackNumber].EventTimeline;
        var mutated = track.Select(note =>
            {
                var noteChance = chance((int)Math.Floor(note.Position / barDuration));
                var noteKey = note.Value.GetStateValue(CompositionStateKinds.NoteKey);
                if (noteChance <= 0)
                    return note;

                var context = new GenerationContext(Seeds.Derive(seed, noteKey));
                if (!context.TestProbability(noteChance))
                    return note;

                var (step, turn) = profile.GenerateStep(context, note.Value.GetStateValue(CompositionStateKinds.LineStepwiseness));
                var key = Seeds.Key(noteKey, unchecked((long)seed));
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
    /// <param name="phraseBars">How many bars the answer's phrase has.</param>
    public static MelodyAnswer DrawAnswer(IGenerationContext context, double amount, int questionEnd, int phraseBars)
    {
        ImmutableArray<bool> rhythm = [..Enumerable.Range(0, phraseBars).Select(bar => context.TestProbability(MelodyLayers.AnswerShare(bar, phraseBars) * amount * MelodyLayers.AnswerRhythm))];
        var endsAfresh = context.TestProbability(amount);
        ImmutableArray<Weighted<int>> ends = [..MelodyLayers.PhraseEnds.Where(x => x.Value != questionEnd)];
        var end = context.Pick(ends);
        return new MelodyAnswer(rhythm, endsAfresh ? end : null);
    }

    /// <summary>
    ///     The key of a beat's note in its figure: its bar pattern's, its cycle's draw and its place in the cycle, so
    ///     that a bar pattern that comes back, and a cycle that repeats the one before, play their beats' notes again.
    /// </summary>
    internal static long GetNoteKey(ulong barPattern, KeptBeat beat)
    {
        var key = Seeds.Key(Seeds.Key(unchecked((long)barPattern), beat.Source), beat.Slot);
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

        // the bar's timeline lasts the bar
        var holdEnd = notes.Duration - MelodyLayers.PhraseEndRest;
        // in a short bar, the end before the rest whatever beat was drawn
        var kept = notes.Where(x => x.Position < Math.Min(end, holdEnd)).ToList();
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
/// <param name="Bars">The section's pattern's bars twice, the question's and the answer's, the answer's mutated.</param>
/// <param name="BuildBars">Those bars built afresh, the answer's mutated, every bar drawing its rhythm afresh by its key, 0 for none.</param>
/// <param name="Letters">Every bar's letter in the section's phrase scheme.</param>
/// <param name="HarmonicRhythm">Where the section's chords change.</param>
/// <param name="Approaches">How each chord of the pattern leads into the next (<see cref="LinePattern.DrawApproaches" />).</param>
/// <param name="Landings">What the first note of each chord of the pattern lands on.</param>
/// <param name="RegisterFreedom">The chance a phrase of the line starts afresh at where it aims, rather than going on from the note before.</param>
/// <param name="Seed">The seed of its appearances' draws.</param>
internal sealed record SectionLine(
    int Track,
    LineProfile Profile,
    TrackEventStateTimelineMap<StateMap> Bars,
    Func<ImmutableArray<long>, TrackEventStateTimelineMap<StateMap>> BuildBars,
    ImmutableArray<int> Letters,
    HarmonicRhythm HarmonicRhythm,
    ImmutableArray<ChordApproach> Approaches,
    ImmutableArray<ChordArrival> Landings,
    double RegisterFreedom,
    ulong Seed,
    bool Hooks
)
{
    // the streams of an appearance's rhythm and of where its phrases start afresh, apart from its notes'
    private const int RhythmStream = 1;
    private const int ResetStream = 2;

    /// <summary>
    ///     The line's bars as the section plays them the given time, from 0, unplaced: the first time as they were made,
    ///     and every later time improvised by the amount given (<see cref="MelodyLayers.Improvisation" />), each appearance
    ///     from the first, not from the one before, so that the section keeps its tune: the bars of a letter draw their
    ///     rhythm afresh, alike, where a phrase varies (<see cref="MelodyLayers.AnswerShare" />), by the amount times
    ///     <see cref="MelodyLayers.ImprovisedRhythm" />, and its notes are mutated by the amount. Every note carries how its
    ///     chord leads out and lands, and a phrase's first note whether it starts afresh, drawn for every appearance.
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
            ImmutableArray<long> rhythmKeys =
            [
                ..Enumerable.Range(0, 2 * Letters.Length).Select(bar =>
                    {
                        var chance = amount * MelodyLayers.ImprovisedRhythm * MelodyLayers.AnswerShare(bar, HarmonicRhythm.Meter.PhraseBars);
                        return draws[Letters[bar % Letters.Length]] < chance ? appearance : 0;
                    }
                )
            ];
            if (rhythmKeys.Any(x => x != 0))
                bars = BuildBars(rhythmKeys);
            bars = LinePattern.Mutate(bars, Track, Profile, seed, HarmonicRhythm.Meter.BarDuration, _ => amount);
        }

        // whether each phrase, the question and the answer, starts afresh, drawn for the appearance
        var resetContext = new GenerationContext(Seeds.Derive(seed, ResetStream));
        bool[] resets = [..Enumerable.Range(0, 2).Select(_ => resetContext.TestProbability(RegisterFreedom))];
        var track = bars.TrackTimelineMap[Track].EventTimeline;
        var firsts = Enumerable.Range(0, 2)
            .Select(phrase => track.Where(x => x.Position >= phrase * HarmonicRhythm.Meter.PatternDuration).Select(x => (double?)x.Position).FirstOrDefault())
            .ToArray();
        var marked = track.Select(note =>
            {
                var chord = HarmonicRhythm.IndexAt(note.Position);
                var phrase = (int)Math.Floor(note.Position / HarmonicRhythm.Meter.PatternDuration);
                var value = note.Value
                    .With(CompositionStateKinds.LineApproach, (int)Approaches[chord])
                    .With(CompositionStateKinds.LineLanding, (int)Landings[chord]);
                // a hooked section's bar carries its letter, for its repeats to sing its first's notes (Hook)
                if (Hooks)
                    value = value.With(CompositionStateKinds.LineHookBar, Letters[(int)Math.Floor(note.Position / HarmonicRhythm.Meter.BarDuration + 1e-9) % Letters.Length] + 1);
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
