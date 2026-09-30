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
    public async Task Notes_GetEachLayersChordRootOnce()
    {
        // a trace names the layer of every offset, so a layer whose offset shows up twice on a note is one applied
        // twice; the offsets themselves can be equal, such as a section's home and a bar's root of the same step
        var layersSeen = new HashSet<string>();
        for (var seed = 0; seed < 30; seed++)
        {
            // a song of its own, traced to explain its states, which the corpus's are not; the merges below keep the
            // layers only while the trace runs, as the song's generation did
            using var trace = StateTrace.Start();
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
                    var layers = note.Value.Explain(StateKinds.ChordRoot).Select(x => x.Layer).ToArray();
                    layersSeen.UnionWith(layers);
                    await Assert.That(layers.Distinct().Count())
                        .IsEqualTo(layers.Length)
                        .Because($"seed {seed}, track {trackNumber}, position {note.Position}: {string.Join(", ", layers)}");
                }
            }
        }

        // the section's home and the progression's root both reach the notes
        await Assert.That(layersSeen.IsSupersetOf(["Section", "Progression"])).IsTrue().Because(string.Join(", ", layersSeen));
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
            1,
            TrackRole.Chords,
            0
        );

        var result = SongTracks.GetGenerationStateMap(trackDefinition);

        await Assert.That(result.GetStateValue(StateKinds.Velocity)).IsEqualTo(0);
        await Assert.That(result.GetStateValue(CompositionStateKinds.Rhythm.Period.Power)).IsEqualTo(-1);
    }
}
