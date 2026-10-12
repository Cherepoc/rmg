using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>A line's ornament: a grace note before a note, a turn around it, or a trill on it.</summary>
public enum Ornament
{
    Grace,
    Turn,
    Trill
}

/// <summary>
///     The ornaments a song's lines play, a stage after its notes are realized (<see cref="SongStages" />): a note of the
///     melody's or the counter-melody's opened by quick notes a scale step from it, its neighbours, which take their time
///     from its start, as a flute or a fiddle ornaments a tune. A grace note, one neighbour, on a beat's note; a turn, the
///     note's upper neighbour, the note and its lower, on a held note; a trill, the note and its upper neighbour by turns,
///     on a long one. The song plays an ornament at all by its melody facet, its chance reaching its end, and a figure's
///     notes take it now and then, alike wherever the figure plays; the note marked with its ornament
///     (<see cref="CompositionStateKinds.Ornament" />), which the rendering plays.
/// </summary>
internal static class Ornaments
{
    /// <summary>
    ///     The ornaments, each the chance a song plays it, by its melody facet, never in the plainest songs; the share of the
    ///     notes it may go on that take it; and the shortest note it goes on, in seconds and in beats.
    /// </summary>
    public static ImmutableArray<(Ornament Ornament, ByConvention Chance, double Share, double Seconds, double Beats)> All { get; } =
    [
        (Ornament.Grace, new ByConvention(0, 0.25, 1), 0.12, 0.25, 0.5),
        (Ornament.Turn, new ByConvention(0, 0.08, 1), 0.1, 0.4, 1),
        (Ornament.Trill, new ByConvention(0, 0.1, 1), 0.2, 0.8, 2)
    ];

    private const int OctaveNoteCount = 12;

    /// <summary>How many quick notes an ornament plays a second: about 12, as a trill does.</summary>
    public const double Rate = 12;

    /// <summary>How much of a trilled note trills, the rest the note held, and how long a trill lasts at most, in seconds.</summary>
    public const double TrillShare = 0.75, TrillSeconds = 1.5;

    /// <summary>An ornament's name, as a genre names it, and its label.</summary>
    public static string NameOf(Ornament ornament) => GenreChoices.Name(ornament + "s");

    public static string LabelOf(Ornament ornament) => ornament == Ornament.Grace ? "Grace notes" : ornament + "s";

    /// <summary>The song's melody and counter-melody with their ornaments, where it plays any.</summary>
    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var context = song.Stream(SongStream.Ornaments);
        var melody = song.Facets[Facet.Melody];
        // drawn for every ornament, so that the draws are as many
        var plays = All.Where(x => context.TestProbability(x.Chance.At(melody, song.Genre.Chance(GenreChoices.Ornaments, NameOf(x.Ornament))))).ToArray();
        StateTrace.RecordSong(TracePoints.Ornaments, string.Join(", ", plays.Select(x => x.Ornament)),
            plays.Select(x => x.Ornament).ToImmutableArray());
        if (plays.Length == 0)
            return draft;

        var notes = SongStages.NotesOf(draft);
        var tempo = draft.Timeline.CommonStateTimelineMap.GetStateTimeline(StateKinds.Tempo);
        var result = notes.ToBuilder();
        foreach (var (track, timeline) in notes)
        {
            if (song.Tracks.Definitions[track].Role is not (TrackRole.Melody or TrackRole.CounterMelody))
                continue;
            result[track] = EventTimeline.Create(timeline.Duration, timeline.Select(note =>
            {
                // drawn by the note of its figure, so that a figure played again, or doubled, is ornamented alike
                var key = note.Value.State.GetStateValue(CompositionStateKinds.NoteKey);
                // a note of one pitch, or a line's in octaves, not a block of voices
                var octaves = note.Value.State.GetStateValue(CompositionStateKinds.Octave) > 0 && note.Value.State.GetStateValue(CompositionStateKinds.LineVoices) <= 1;
                if (note.Value.Pitches.Length != 1 && !octaves || key == 0)
                    return note;
                var draw = song.Stream(SongStream.Ornaments, key).GenerateDouble();
                // a quick note's length in beats at the tempo there
                var step = Meter.BeatsPerSecond(tempo.GetEffectiveValueAt(note.Position)) / Rate;
                var seconds = note.Value.Duration / (step * Rate);
                var onBeat = note.Value.State.GetStateValue(CompositionStateKinds.BeatRank) == 0;
                // the longest ornament the note may take, of those the song plays and the note drew
                var ornament = plays.LastOrDefault(x => draw < x.Share && seconds >= x.Seconds && note.Value.Duration >= x.Beats - 1e-9 && (x.Ornament != Ornament.Grace || onBeat));
                return ornament.Share > 0 ? Ornamented(note, ornament.Ornament, step, draw < ornament.Share / 2) : note;
            }));
        }

        return draft with { Notes = result.ToImmutable() };
    }

    /// <summary>A note with its ornament, its quick notes a step each from its start, the note held for the rest.</summary>
    /// <param name="below">A grace note's neighbour below rather than above.</param>
    internal static TimelineItem<RealizedNote> Ornamented(TimelineItem<RealizedNote> note, Ornament ornament, double step, bool below)
    {
        int[] steps = ornament switch
        {
            Ornament.Grace => [below ? -1 : 1],
            Ornament.Turn => [1, 0, -1],
            _ => Enumerable.Range(0, Math.Max(2, (int)Math.Min(note.Value.Duration * TrillShare / step, TrillSeconds * Rate) / 2 * 2)).Select(x => x % 2).ToArray()
        };
        var pitch = note.Value.Pitches[0];
        // the neighbours in the line's scale, or the section's where the line keeps to it
        var (_, _, chord) = Realizer.GetChord(note.Value.State);
        var classes = LineScales.Classes((LineScale)note.Value.State.GetStateValue(CompositionStateKinds.LineScale), chord)
                      ?? Enumerable.Range(0, Scales.StepCount).Select(x => chord.GetPitch(x).Mod(OctaveNoteCount)).ToHashSet();
        int Neighbour(int direction) => Enumerable.Range(1, OctaveNoteCount).Select(x => direction * x).First(x => classes.Contains((pitch + x).Mod(OctaveNoteCount)));
        var state = note.Value.State.With(CompositionStateKinds.Ornament, [..steps.Select(x => x == 0 ? 0 : Neighbour(x))]).With(CompositionStateKinds.OrnamentStep, step);
        return (note.Value with { State = state }).ToTimelineItem(note.Position);
    }
}
