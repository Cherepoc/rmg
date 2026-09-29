using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A note of a line to place: the note, its state as <c>Realizer</c> reads it, and the chord it plays over, as the
///     scale's steps above the chord's root, the chord's notes among them, and their pitch classes.
/// </summary>
/// <param name="ScaleLength">How many steps the note's scale has.</param>
internal sealed record LineNote(
    TimelineItem<StateMap> Note,
    StateMap State,
    ChordContext Chord,
    ImmutableArray<int> Steps,
    IReadOnlySet<int> Classes,
    int ScaleLength
)
{
    public double Position => Note.Position;
}

/// <summary>
///     Where a line's notes are placed, the melody's and the bass's, before <c>Realizer</c>: its notes in order over
///     their chords, for the line's rules to place one after another, and every placed note kept as its scale step
///     above its chord's root, and the semitones it is raised or lowered from it, which <c>Realizer</c> plays.
/// </summary>
internal static class LinePlacement
{
    private const int OctaveNoteCount = 12;

    /// <summary>A track's notes in order, each over the chord it plays, as <c>Realizer</c> works them out.</summary>
    /// <param name="common">The state every track shares, such as the chords and the key.</param>
    public static ImmutableArray<LineNote> GetNotes(EventStateTimelineMap<StateMap> track, IInstrumentTrack definition, StateTimelineMap common)
    {
        var states = Realizer.GetNoteStates(track, definition, common);
        return
        [
            ..track.EventTimeline.Zip(states).Select(x =>
                {
                    var (note, state) = x;
                    var (chord, steps, classes) = Realizer.GetChordNotes(state.Value);
                    return new LineNote(note, state.Value, chord, steps, classes, Realizer.GetChord(state.Value).ScaleOffsets.Length);
                }
            )
        ];
    }

    /// <summary>
    ///     The notes placed at the pitches given, each kept as its scale step above its chord's root and, off the scale,
    ///     the semitones it is raised or lowered from the step nearest it.
    /// </summary>
    public static EventTimeline<StateMap> Store(double duration, IEnumerable<(LineNote Note, int Pitch)> placed)
    {
        return EventTimeline.Create(
            duration,
            placed.Select(x =>
                {
                    var step = GetScaleStep(x.Note.Chord, x.Pitch);
                    var alteration = x.Pitch - x.Note.Chord.GetPitch(step);
                    var value = x.Note.Note.Value.With(StateKinds.ScaleStep, step);
                    return (alteration == 0 ? value : value.With(StateKinds.Alteration, alteration)).ToTimelineItem(x.Note.Position);
                }
            )
        );
    }

    /// <summary>The scale step of a note, counted from the chord's root: the step whose note is nearest it.</summary>
    internal static int GetScaleStep(ChordContext chord, int note)
    {
        // a scale step is between one and a few semitones, so the step is near the note's distance in sevenths of
        // an octave
        var guess = (int)Math.Round((note - chord.Root) * (double)Scales.StepCount / OctaveNoteCount);
        return Enumerable.Range(guess - 4, 9).MinBy(x => Math.Abs(chord.GetPitch(x) - note));
    }
}
