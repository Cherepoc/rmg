using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>The notes' articulations, two stages of the song after it is put together (<see cref="SongStages" />): chosen as state (<see cref="Choose" />), and played.</summary>
internal static partial class Articulations
{
    /// <summary>The chance a song's chords are struck short, stabs, a genre's that the songs never play, and how long a stab sounds, in beats: a 16th.</summary>
    public static ByConvention StabChance { get; } = new(0, 0, 0);

    public const double StabLength = 0.25;

    /// <summary>The chance a part on a piano rolls its held chords, by the sound facet: never in the plainest songs.</summary>
    public static ByConvention RollChance { get; } = new(0, 0.3, 0.5);

    /// <summary>The chance a part's broken chords are let ring to the chord's change, a genre's that the songs never play.</summary>
    public static ByConvention LetRingChance { get; } = new(0, 0, 0);

    /// <summary>
    ///     The chance a bass, a riff or a rhythm part mutes the 16ths of its chains that fall between the 8ths, dead notes,
    ///     played short and soft as a funk player's percussive ghosts: a genre's that the songs never play.
    /// </summary>
    public static ByConvention DeadNoteChance { get; } = new(0, 0, 0);

    /// <summary>
    ///     The chance a song's soloist plays ghosts in its licks (<see cref="Soloist.Ghosts" />), by its sound facet: never at the
    ///     plain end, now and then past it, a genre's where it names it, a funk band's the more.
    /// </summary>
    public static ByConvention SoloGhostChance { get; } = new(0, 0.15, 0.3);

    /// <summary>
    ///     The chance an appearance's riff, rhythm part or lead on a plucked instrument tremolo-picks its notes, a genre's that
    ///     the songs never play: each note two strokes long or more re-struck, down and up, at a picker's speed (<see cref="Tremolo" />).
    /// </summary>
    public static ByConvention TremoloChance { get; } = new(0, 0, 0);

    /// <summary>
    ///     How an appearance's energy leans its tremolo, as far as its section's rhythm follows the energy: its chance through
    ///     its odds, and its step, the finest leaning this far and the coarsest as far the other way, so that a line heard
    ///     quietly is picked again, and faster, as it comes back louder.
    /// </summary>
    public const double TremoloLean = 1;

    /// <summary>
    ///     The chance a song's tremolo carries a picked line through its rests, a genre's that the songs never play: its notes
    ///     held each to the next where it picks, as a black metal guitarist's picking runs on as one wave (<see cref="PartArticulation.Holds" />).
    /// </summary>
    public static ByConvention TremoloThroughChance { get; } = new(0, 0, 0);

    /// <summary>How many strokes a note is picked in at the fewest: two, as a note struck once is the line's own.</summary>
    public const int TremoloStrokes = 2;

    /// <summary>How many strokes a second a tremolo picker strikes at the most, as fast as the hand plays evenly.</summary>
    public const double FastStrokes = 16;

    /// <summary>
    ///     The steps a tremolo picks at, in beats, from the slowest: the 8th, at a tempo too fast for 16ths, the 16th, a
    ///     measured picker's finest, and, a fast one's, the sextuplet and the 32nd.
    /// </summary>
    public static ImmutableArray<double> TremoloSteps { get; } = [0.5, 0.25, 1 / 6.0, 0.125];

    /// <summary>How many of the steps a measured picker plays, the 8th and the 16th; a fast one plays the rest from the 16th.</summary>
    private const int MeasuredSteps = 2;

    /// <summary>The chance a part plays its whole chords cycled at a sound chip's frame rate, a genre's that the songs never play.</summary>
    public static ByConvention FrameChance { get; } = new(0, 0, 0);

    /// <summary>A console's frame, in seconds, a sixtieth, the step a frame-rate arpeggio's notes are held in whole frames of.</summary>
    public const double Frame = 1 / 60.0;

    /// <summary>How long a frame-rate arpeggio holds each of a chord's notes at the least, in seconds: two frames.</summary>
    public const double FrameSeconds = 2 * Frame;

    /// <summary>
    ///     How many times a frame-rate arpeggio cycles through a chord at the most: a held chord's notes slowed to fit, in
    ///     whole frames, as a long chord cycled at two frames a note is heard as a buzz rather than a chip's chord.
    /// </summary>
    public const int FrameCycles = 3;

    private const double Sixteenth = 0.25;

    /// <summary>The guitars whose dead notes sound on General MIDI's muted guitar, a jazz guitar and a clean one.</summary>
    private static readonly int[] MutedProgram = [26, 27];

    /// <summary>General MIDI's muted guitar.</summary>
    private const int MutedGuitar = 28;

    /// <summary>What a ringing note keeps out of: stop time and a part stopped by the form, whose silence it would fill.</summary>
    private const Hold RingsOutOf = Hold.Stop | Hold.Cut;

