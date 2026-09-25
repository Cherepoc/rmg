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
        var eventStateTimelineMap = EventStateTimelineMap.Create(duration, eventTimeline, StateTimelineMap.Create(0));
        var trackEventStateTimelineMap = TrackEventStateTimelineMap.Create(
            duration,
            [new KeyValuePair<int, EventStateTimelineMap<StateMap>>(0, eventStateTimelineMap)],
            StateTimelineMap.Create(0)
        );
        var tracks = ImmutableSortedDictionary<int, IInstrumentTrack>.Empty.Add(0, track);
        return new Song(duration, tracks, trackEventStateTimelineMap);
    }

    private static PitchInstrumentTrack PitchTrack(int minOctaveOffset = 0, int maxOctaveOffset = 4) =>
        new(StateMap.Default, 0, minOctaveOffset, maxOctaveOffset);

    [Test]
    [Arguments(-1.5)]
    [Arguments(0)]
    [Arguments(1.5)]
    public async Task PercussionNote_LastsASixteenth_WhateverItsDurationState(double quarterNoteDurationPower)
    {
        var stateMap = StateMap.FromStates([StateKinds.QuarterNoteDurationPower.CreateState(quarterNoteDurationPower)]);
        var song = CreateSong(1, new PercussionInstrumentTrack(StateMap.Default, [36]), stateMap.ToTimelineItem(0));

        var result = Render.RenderSong(song);

        await Assert.That(result.Tracks[0].NoteTimeline[0].Value.Duration).IsEqualTo(0.25);
    }

    [Test]
    public async Task SingleNote_HasVelocityInMidiRange()
    {
        var song = CreateSong(1, PitchTrack(), StateMap.Default.ToTimelineItem(0));

        var result = Render.RenderSong(song);

        // with no spread to scale by, the middle of the typical velocities
        await Assert.That(result.Tracks[0].NoteTimeline[0].Value.Velocity).IsEqualTo(0.6);
    }

    [Test]
    public async Task NotesWithEqualVelocity_HaveVelocityInMidiRange()
    {
        var stateMap = StateMap.FromStates([StateKinds.Velocity.CreateState(0.7)]);
        var song = CreateSong(2, PitchTrack(), stateMap.ToTimelineItem(0), stateMap.ToTimelineItem(1));

        var result = Render.RenderSong(song);

        foreach (var note in result.Tracks[0].NoteTimeline)
        {
            await Assert.That(note.Value.Velocity).IsEqualTo(0.6);
        }
    }

    [Test]
    public async Task NotesWithDifferentVelocity_AreScaledToFullRange()
    {
        var quiet = StateMap.FromStates([StateKinds.Velocity.CreateState(1)]);
        var loud = StateMap.FromStates([StateKinds.Velocity.CreateState(3)]);
        var song = CreateSong(2, PitchTrack(), quiet.ToTimelineItem(0), loud.ToTimelineItem(1));

        var result = Render.RenderSong(song);

        var velocities = result.Tracks[0].NoteTimeline.Select(x => x.Value.Velocity).ToArray();
        await Assert.That(velocities[0]).IsEqualTo(0.2);
        await Assert.That(velocities[1]).IsEqualTo(1);
    }

    [Test]
    [Arguments(0, 0.2)]
    [Arguments(1, 0.3)]
    [Arguments(10, 0.6)]
    [Arguments(19, 0.9)]
    [Arguments(20, 1)]
    public async Task Velocities_FromFifthToNinetyFifthPercentile_AreSpreadFrom03To09(int velocity, double expected)
    {
        // velocities 0 to 20, whose 5th percentile is 1 and 95th is 19
        var notes = Enumerable.Range(0, 21)
            .Select(x => StateMap.FromStates([StateKinds.Velocity.CreateState(x)]).ToTimelineItem(x))
            .ToArray();
        var song = CreateSong(21, PitchTrack(), notes);

        var result = Render.RenderSong(song);

        await Assert.That(result.Tracks[0].NoteTimeline[velocity].Value.Velocity).IsEqualTo(expected).Within(1e-9);
    }

    [Test]
    public async Task ChordNoteFromNextChordOctave_IsOneScaleOctaveAboveChordNote()
    {
        // 7-note scale, a triad of C, E and G, which are scale steps 0, 2 and 4.
        // Chord note offset 1.4 selects the 5th chord note (index 4 of the repeating chord): chord note #2 one octave up.
        // One octave up in the scale is 7 scale steps, not the 3 notes of the chord.
        var stateMap = StateMap.FromStates(
            [
                StateKinds.ScaleOffsets.CreateState([0, 2, 4, 5, 7, 9, 11]),
                StateKinds.ChordNotePitchOffsets.CreateState([0, 4 / 12.0, 7 / 12.0]),
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
                StateKinds.ChordNotePitchOffsets.CreateState([0, 4 / 12.0, 7 / 12.0]),
                StateKinds.ChordNoteOffset.CreateState([0.4]),
                StateKinds.OctaveOffset.CreateState(7),
            ]
        );
        var song = CreateSong(1, PitchTrack(), stateMap.ToTimelineItem(0));

        var result = Render.RenderSong(song);

        // chord note offset 0.4 -> index round(0.4 * 3) = 1 -> scale step 2 -> scale offset 4 in octave 7
        await Assert.That(result.Tracks[0].NoteTimeline[0].Value.Offset).IsEqualTo(7 * 12 + 4);
    }

    [Test]
    // a small offset either way is less than half a step, so the note stays on the root
    [Arguments(-0.05, 7 * 12 + 0)]
    [Arguments(0.05, 7 * 12 + 0)]
    // more than half a step up or down moves the note a whole step
    [Arguments(0.1, 7 * 12 + 2)]
    [Arguments(-0.1, 6 * 12 + 11)]
    public async Task ChordRootOffset_RoundsToNearestScaleStep(double chordRootOffset, int expectedNote)
    {
        // 7-note scale: one scale step is 1/7 of the offset
        var stateMap = StateMap.FromStates(
            [
                StateKinds.ScaleOffsets.CreateState([0, 2, 4, 5, 7, 9, 11]),
                StateKinds.ChordRootNoteOffset.CreateState([chordRootOffset]),
                StateKinds.ChordNoteOffset.CreateState([0]),
                StateKinds.OctaveOffset.CreateState(7),
            ]
        );
        var song = CreateSong(1, PitchTrack(), stateMap.ToTimelineItem(0));

        var result = Render.RenderSong(song);

        await Assert.That(result.Tracks[0].NoteTimeline[0].Value.Offset).IsEqualTo(expectedNote);
    }

    [Test]
    [Arguments(2, 2)]
    [Arguments(4, 4)]
    [Arguments(12, 4)]
    public async Task HeldNote_LastsUntilTheNextNote_ButNoLongerThanABar(double nextNotePosition, double expectedDuration)
    {
        // a factor of 1 holds the note all the way to the next one
        var stateMap = StateMap.FromStates([StateKinds.NextNoteDurationFactor.CreateState(1.0)]);
        var song = CreateSong(16, PitchTrack(), stateMap.ToTimelineItem(0), stateMap.ToTimelineItem(nextNotePosition));

        var result = Render.RenderSong(song);

        await Assert.That(result.Tracks[0].NoteTimeline[0].Value.Duration).IsEqualTo(expectedDuration);
    }

    [Test]
    public async Task LastHeldNote_DoesNotRingToTheEndOfALongSilence()
    {
        var stateMap = StateMap.FromStates([StateKinds.NextNoteDurationFactor.CreateState(1.0)]);
        var song = CreateSong(32, PitchTrack(), stateMap.ToTimelineItem(0));

        var result = Render.RenderSong(song);

        await Assert.That(result.Tracks[0].NoteTimeline[0].Value.Duration).IsEqualTo(4);
    }
}
