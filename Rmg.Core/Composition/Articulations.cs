using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>The notes' articulations, a stage of the song after it is put together (<see cref="SongStages" />).</summary>
internal static class Articulations
{
    /// <summary>The chance a song's chords are struck short, stabs, a genre's that the songs never play, and how long a stab sounds, in beats: a 16th.</summary>
    public static ByConvention StabChance { get; } = new(0, 0, 0);

    public const double StabLength = 0.25;

    /// <summary>The chance a part on a piano rolls its held chords, by the sound facet: never in the plainest songs.</summary>
    public static ByConvention RollChance { get; } = new(0, 0.3, 0.5);

    /// <summary>The chance a part's broken chords are let ring to the chord's change, a genre's that the songs never play.</summary>
    public static ByConvention LetRingChance { get; } = new(0, 0, 0);

    /// <summary>What a ringing note keeps out of: stop time and a part stopped by the form, whose silence it would fill.</summary>
    private const Hold RingsOutOf = Hold.Stop | Hold.Cut;

    /// <summary>The quickest a broken chord's notes follow each other and are let ring, in beats: an 8th, as faster ones would blur.</summary>
    public const double RingsFrom = 0.5;

    /// <summary>How long a piano's rolled chord takes from its lowest note to its highest, in seconds: slower than a strum.</summary>
    public const double Roll = 0.06;

    /// <summary>How long a guitar's stroke takes across its strings at its quickest and its slowest, in seconds.</summary>
    public const double StrokeFrom = 0.01, StrokeTo = 0.03;