    /// <summary>What a tremolo keeps out of: stop time and ensemble hits, whose hits strike with the kick and rest between.</summary>
    internal const Hold PicksOutOf = Hold.Stop;

    /// <summary>The quickest a broken chord's notes follow each other and are let ring, in beats: an 8th, as faster ones would blur.</summary>
    public const double RingsFrom = 0.5;

    /// <summary>How long a piano's rolled chord takes from its lowest note to its highest, in seconds: slower than a strum.</summary>
    public const double Roll = 0.06;

    /// <summary>How long a guitar's stroke takes across its strings at its quickest and its slowest, in seconds.</summary>
    public const double StrokeFrom = 0.01, StrokeTo = 0.03;

    /// <summary>How a note's part plays it, by its state.</summary>
    private static ArticulationChoice ChoiceOf(RealizedNote note) => note.State.GetStateValue(CompositionStateKinds.Articulation).Value;

    /// <summary>Whether a note's part plays it with an articulation, by its state.</summary>
    private static bool Has(RealizedNote note, PartArticulation articulation) => ChoiceOf(note).Articulates.HasFlag(articulation);

    /// <summary>
    ///     The notes' articulations: in a section that chugs, a guitar's or a picked bass's note that repeats the one before
    ///     on a weak beat palm-muted, its accents and its moves ringing, as a chug riff mutes its eighths on the root and
    ///     rings its accent; the riff's, the rhythm part's, the chords' and the bass's, by their instruments, as a pop punk guitar mutes its verse's strums. And every chord a guitar
    ///     plays strummed, down and up in turn on the steps of its part's chords, a 16th at the finest, as quickly as the
    ///     song strums, and a piano's held
    ///     chord rolled upward where its part rolls, and the chords struck short where they stab; and a bass in
    ///     octaves, its notes off the beat the note before an octave away, or alternating its chord's root and fifth.
    /// </summary>
    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var notes = SongStages.NotesOf(draft);
        var stroke = draft.Timeline.CommonStateTimelineMap.GetEffectiveStateMapAt(0).GetStateValue(CompositionStateKinds.Stroke);
        var tempo = draft.Timeline.CommonStateTimelineMap.GetStateTimeline(StateKinds.Tempo);
        var result = notes.ToBuilder();
        foreach (var (track, timeline) in notes)
        {
            if (song.Tracks.Definitions[track] is not PitchInstrumentTrack part)
                continue;
            var (bottom, top) = Realizer.GetRange(part);
            // every strong note's place among the strong notes of its chord in its section, from 0, a weak note's -1
            var strongPlaces = new int[timeline.Count];
            for (var (k, last) = (0, -1); k < timeline.Count; k++)
            {
                strongPlaces[k] = -1;
                if (timeline[k].Value.State.GetStateValue(CompositionStateKinds.BeatRank) > 0)
                    continue;
                var sameChord = last >= 0 && timeline[last].Value.State.GetStateValue(StateKinds.ChordChange) == timeline[k].Value.State.GetStateValue(StateKinds.ChordChange)
                                && song.Form.Map.PlayingAt(timeline[last].Position) == song.Form.Map.PlayingAt(timeline[k].Position);
                strongPlaces[k] = sameChord ? strongPlaces[last] + 1 : 0;
                last = k;
            }

            // whether each note was played an octave from the note before, which the next of a run of weak notes turns back
            // to, and the pitches each was played at
            var octaved = new bool[timeline.Count];
            var played = new ImmutableArray<int>[timeline.Count];
            // the strumming grid, the shortest step between the part's chords, a 16th at the finest, a strum down on its
            // even steps and up on its odd; its own chords', not a walk's into a change, which its few quick chords would
            // set the whole song's hand by
            var strummed = timeline.Where(x => x.Value.Pitches.Length > 1 && x.Value.State.GetStateValue(CompositionStateKinds.Walk) == 0).Select(x => x.Position).ToArray();
            var grid = Math.Max(0.25, strummed.Zip(strummed.Skip(1), (a, b) => b - a).Where(x => x > 1e-6).DefaultIfEmpty(0.5).Min());

            result[track] = EventTimeline.Create(timeline.Duration, timeline.Select((note, i) =>
            {
                var state = note.Value.State;
                var (rolls, deadNotes, stabs) = (Has(note.Value, PartArticulation.Rolls), Has(note.Value, PartArticulation.DeadNotes), Has(note.Value, PartArticulation.Stabs));
                // the appearance it plays as, an intro's and the ending's too
                var section = song.Form.Map.PlayingAt(note.Position)?.Index ?? -1;
                // a repeat of the note before as it is played, a bass cycle's step or an octave taken
                var repeats = i > 0 && played[i - 1].SequenceEqual(note.Value.Pitches);
                var weak = state.GetStateValue(CompositionStateKinds.BeatRank) > 0;
                // by the instrument playing the note, a switched one's
                var program = InstrumentsOverTime.ProgramOf(state, part.InstrumentCode);
                // a riff chugging on its pedal muting every note off its accents, another part its repeats, its own line's
                // alone, a lead line it solos playing as it is
                var onPedal = state.GetStateValue(CompositionStateKinds.LinePedal) > 0;
                var ownLine = state.GetStateValue(CompositionStateKinds.LineSolo) == 0;
                if (Chugs(part.Role, program) && ownLine && section >= 0 && song.Sections[section].Chugs && (repeats || onPedal) && weak)
                    state = state.With(CompositionStateKinds.PalmMute, 1);
                // a dead note: a 16th between the 8ths with a note a 16th before or after it, of the part's own line
                // between the 8ths of its bar, counted from the bar's start, as a bar of an odd number of 16ths moves them
                var between = Math.Abs((song.Form.Map.PlaceOf(note.Position).Beat + Sixteenth) % (2 * Sixteenth)) < 1e-6;
                var chained = i > 0 && Math.Abs(note.Position - timeline[i - 1].Position - Sixteenth) < 1e-6
                              || i + 1 < timeline.Count && Math.Abs(timeline[i + 1].Position - note.Position - Sixteenth) < 1e-6;
                // on a clean or a jazz guitar its muted sound, General MIDI's muted guitar, as a funk player scratches a
                // ghost between open chords on the one guitar
                if (deadNotes && ownLine && between && chained)
                    state = MutedProgram.Contains(program)
                        ? state.With(CompositionStateKinds.PalmMute, 1).With(CompositionStateKinds.Program, MutedGuitar + 1)
                        : state.With(CompositionStateKinds.PalmMute, 1);
                // a bass in octaves, a note off the beat the note before it an octave away, up where its range has it and down
                // where not, as a disco or a funk bass alternates them; but one leading into the next chord, and a section that
                // chugs, which mutes its repeats instead
                var pitches = note.Value.Pitches;
                var leads = i + 1 < timeline.Count && timeline[i + 1].Value.State.GetStateValue(StateKinds.ChordChange) != note.Value.State.GetStateValue(StateKinds.ChordChange);
                // a weak note after one played an octave away turning back to the note it was taken from, so that a run
                // alternates; but a note the bass plays doubling another's line, which plays the line's, a walk's, which moves
                // to the next chord, and one after a note of another chord, whose octave is not this chord's
                var doubled = state.GetStateValue(CompositionStateKinds.Doubled) > 0;
                var walks = state.GetStateValue(CompositionStateKinds.Walk) > 0;
                var sameChordBefore = i > 0 && timeline[i - 1].Value.State.GetStateValue(StateKinds.ChordChange) == state.GetStateValue(StateKinds.ChordChange)
                                      && song.Form.Map.PlayingAt(timeline[i - 1].Position)?.Index == section;
                if (part.Role == TrackRole.Bass && ownLine && !doubled && !walks && sameChordBefore && section >= 0 && song.Sections[section].OctaveBass && !song.Sections[section].Chugs && weak && !leads
                    && played[i - 1] is var before && (octaved[i - 1] || before.All(x => x + 12 <= top) || before.All(x => x - 12 >= bottom)))
                {
                    pitches = octaved[i - 1] ? played[i - 2 < 0 ? 0 : i - 2] : [..before.Select(x => before.All(y => y + 12 <= top) ? x + 12 : x - 12)];
                    octaved[i] = !octaved[i - 1];
                }
                // a bass on a cycle of its chord's steps, its strong notes after the first taking them by turns while the chord
                // lasts, each nearest the strong note before, below where as near, in its range: the root and the fifth below
                // it, as a country bass plays, or a boogie climbing and coming back; but where it holds a pedal
                var cycle = section >= 0 ? song.Sections[section].BassCycle : [];
                if (part.Role == TrackRole.Bass && ownLine && !doubled && !cycle.IsEmpty && strongPlaces[i] > 0 && cycle[strongPlaces[i] % cycle.Length] is var step and not 0
                    && pitches.Length == 1 && state.GetStateValue(CompositionStateKinds.BassPedal) == 0)
                {
                    // its sixth and seventh a boogie's over the root, the major sixth and the flat seventh, whatever the
                    // chord's scale has there, and the chord's own root, third and fifth, as the band's chord holds them: a sus
                    // chord's fourth or second in place of the third it leaves out, a power chord's fifth, as a shuffle walks
                    // root, fifth and sixth over one; the scale's where no chord sounds, its root alone, or the chord leaves out its root
                    var (chord, _, classes) = Realizer.GetChordNotes(note.Value.State);
                    var root = chord.GetPitch(0);
                    int[] offsets = step switch { 2 => [4, 3, 5, 2, 7, 0], 4 => [7, 6, 8, 0], _ => [] };
                    var target = step switch
                    {
                        5 => root + 9,
                        6 => root + 10,
                        2 or 4 when classes.Count > 1 => offsets.Where(x => classes.Contains((root + x).Mod(12))).Select(x => root + x).DefaultIfEmpty(chord.GetPitch(step)).First(),
                        _ => chord.GetPitch(step)
                    };
                    var last = played[Array.LastIndexOf(strongPlaces, strongPlaces[i] - 1, i)].Min();
                    var under = last - (last - target).Mod(12);
                    var nearest = last - under <= under + 12 - last ? under : under + 12;
                    pitches = [nearest >= bottom && nearest <= top ? nearest : nearest == under && under + 12 <= top ? under + 12 : nearest - 12 >= bottom ? nearest - 12 : pitches[0]];
                }
                // the stroke in beats at the tempo there
                double Beats(double seconds) => seconds * Meter.BeatsPerSecond(tempo.GetEffectiveValueAt(note.Position));
                if (note.Value.Pitches.Length > 1 && InstrumentTraits.Has(program, InstrumentTrait.Strums))
                    // down on the grid's even steps and up on its odd, as the hand swings
                    state = state.With(CompositionStateKinds.Strum, (long)Math.Floor(song.Form.Map.BeatInBar(note.Position) / grid + 1e-9) % 2 == 0 ? Beats(stroke) : -Beats(stroke));
                // a piano's chord held a beat or longer rolled upward, slower, where its part rolls, but where it stabs, which
                // holds no chord that long
                else if (rolls && !stabs && note.Value.Pitches.Length >= 3 && note.Value.Duration >= 1 - 1e-9 && InstrumentTraits.Has(program, InstrumentTrait.Rolls))
                    state = state.With(CompositionStateKinds.Strum, Beats(Roll));

                // but the ending's final chord, held where the ending holds it, as the band holds it
                var ending = song.Form.Map.Ending;
                var duration = stabs && !(FormLayers.HoldsFinalChord(ending.Kind) && note.Position >= ending.Start - 1e-9) ? Math.Min(note.Value.Duration, StabLength) : note.Value.Duration;
                played[i] = pitches;
                return state == note.Value.State && pitches == note.Value.Pitches && duration == note.Value.Duration
                    ? note
                    : (note.Value with { State = state, Pitches = pitches, Duration = duration }).ToTimelineItem(note.Position);
            }));
        }

