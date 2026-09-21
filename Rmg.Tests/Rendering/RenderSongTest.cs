using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.Rendering;

public sealed class RenderSongTest
{
    private static Song CreateSong(double duration, IInstrumentTrack track, params TimelineItem<StateMap>[] notes)
    {
        var eventTimeline = EventTimeline.Create(duration, notes);
        var eventStateTimelineMap = EventStateTimelineMap.Create(duration, eventTimeline, StateTimelineMap.Empty);
        var trackEventStateTimelineMap = TrackEventStateTimelineMap.Create(
            duration,
            [new KeyValuePair<int, EventStateTimelineMap<StateMap>>(0, eventStateTimelineMap)],
            StateTimelineMap.Empty
        );
        var tracks = ImmutableSortedDictionary<int, IInstrumentTrack>.Empty.Add(0, track);
        return new Song(duration, tracks, trackEventStateTimelineMap);
    }

    private static PitchInstrumentTrack PitchTrack(int minOctaveOffset = 0, int maxOctaveOffset = 4) =>
        new(StateMap.Default, 0, minOctaveOffset, maxOctaveOffset);

    [Test]
    public async Task SingleNote_HasVelocityInMidiRange()
    {
        var song = CreateSong(1, PitchTrack(), StateMap.Default.ToTimelineItem(0));

        var result = Render.RenderSong(song);

        var velocity = result.Tracks[0].NoteTimeline[0].Value.Velocity;
        await Assert.That(double.IsNaN(velocity)).IsFalse();
        await Assert.That(velocity).IsBetween(0.5, 1);
    }

    [Test]
    public async Task NotesWithEqualVelocity_HaveVelocityInMidiRange()
    {
        var stateMap = StateMap.FromStates([StateKinds.Velocity.CreateState(0.7)]);
        var song = CreateSong(2, PitchTrack(), stateMap.ToTimelineItem(0), stateMap.ToTimelineItem(1));

        var result = Render.RenderSong(song);

        foreach (var note in result.Tracks[0].NoteTimeline)
        {
            await Assert.That(double.IsNaN(note.Value.Velocity)).IsFalse();
            await Assert.That(note.Value.Velocity).IsBetween(0.5, 1);
        }
    }

    [Test]
    public async Task NotesWithDifferentVelocity_AreScaledToUpperHalfOfRange()
    {
        var quiet = StateMap.FromStates([StateKinds.Velocity.CreateState(1)]);
        var loud = StateMap.FromStates([StateKinds.Velocity.CreateState(3)]);
        var song = CreateSong(2, PitchTrack(), quiet.ToTimelineItem(0), loud.ToTimelineItem(1));

        var result = Render.RenderSong(song);

        var velocities = result.Tracks[0].NoteTimeline.Select(x => x.Value.Velocity).ToArray();
        await Assert.That(velocities[0]).IsEqualTo(0.5);
        await Assert.That(velocities[1]).IsEqualTo(1);
    }

    [Test]
    public async Task ChordNoteFromNextChordOctave_IsOneScaleOctaveAboveChordNote()
    {
        // 7-note scale, chord built on scale steps 0, 2, 4.
        // Chord note offset 1.4 selects the 5th chord note (index 4 of the repeating chord): chord note #2 one octave up.
        // One octave up in the scale is 7 scale steps, not the 3 notes of the chord.
        var stateMap = StateMap.FromStates(
            [
                StateKinds.ScaleOffsets.CreateState([0, 2, 4, 5, 7, 9, 11]),
                StateKinds.ChordNoteInScaleOffsets.CreateState([0.3, 0.6]),
                StateKinds.ChordNoteOffset.CreateState([1.4]),
                StateKinds.OctaveOffset.CreateState(7),
            ]
        );
        var song = CreateSong(1, PitchTrack(), stateMap.ToTimelineItem(0));

        var result = Render.RenderSong(song);

        // scale step 2 + 7 = step 9 -> scale offset 4 (E), one octave above the base octave 7 -> octave 8
        var notes = result.Tracks[0].NoteTimeline;
        await Assert.That(notes.Count).IsEqualTo(1);
        await Assert.That(notes[0].Value.Offset).IsEqualTo(8 * 12 + 4);
    }

    [Test]
    public async Task ChordNoteFromBaseChordOctave_IsChordNoteInScale()
    {
        var stateMap = StateMap.FromStates(
            [
                StateKinds.ScaleOffsets.CreateState([0, 2, 4, 5, 7, 9, 11]),
                StateKinds.ChordNoteInScaleOffsets.CreateState([0.3, 0.6]),
                StateKinds.ChordNoteOffset.CreateState([0.4]),
                StateKinds.OctaveOffset.CreateState(7),
            ]
        );
        var song = CreateSong(1, PitchTrack(), stateMap.ToTimelineItem(0));

        var result = Render.RenderSong(song);

        // chord note offset 0.4 -> index floor(0.4 * 3) = 1 -> scale step 2 -> scale offset 4 in octave 7
        await Assert.That(result.Tracks[0].NoteTimeline[0].Value.Offset).IsEqualTo(7 * 12 + 4);
    }
}
