using Rmg.Core.Composition;
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
        return new Song(duration, Meter.FourFour, tracks, trackEventStateTimelineMap);
    }

    private static PitchInstrumentTrack PitchTrack(int minOctaveOffset = 0, int maxOctaveOffset = 4) =>
        new(StateMap.Default, 0, minOctaveOffset, maxOctaveOffset, TrackRole.Chords, 0);

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
    public async Task ANoteAtTheCentre_PlaysAtTheMiddleVelocity()
    {
        var stateMap = StateMap.FromStates([StateKinds.Velocity.CreateState(Render.VelocityCentre)]);
        var song = CreateSong(1, PitchTrack(), stateMap.ToTimelineItem(0));

        var result = Render.RenderSong(song);

        await Assert.That(result.Tracks[0].NoteTimeline[0].Value.Velocity).IsEqualTo(0.6).Within(1e-9);
    }

    [Test]
    public async Task ANotesVelocity_IsTheSame_WhateverTheOtherNotesOfTheSong()
    {
        // on a fixed scale, so that an evenly played song stays even, and a quiet one quiet
        StateMap Velocity(double velocity) => StateMap.FromStates([StateKinds.Velocity.CreateState(velocity)]);
        var alone = Render.RenderSong(CreateSong(1, PitchTrack(), Velocity(0.5).ToTimelineItem(0)));
        var withOthers = Render.RenderSong(CreateSong(3, PitchTrack(), Velocity(0.5).ToTimelineItem(0), Velocity(-2).ToTimelineItem(1), Velocity(3).ToTimelineItem(2)));

        await Assert.That(withOthers.Tracks[0].NoteTimeline[0].Value.Velocity).IsEqualTo(alone.Tracks[0].NoteTimeline[0].Value.Velocity);
    }

    [Test]
    [Arguments(-10.0)]
    [Arguments(-1.0)]
    [Arguments(0.0)]
    [Arguments(1.0)]
    [Arguments(10.0)]
    public async Task Velocities_RiseWithTheSum_AndStayBetweenTheQuietestAndTheLoudest(double sum)
    {
        var velocity = Render.ToMidiVelocity(sum);

        await Assert.That(velocity).IsBetween(0.2, 1);
        await Assert.That(Render.ToMidiVelocity(sum + 0.1)).IsGreaterThan(velocity);
    }

    [Test]
    [Arguments(0, 7 * 12 + 0)]
    [Arguments(1, 7 * 12 + 2)]
    [Arguments(-1, 6 * 12 + 11)]
    [Arguments(7, 8 * 12 + 0)]
    public async Task ChordRoot_IsWholeScaleSteps_AboveTheTonic(int chordRoot, int expectedNote)
    {
        // a chord of the root alone, over a 7-note scale
        var stateMap = StateMap.FromStates(
            [
                StateKinds.ScaleOffsets.CreateState([0, 2, 4, 5, 7, 9, 11]),
                StateKinds.ChordRoot.CreateState(chordRoot),
                StateKinds.ChordNotePitchOffsets.CreateState([0.0]),
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