    /// <summary>
    ///     The notes' articulations: in a section that chugs, a guitar's or a picked bass's note that repeats the one before
    ///     on a weak beat palm-muted, its accents and its moves ringing, as a chug riff mutes its eighths on the root and
    ///     rings its accent; the riff's, the rhythm part's and the bass's, by their instruments. And every chord a guitar
    ///     plays strummed, down and up in turn on the steps of its part's chords, a 16th at the finest, as quickly as the
    ///     song strums, and a piano's held
    ///     chord rolled upward where its part rolls, and the chords struck short where they stab; and a bass in
    ///     octaves, its notes off the beat the note before an octave away, or alternating its chord's root and fifth.
    /// </summary>
    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var notes = SongStages.NotesOf(draft);
        var stroke = StrokeFrom + (StrokeTo - StrokeFrom) * song.Stream(SongStream.Strum).GenerateDouble();
        var tempo = draft.Timeline.CommonStateTimelineMap.GetStateTimeline(StateKinds.Tempo);
        var result = notes.ToBuilder();
        foreach (var (track, timeline) in notes)
        {
            if (song.Tracks.Definitions[track] is not PitchInstrumentTrack part)
                continue;
            var rolls = song.Stream(SongStream.Roll, track).TestProbability(RollChance.At(song.Facets[Facet.Sound], song.Genre.Chance(GenreChoices.Articulation, "roll")));
            // the chords struck short, stabs, a genre's, the chords' and the rhythm part's, a skank's chop
            var stabs = part.Role.PlaysChords()
                        && song.Stream(SongStream.Stabs, track).TestProbability(StabChance.At(song.Facets[Facet.Sound], song.Genre.Chance(GenreChoices.Articulation, "stabs")));
            var (bottom, top) = Realizer.GetRange(part);
            // every strong note's place among the strong notes of its chord in its section, from 0, a weak note's -1
            var strongPlaces = new int[timeline.Count];
            for (var (k, last) = (0, -1); k < timeline.Count; k++)
            {
                strongPlaces[k] = -1;
                if (timeline[k].Value.State.GetStateValue(CompositionStateKinds.BeatRank) > 0)
                    continue;
                var sameChord = last >= 0 && timeline[last].Value.State.GetStateValue(StateKinds.ChordChange) == timeline[k].Value.State.GetStateValue(StateKinds.ChordChange)
                                && song.Form.Map.SectionAt(timeline[last].Position) == song.Form.Map.SectionAt(timeline[k].Position);
                strongPlaces[k] = sameChord ? strongPlaces[last] + 1 : 0;
                last = k;
            }

            // whether each note was played an octave from the note before, which the next of a run of weak notes turns back
            // to, and the pitches each was played at
            var octaved = new bool[timeline.Count];
            var played = new ImmutableArray<int>[timeline.Count];
            // the strumming grid, the shortest step between the part's chords, a 16th at the finest, a strum down on its
            // even steps and up on its odd
            var strummed = timeline.Where(x => x.Value.Pitches.Length > 1).Select(x => x.Position).ToArray();
            var grid = Math.Max(0.25, strummed.Zip(strummed.Skip(1), (a, b) => b - a).Where(x => x > 1e-6).DefaultIfEmpty(0.5).Min());

            result[track] = EventTimeline.Create(timeline.Duration, timeline.Select((note, i) =>
            {
                var state = note.Value.State;
                // the appearance it plays as, an intro's and the ending's too
                var section = song.Form.Map.PlayAt(note.Position)?.Index ?? -1;
                var repeats = i > 0 && timeline[i - 1].Value.Pitches.SequenceEqual(note.Value.Pitches);
                var weak = state.GetStateValue(CompositionStateKinds.BeatRank) > 0;
                // by the instrument playing the note, a switched one's
                var program = InstrumentsOverTime.ProgramOf(state, part.InstrumentCode);
                // a riff chugging on its pedal muting every note off its accents, another part its repeats, its own line's
                // alone, a lead line it solos playing as it is
                var onPedal = state.GetStateValue(CompositionStateKinds.LinePedal) > 0;
                var ownLine = state.GetStateValue(CompositionStateKinds.LineSolo) == 0;
                if (Chugs(part.Role, program) && ownLine && section >= 0 && song.Sections[section].Chugs && (repeats || onPedal) && weak)
                    state = state.With(CompositionStateKinds.PalmMute, 1);
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
                                      && song.Form.Map.PlayAt(timeline[i - 1].Position)?.Index == section;
                if (part.Role == TrackRole.Bass && ownLine && !doubled && !walks && sameChordBefore && section >= 0 && song.Sections[section].OctaveBass && !song.Sections[section].Chugs && weak && !leads
                    && played[i - 1] is var before && (octaved[i - 1] || before.All(x => x + 12 <= top) || before.All(x => x - 12 >= bottom)))
                {
                    pitches = octaved[i - 1] ? played[i - 2 < 0 ? 0 : i - 2] : [..before.Select(x => before.All(y => y + 12 <= top) ? x + 12 : x - 12)];
                    octaved[i] = !octaved[i - 1];
                }
                // a bass alternating the root and the fifth, every other strong note of a chord its fifth, below the note
                // before where its range has it and above where not, as a country bass plays; but where it holds a pedal
                if (part.Role == TrackRole.Bass && ownLine && !doubled && section >= 0 && song.Sections[section].AlternatingBass && strongPlaces[i] % 2 == 1 && pitches.Length == 1
                    && state.GetStateValue(CompositionStateKinds.BassPedal) == 0)
                {
                    var fifth = Realizer.GetChord(note.Value.State).Chord.GetPitch(4);
                    var below = timeline[Array.LastIndexOf(strongPlaces, strongPlaces[i] - 1, i)].Value.Pitches.Min() - 1;
                    var under = below - (below - fifth).Mod(12);
                    pitches = [under >= bottom ? under : under + 12 <= top ? under + 12 : pitches[0]];
                }
                // the stroke in beats at the tempo there
                double Beats(double seconds) => seconds * Meter.BeatsPerSecond(tempo.GetEffectiveValueAt(note.Position));
                if (note.Value.Pitches.Length > 1 && InstrumentTraits.Has(program, InstrumentTrait.Strums))
                    // down on the grid's even steps and up on its odd, as the hand swings
                    state = state.With(CompositionStateKinds.Strum, (long)Math.Floor(song.Form.Map.BeatInBar(note.Position) / grid + 1e-9) % 2 == 0 ? Beats(stroke) : -Beats(stroke));
                // a piano's chord held a beat or longer rolled upward, slower, where its part rolls
                else if (rolls && note.Value.Pitches.Length >= 3 && note.Value.Duration >= 1 - 1e-9 && InstrumentTraits.Has(program, InstrumentTrait.Rolls))
                    state = state.With(CompositionStateKinds.Strum, Beats(Roll));

                var duration = stabs ? Math.Min(note.Value.Duration, StabLength) : note.Value.Duration;
                played[i] = pitches;
                return state == note.Value.State && pitches == note.Value.Pitches && duration == note.Value.Duration
                    ? note
                    : (note.Value with { State = state, Pitches = pitches, Duration = duration }).ToTimelineItem(note.Position);
            }));
        }

