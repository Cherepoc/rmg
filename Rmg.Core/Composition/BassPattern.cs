using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>The bass's line placed over the song put together, as <see cref="BassLine" /> places it.</summary>
internal static class BassPattern
{
    /// <summary>
    ///     The song with its bass placed, every note by the rules of <see cref="BassLine" />, in order over the whole song,
    ///     so that its line goes on from section to section and leads into the next section's chord: the chord's note its
    ///     chord note offset picks, landing on a new chord and leading into the next as the bar asks. Placed once the song
    ///     is put together, as it leads across the sections, and kept as its scale steps (<see cref="LinePlacement" />).
    /// </summary>
    public static TrackEventStateTimelineMap<StateMap> Place(TrackEventStateTimelineMap<StateMap> song, int trackNumber, PitchInstrumentTrack definition)
    {
        var (low, high) = Realizer.GetRange(definition);
        var line = new BassLine(low, high, note => Realizer.FitNote(definition, note));
        var track = song.TrackTimelineMap[trackNumber];
        var notes = LinePlacement.GetNotes(track, definition, song.CommonStateTimelineMap);
        var placed = notes.Select((note, i) =>
            {
                LineNote? next = i + 1 < notes.Length ? notes[i + 1] : null;
                var pitch = line.Place(
                    PickChordNote(note),
                    note.Chord,
                    note.Position,
                    next?.Chord,
                    next?.Position,
                    (ChordArrival)note.State.GetStateValue(StateKinds.ChordArrival),
                    (ChordApproach)note.State.GetStateValue(StateKinds.ChordApproach)
                );
                return (note, pitch);
            }
        );
        var timeline = LinePlacement.Store(track.EventTimeline.Duration, placed.ToArray());
        return song.MapTrackEvents(new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>> { [trackNumber] = _ => timeline });
    }

    /// <summary>
    ///     The chord's note a bass note's chord note offset picks, going round the chord's scale notes from the root up and
    ///     an octave up or down for every lap around it; the root for none.
    /// </summary>
    internal static int PickChordNote(LineNote note)
    {
        ImmutableArray<int> degrees = [..note.Steps.Select(x => x.Mod(note.ScaleLength)).Distinct().Order()];
        var (octave, degree) = note.State.GetStateValue(StateKinds.ChordNoteOffset)
            .ToIndexOverLength(degrees.Length)
            .ToPeriodRemainder(degrees.Length);
        return note.Chord.GetPitch(degrees[degree] + octave * note.ScaleLength);
    }
}
