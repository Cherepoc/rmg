using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.SongGenerators;

/// <summary>
///     Every layer of state (song, section, group, track, bar, note) adds its own values to a note, and each of them
///     must reach the note once.
/// </summary>
public sealed class SongGeneratorStateLayerTest
{
    [Test]
    public async Task Notes_GetEachChordRootOffsetOnce()
    {
        // the chord root offsets are a collection, so every layer's random offset stays visible: an offset that
        // shows up twice on a note is one layer's value applied twice
        for (var seed = 0; seed < 30; seed++)
        {
            var song = SongGenerator.GenerateSong(seed);
            var commonStateTimelineMap = song.TrackEventStateTimelineMap.CommonStateTimelineMap;

            foreach (var (trackNumber, trackTimelineMap) in song.TrackEventStateTimelineMap.TrackTimelineMap)
            {
                // the same merge as Render does
                var notes = trackTimelineMap
                    .MergeStateMap(song.TrackDefinitions[trackNumber].StateMap)
                    .MergeStateTimelineMap(commonStateTimelineMap)
                    .ToMappedEventTimeline((s1, s2) => StateMap.Aggregate([s1, s2]));

                foreach (var note in notes)
                {
                    var offsets = note.Value.GetStateValue(StateKinds.ChordRootNoteOffset);
                    await Assert.That(offsets.Distinct().Count())
                        .IsEqualTo(offsets.Length)
                        .Because($"seed {seed}, track {trackNumber}, position {note.Position}");
                }
            }
        }
    }

    [Test]
    public async Task TrackGenerationState_LeavesRenderedStateToRender()
    {
        // Render applies the track definition's state to every note of the track, so generation must not bake it in
        var trackDefinition = new PitchInstrumentTrack(
            new StateMapBuilder()
                .Add(StateKinds.Velocity, 0.5)
                .Add(CompositionStateKinds.Rhythm.Period.Power, -1)
                .ToStateMap(new FakeGenerationContext()),
            0,
            0,
            1
        );

        var result = SongGenerator.GetTrackGenerationStateMap(trackDefinition);

        await Assert.That(result.GetStateValue(StateKinds.Velocity)).IsEqualTo(0);
        await Assert.That(result.GetStateValue(CompositionStateKinds.Rhythm.Period.Power)).IsEqualTo(-1);
    }
}