        // and a line's steps slurred where its part slurs, the riff's twin as its riff does
        foreach (var (track, timeline) in result.ToArray())
            if (song.Tracks.Definitions[track].Role is var role && role.PlaysLine() && role != TrackRole.Bass
                && song.Stream(SongStream.Legato, song.DrawsAs(track))
                    .TestProbability(LegatoChance.At(song.Facets[Facet.Sound], song.Genre.Chance(GenreChoices.Articulation, "legato"))))
                result[track] = Slur(timeline);

        // and a part's broken chords let ring where it lets them, its own sequence drawn for every part that holds chords,
        // and in a calm section always (SectionForm)
        var changes = draft.Timeline.CommonStateTimelineMap.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position).ToArray();
        bool Calm(double position) => song.Form.Map.SectionAt(position) is { } span && song.SectionOf(span.SectionId).Form.Kind == SectionFormKind.Calm;
        // but never into the silence of stop time or a stop the form makes, which a ringing note would fill
        var holds = draft.Holds(song);
        foreach (var (track, timeline) in result.ToArray())
            if (song.Tracks.Definitions[track] is PitchInstrumentTrack { } chords && chords.Role.PlaysChords())
            {
                var rings = song.Stream(SongStream.LetRing, track).TestProbability(LetRingChance.At(song.Facets[Facet.Sound], song.Genre.Chance(GenreChoices.Articulation, "letRing")));
                result[track] = LetRing(timeline, chords.InstrumentCode, changes, song.Form.Map, position => (rings || Calm(position)) && !holds.At(RingsOutOf, position, track),
                    position => holds.NextAfter(RingsOutOf, position, track));
            }

        return draft with { Notes = result.ToImmutable() };
    }

    /// <summary>
    ///     A part's broken chords let ring, as a guitar's strings or a piano's held keys ring: every note of one sounding to its
    ///     chord's change, a bar at most, or to where its pitch is struck again, so that its seconds ring together; on a
    ///     clean instrument that dies away, and a figure of 8ths or slower, as faster ones or held sounds would blur.
    /// </summary>
    /// <param name="rings">Whether the part lets its notes ring where they are.</param>
    /// <param name="heldFrom">Where the part is next held silent after a position, which a ringing note stops at.</param>
    private static EventTimeline<RealizedNote> LetRing(EventTimeline<RealizedNote> timeline, int own, double[] changes, SongMap map, Func<double, bool> rings, Func<double, double> heldFrom)
    {
        var notes = timeline.ToArray();
        return EventTimeline.Create(timeline.Duration, notes.Select((note, i) =>
        {
            var program = InstrumentsOverTime.ProgramOf(note.Value.State, own);
            if (!rings(note.Position) || note.Value.State.GetStateValue(CompositionStateKinds.Arpeggio) == 0 || note.Value.Pitches.Length != 1
                || !InstrumentTraits.Rings(program)
                || i + 1 < notes.Length && notes[i + 1].Position - note.Position < RingsFrom - 1e-9)
                return note;
            var change = changes.FirstOrDefault(x => x > note.Position + 1e-9, timeline.Duration);
            var again = notes.Skip(i + 1).FirstOrDefault(x => x.Value.Pitches.Contains(note.Value.Pitches[0]), new TimelineItem<RealizedNote>(timeline.Duration, note.Value)).Position;
            var end = Math.Min(Math.Min(Math.Min(change, again), note.Position + map.MeterAt(note.Position).BarDuration), heldFrom(note.Position));
            return end - note.Position > note.Value.Duration ? (note.Value with { Duration = end - note.Position }).ToTimelineItem(note.Position) : note;
        }));
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
            if (Math.Abs(note.Value.Pitches[0] - before.Value.Pitches[0]) is 0 or > SlurStep || gap > SlurGap + 1e-9 || note.Position <= before.Position + 1e-9
                || before.Value.State.GetStateValue(StateKinds.HeldDuration) > 0 || note.Value.State.GetStateValue(CompositionStateKinds.LinePhraseStart) != (int)PhraseStart.None)
                continue;
            items[i - 1] = (before.Value with { Duration = note.Position - before.Position }).ToTimelineItem(before.Position);
            items[i] = (note.Value with { State = note.Value.State.With(CompositionStateKinds.Legato, 1) }).ToTimelineItem(note.Position);
        }

        return EventTimeline.Create(timeline.Duration, items);
    }


    /// <summary>Whether a part chugs: the riff and its twin, the rhythm part or the bass, on a guitar or a picked bass.</summary>
    internal static bool Chugs(TrackRole role, int program) => role is TrackRole.Riff or TrackRole.RiffTwin or TrackRole.Rhythm or TrackRole.Bass && InstrumentTraits.Has(program, InstrumentTrait.Chugs);
}