        // and a line's steps slurred where its part slurs, but the bass's, which plays a line it doubles struck
        foreach (var (track, timeline) in result.ToArray())
            if (song.Tracks.Definitions[track].Role != TrackRole.Bass && timeline.Any(x => Has(x.Value, PartArticulation.Slurs)))
                result[track] = Slur(timeline);

        // and a part's broken chords let ring where it lets them
        var changes = draft.Timeline.CommonStateTimelineMap.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position).ToArray();
        // but never into the silence of stop time or a stop the form makes, which a ringing note would fill
        var holds = draft.Holds(song);
        foreach (var (track, timeline) in result.ToArray())
            if (song.Tracks.Definitions[track] is PitchInstrumentTrack { } chords && chords.Role.PlaysChords())
                result[track] = LetRing(timeline, chords.InstrumentCode, changes, song.Form.Map, (note, position) => Has(note, PartArticulation.LetsRing) && !holds.At(RingsOutOf, position, track),
                    position => holds.NextAfter(RingsOutOf, position, track));

        // the first 8th of its bar at or after a position, a short bar's end its next bar's first
        double EighthFrom(double position)
        {
            var place = song.Form.Map.PlaceOf(position);
            var (start, eighth) = (position - place.Beat, Math.Ceiling(place.Beat / RollStep - 1e-9) * RollStep);
            return Math.Min(start + eighth, start + place.Meter.BarDuration);
        }
        // where the phrase a position plays in ends, none outside the sections
        double? PhraseEnd(double position) => song.Form.Map.PlayingAt(position)?.Span.Phrases.Where(p => position < p.End - 1e-9).Select(p => (double?)p.End).FirstOrDefault();
        // and a line held through its rests where it holds them, never into stop time or a stop the form makes
        foreach (var (track, timeline) in result.ToArray())
            if (timeline.Any(x => Has(x.Value, PartArticulation.Holds)))
                result[track] = Held(timeline, note => Has(note, PartArticulation.Holds), changes, PhraseEnd, position => holds.NextAfter(RingsOutOf, position, track));

        // and a part's notes tremolo-picked where it picks them, until the end of the appearance a note starts in, but a
        // hit of stop time's
        foreach (var (track, timeline) in result.ToArray())
            if (song.Tracks.Definitions[track] is PitchInstrumentTrack line && timeline.Any(x => ChoiceOf(x.Value).TremoloStep > 0))
                result[track] = Tremolo(timeline, line.InstrumentCode, position => holds.At(PicksOutOf, position, track) ? null : song.Form.Map.PlayingAt(position)?.Span.End,
                    position => Meter.BeatsPerSecond(tempo.GetEffectiveValueAt(position)), stroke);

        // and a part's rolled chords rolled in 8ths, as a banjo's fingers roll through a held chord, each held to the next through
        // the part's rests, so that the roll runs on, the next chord taking it over at its first 8th
        // but a break's, its soloist's own line, whose rests are the break's
        static bool Rolled(RealizedNote note) => note.State.GetStateValue(CompositionStateKinds.Arpeggio) == (int)ArpeggioPattern.Roll && note.Pitches.Length > 1
                                                 && note.State.GetStateValue(CompositionStateKinds.LineSolo) == 0;
        foreach (var (track, timeline) in result.ToArray())
            if (song.Tracks.Definitions[track].Role.PlaysChords() && timeline.Any(x => Rolled(x.Value)))
                result[track] = ThreeFingerRoll(Held(timeline, Rolled, changes, PhraseEnd, position => holds.NextAfter(RingsOutOf, position, track)), position => song.Form.Map.PlaceOf(position).Beat, EighthFrom);

        // and a part's whole chords cycled at the frame rate where it plays them so, as a sound chip plays a chord on one voice
        foreach (var (track, timeline) in result.ToArray())
            if (timeline.Any(x => Has(x.Value, PartArticulation.Frames)))
                result[track] = Frames(timeline, position => Meter.BeatsPerSecond(tempo.GetEffectiveValueAt(position)));

        return draft with { Notes = result.ToImmutable() };
    }

    /// <summary>
    ///     A part's broken chords let ring, as a guitar's strings or a piano's held keys ring: every note of one sounding to its
    ///     chord's change, a bar at most, or to where its pitch is struck again, so that its seconds ring together; on a
    ///     clean instrument that dies away, and a figure of 8ths or slower, as faster ones or held sounds would blur.
    /// </summary>
    /// <param name="rings">Whether the part lets a note ring where it is, by its state and its place.</param>
    /// <param name="heldFrom">Where the part is next held silent after a position, which a ringing note stops at.</param>
    private static EventTimeline<RealizedNote> LetRing(EventTimeline<RealizedNote> timeline, int own, double[] changes, SongMap map, Func<RealizedNote, double, bool> rings, Func<double, double> heldFrom)
    {
        var notes = timeline.ToArray();
        return EventTimeline.Create(timeline.Duration, notes.Select((note, i) =>
        {
            var program = InstrumentsOverTime.ProgramOf(note.Value.State, own);
            var broken = note.Value.State.GetStateValue(CompositionStateKinds.Arpeggio) != 0 && note.Value.Pitches.Length == 1;
            // a broken chord's note on an instrument that holds its sound ends where the part's next starts, as an
            // arpeggiator's notes do, which held on would smear into a cluster
            if (broken && !InstrumentTraits.Rings(program) && i + 1 < notes.Length && notes[i + 1].Position - note.Position < note.Value.Duration - 1e-9)
                return (note.Value with { Duration = notes[i + 1].Position - note.Position }).ToTimelineItem(note.Position);
            if (!rings(note.Value, note.Position) || !broken || !InstrumentTraits.Rings(program)
                || i + 1 < notes.Length && notes[i + 1].Position - note.Position < RingsFrom - 1e-9)
                return note;
            var change = changes.FirstOrDefault(x => x > note.Position + 1e-9, timeline.Duration);
            var again = notes.Skip(i + 1).FirstOrDefault(x => x.Value.Pitches.Contains(note.Value.Pitches[0]), new TimelineItem<RealizedNote>(timeline.Duration, note.Value)).Position;
            var end = Math.Min(Math.Min(Math.Min(change, again), note.Position + map.MeterAt(note.Position).BarDuration), heldFrom(note.Position));
            return end - note.Position > note.Value.Duration ? (note.Value with { Duration = end - note.Position }).ToTimelineItem(note.Position) : note;
        }));
    }

    /// <summary>
    ///     A part's whole chords cycled at the frame rate: each chord's notes in turn from its lowest up, spread so that no two
    ///     in turn are a tone or less apart (<see cref="Spread" />), a note every <see cref="FrameSeconds" /> or, in a chord
    ///     held longer than <see cref="FrameCycles" /> cycles of it, slower in whole frames to cycle that often, for as long
    ///     as the chord sounds, unstrummed.
    /// </summary>
    /// <param name="beatsPerSecond">The beats a second at a position, by the tempo there.</param>
    /// <summary>
    ///     A line's notes held each to its next where it holds them, a picked line's (<see cref="PartArticulation.Holds" />) or a
    ///     rolled chord's, so that no rest
    ///     falls between them: to the next note's start, but no further than its phrase's end, which the next phrase starts
    ///     after a breath, the next change of chord, over which it would sound against the next chord, and the next stretch
    ///     held silent; and where it stops at a change, the next note, of the chord there, struck at the change, so that the
    ///     line moves with the chords and runs on; a note in no phrase, an intro of its own's, as it is.
    /// </summary>
    /// <param name="held">Whether a note is held.</param>
    /// <param name="phraseEnd">Where the phrase a position plays in ends; none outside the sections.</param>
    /// <param name="heldFrom">Where the part is next held silent after a position.</param>
    internal static EventTimeline<RealizedNote> Held(EventTimeline<RealizedNote> timeline, Func<RealizedNote, bool> held, double[] changes, Func<double, double?> phraseEnd, Func<double, double> heldFrom)
    {
        var notes = timeline.ToList();
        for (var i = 0; i < notes.Count; i++)
        {
            var note = notes[i];
            if (!held(note.Value) || phraseEnd(note.Position) is not { } phrase)
                continue;
            var nextAt = notes.FindIndex(i + 1, x => x.Position > note.Position + 1e-9);
            var next = nextAt < 0 ? timeline.Duration : notes[nextAt].Position;
            var change = changes.FirstOrDefault(x => x > note.Position + 1e-9, timeline.Duration);
            var silent = heldFrom(note.Position);
            var end = Math.Min(Math.Min(next, phrase), Math.Min(change, silent));
            if (end - note.Position > note.Value.Duration + 1e-9)
                notes[i] = (note.Value with { Duration = end - note.Position }).ToTimelineItem(note.Position);
            // the next note struck at the change it waits after, where it is held too and no other change or silence comes first
            if (nextAt >= 0 && change < next - 1e-9 && change < phrase - 1e-9 && change < silent - 1e-9 && held(notes[nextAt].Value)
                && !changes.Any(x => x > change + 1e-9 && x <= next + 1e-9) && heldFrom(change) > next - 1e-9)
            {
                var moved = notes[nextAt];
                notes[nextAt] = (moved.Value with { Duration = moved.Value.Duration + moved.Position - change }).ToTimelineItem(change);
                // a note struck with it, a chord's, moved with it
                for (var k = nextAt + 1; k < notes.Count && Math.Abs(notes[k].Position - moved.Position) < 1e-9; k++)
                    notes[k] = (notes[k].Value with { Duration = notes[k].Value.Duration + notes[k].Position - change }).ToTimelineItem(change);
            }
        }

        return EventTimeline.Create(timeline.Duration, notes);
    }

    /// <summary>How a genre's tremolo picks: in measured 16ths, as rock and metal pick their long notes, or as fast as the hand plays evenly, a surf or a black metal guitarist's.</summary>
    public static ImmutableArray<(TremoloSpeed Speed, ByConvention Weight)> Speeds { get; } = [(TremoloSpeed.Measured, new(1, 1, 1)), (TremoloSpeed.Fast, new(0, 0, 0))];

    /// <summary>
    ///     A part's notes tremolo-picked where it picks them (<see cref="ArticulationChoice.TremoloStep" />): each on a plucked
    ///     instrument re-struck, its pitches and all, a power chord whole, down and up in turn as the hand alternates, the
    ///     up-stroke the strum's, a touch softer; at the step its state picks no quicker than <see cref="FastStrokes" />, an
    ///     8th at a tempo too fast for its 16ths, a tempo's 16ths at a black metal band's and its 32nds at a slow one's, a note
    ///     shorter than <see cref="TremoloStrokes" /> of its strokes played as it is, the line's own; picked until the
    ///     part's next note at the latest, and in the appearance it starts in alone, held on into the next. A note it mutes,
    ///     bends or ornaments is left as it is, and a solo's.
    /// </summary>
    /// <param name="until">Where the appearance a note starts in ends; none in an intro of its own, or where it keeps out.</param>
    internal static EventTimeline<RealizedNote> Tremolo(EventTimeline<RealizedNote> timeline, int own, Func<double, double?> until, Func<double, double> beatsPerSecond, double stroke) =>
        EventTimeline.Create(timeline.Duration, timeline.SelectMany((struck, i) =>
        {
            // picked until the part's next note at the latest, which the hand moves to
            var next = timeline.Skip(i + 1).Select(x => (TimelineItem<RealizedNote>?)x).FirstOrDefault(x => x!.Value.Position > struck.Position + 1e-9);
            var note = next is { } after && after.Position - struck.Position < struck.Value.Duration - 1e-9
                ? (struck.Value with { Duration = after.Position - struck.Position }).ToTimelineItem(struck.Position)
                : struck;
            var state = note.Value.State;
            var ornament = state.GetStateValue(CompositionStateKinds.Ornament);
            // a lead line the part solos played as it is
            var finest = ChoiceOf(note.Value).TremoloStep;
            if (finest <= 0 || until(note.Position) is not { } end || !InstrumentTraits.Has(InstrumentsOverTime.ProgramOf(state, own), InstrumentTrait.Strums)
                || state.GetStateValue(CompositionStateKinds.PalmMute) > 0 || state.GetStateValue(CompositionStateKinds.Bend) != 0 || !ornament.IsDefaultOrEmpty
                || state.GetStateValue(CompositionStateKinds.LineSolo) != 0)
                return [struck];
            // at the quickest tempo it is held at, its start's or its end's, a note held into a faster section's
            var perSecond = Math.Max(beatsPerSecond(note.Position), beatsPerSecond(note.Position + note.Value.Duration - 1e-6));
            var step = TremoloSteps.Where(x => x >= finest - 1e-9).LastOrDefault(x => perSecond / x <= FastStrokes + 1e-9, TremoloSteps[0]);
            if (note.Value.Duration < TremoloStrokes * step - 1e-9)
                return [struck];
            // its strokes in the section that picks, the rest of it held into the next as it was
            var count = Math.Max(1, Enumerable.Range(0, (int)Math.Round(note.Value.Duration / step)).TakeWhile(k => note.Position + k * step < end - 1e-9).Count());
            return Enumerable.Range(0, count).Select(k => (note.Value with
            {
                State = state.With(CompositionStateKinds.Strum, (k % 2 == 0 ? 1 : -1) * stroke * perSecond).With(CompositionStateKinds.Picked, k + 1),
                Duration = k + 1 < count ? step : note.Value.Duration - k * step
            }).ToTimelineItem(note.Position + k * step));
        }));

    /// <summary>
    ///     A part's rolled chords, each struck in even 8ths from its first 8th until the 8th the part's next note takes over,
    ///     each stroke the note of the roll its 8th in its bar plays (<see cref="ArpeggioPattern.Roll" />), as a banjo's
    ///     fingers roll through a chord in an even stream whatever the part's own rhythm, its accents moving against the bar
    ///     rather than its notes; a chord no 8th falls in left to the roll before it, but a break's, struck where it falls as
    ///     the tune's; another note as it is.
    /// </summary>
    /// <param name="inBar">Where a position falls in its bar, in beats.</param>
    /// <param name="eighthFrom">The first 8th of its bar at or after a position.</param>
    internal static EventTimeline<RealizedNote> ThreeFingerRoll(EventTimeline<RealizedNote> timeline, Func<double, double> inBar, Func<double, double> eighthFrom) =>
        EventTimeline.Create(timeline.Duration, timeline.SelectMany((struck, i) =>
        {
            if (struck.Value.State.GetStateValue(CompositionStateKinds.Arpeggio) != (int)ArpeggioPattern.Roll || struck.Value.Pitches.Length < 2)
                return [struck];
            var next = timeline.Skip(i + 1).Select(x => (TimelineItem<RealizedNote>?)x).FirstOrDefault(x => x!.Value.Position > struck.Position + 1e-9);
            var voiced = struck.Value.Pitches;
            // on the 8ths from the chord's first to the one the next note takes over at, or as long as it sounds, its strokes
            // each to the next
            var end = next is { } after && struck.Position + struck.Value.Duration >= after.Position - 1e-9 ? eighthFrom(after.Position) : struck.Position + struck.Value.Duration;
            var strokes = new List<double>();
            // a break's note, the tune's, struck where it falls, the roll running on from the next 8th
            if (struck.Value.State.GetStateValue(CompositionStateKinds.LineSolo) != 0 && eighthFrom(struck.Position) > struck.Position + 1e-9)
                strokes.Add(struck.Position);
            for (var at = eighthFrom(struck.Position); at < end - 1e-9; at = eighthFrom(at + 1e-6))
                strokes.Add(at);
            return strokes.Select((at, k) => (struck.Value with
            {
                Pitches = [Arpeggios.Pick(ArpeggioPattern.Roll, voiced, k, at, false, 0, voiced[0], 0, inBar(at))],
                Duration = (k + 1 < strokes.Count ? strokes[k + 1] : end) - at,
                State = struck.Value.State.With(CompositionStateKinds.Strum, 0.0)
            }).ToTimelineItem(at));
        }));

    /// <summary>A roll's step, in beats: an 8th.</summary>
    public const double RollStep = 0.5;

    /// <summary>
    ///     A part's whole chords cycled at the frame rate where it cycles them (<see cref="PartArticulation.Frames" />), each
    ///     until the part's next note at the latest; another note as it is.
    /// </summary>
    /// <param name="beatsPerSecond">The beats a second at a position, by the tempo there.</param>
    internal static EventTimeline<RealizedNote> Frames(EventTimeline<RealizedNote> timeline, Func<double, double> beatsPerSecond) =>
        EventTimeline.Create(timeline.Duration, timeline.SelectMany((struck, i) =>
        {
            if (struck.Value.Pitches.Length < 2 || !Has(struck.Value, PartArticulation.Frames))
                return [struck];
            // cycled until the part's next note at the latest, so that two chords' cycles never run together
            var note = i + 1 < timeline.Count && timeline[i + 1].Position - struck.Position < struck.Value.Duration - 1e-9
                ? (struck.Value with { Duration = timeline[i + 1].Position - struck.Position }).ToTimelineItem(struck.Position)
                : struck;
            var pitches = Spread(note.Value.Pitches);
            var seconds = note.Value.Duration / beatsPerSecond(note.Position);
            var frames = Math.Max(FrameSeconds / Frame, Math.Floor(seconds / (pitches.Length * FrameCycles) / Frame));
            var step = frames * Frame * beatsPerSecond(note.Position);
            var count = Math.Max(1, (int)Math.Ceiling(note.Value.Duration / step - 1e-9));
            var state = note.Value.State.With(CompositionStateKinds.Strum, 0.0).With(CompositionStateKinds.Cycled, 1);
            return Enumerable.Range(0, count).Select(k => (note.Value with
            {
                State = state, Pitches = [pitches[k % pitches.Length]], Duration = Math.Min(step, note.Value.Duration - k * step)
            }).ToTimelineItem(note.Position + k * step));
        }));

    /// <summary>
    ///     How near two of a frame-rate arpeggio's notes may sound in turn, in semitones: a tone, which cycled that fast is
    ///     heard as one line warbling, a trill, rather than a chord, where a third is heard as two notes.
    /// </summary>
    public const int FramesNearest = 2;

    /// <summary>
    ///     A chord's notes as a frame-rate arpeggio cycles them, from the lowest up, each a tone or less above the note below
    ///     moved up an octave until none is, as a chip musician spreads a sus2 to root, fifth and ninth: every note kept.
    /// </summary>
    internal static int[] Spread(IEnumerable<int> chord)
    {
        var pitches = chord.Distinct().Order().ToList();
        for (var i = 1; i < pitches.Count; i++)
        {
            if (pitches[i] - pitches[i - 1] > FramesNearest)
                continue;
            var moved = pitches[i] + 12;
            pitches.RemoveAt(i);
            // an octave up may land on another note, which the chord sounds already
            if (!pitches.Contains(moved))
                pitches.Insert(pitches.FindIndex(x => x > moved) is var at and >= 0 ? at : pitches.Count, moved);
            i = 0;
        }

        return [..pitches];
    }

    /// <summary>The chance a line's part slurs its steps, by the sound facet: never in the plainest songs, which play every note as struck.</summary>
    public static ByConvention LegatoChance { get; } = new(0, 0.4, 0.6);

    /// <summary>How far a note may step from the note before and be slurred into, in semitones, and the gap it may leave, in beats.</summary>
    public const int SlurStep = 2;

    public const double SlurGap = 0.25;

    /// <summary>
    ///     A line with its steps slurred: a note a step or less from the one before, within its phrase and close after it,
    ///     marked as slurred, its articulation, and the note before held into it with no gap.
    /// </summary>
    internal static EventTimeline<RealizedNote> Slur(EventTimeline<RealizedNote> timeline)
    {
        var items = timeline.ToArray();
        for (var i = 1; i < items.Length; i++)
        {
            var (before, note) = (items[i - 1], items[i]);
            var gap = note.Position - (before.Position + before.Value.Duration);
            if (!Has(note.Value, PartArticulation.Slurs) || Math.Abs(note.Value.Pitches[0] - before.Value.Pitches[0]) is 0 or > SlurStep || gap > SlurGap + 1e-9 || note.Position <= before.Position + 1e-9
                || before.Value.State.GetStateValue(StateKinds.HeldDuration) > 0 || note.Value.State.GetStateValue(CompositionStateKinds.LinePhraseStart) != (int)PhraseStart.None)
                continue;
            items[i - 1] = (before.Value with { Duration = note.Position - before.Position }).ToTimelineItem(before.Position);
            items[i] = (note.Value with { State = note.Value.State.With(CompositionStateKinds.Legato, 1) }).ToTimelineItem(note.Position);
        }

        return EventTimeline.Create(timeline.Duration, items);
    }


    /// <summary>Whether a part chugs: the riff and its twin, the rhythm part, the chords or the bass, on a guitar or a picked bass.</summary>
    internal static bool Chugs(TrackRole role, int program) => role is TrackRole.Riff or TrackRole.RiffTwin or TrackRole.Rhythm or TrackRole.Chords or TrackRole.Bass && InstrumentTraits.Has(program, InstrumentTrait.Chugs);
}
