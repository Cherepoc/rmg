using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.Scales;

public sealed class ScalesTest
{
    private static IEnumerable<Scale> AllScales => Rmg.Core.Composition.Scales.All;

    [Test]
    public async Task EveryScale_HasSevenAscendingNotesFromTheRootWithinAnOctave()
    {
        foreach (var scale in AllScales)
        {
            await Assert.That(scale.Offsets.Length).IsEqualTo(7);
            await Assert.That(scale.Offsets[0]).IsEqualTo(0);
            await Assert.That(scale.Offsets.Zip(scale.Offsets.Skip(1)).All(x => x.First < x.Second)).IsTrue();
            await Assert.That(scale.Offsets[^1]).IsLessThan(12);
        }
    }

    [Test]
    public async Task Scales_AreDistinct_AndWeighted()
    {
        await Assert.That(AllScales.Select(x => x.Name).Distinct().Count()).IsEqualTo(AllScales.Count());
        await Assert.That(AllScales.Select(x => string.Join(",", x.Offsets)).Distinct().Count()).IsEqualTo(AllScales.Count());
        await Assert.That(AllScales.All(x => x.Weight > 0)).IsTrue();
    }

    [Test]
    public async Task Pick_FollowsTheWeights()
    {
        const int drawCount = 100_000;
        var context = new GenerationContext(1);
        var counts = Enumerable.Range(0, drawCount)
            .Select(_ => Rmg.Core.Composition.Scales.Pick(context))
            .CountBy(x => x)
            .ToDictionary();
        var weightSum = AllScales.Sum(x => x.Weight);

        foreach (var scale in AllScales)
            await Assert.That(counts.GetValueOrDefault(scale) / (double)drawCount).IsEqualTo(scale.Weight / weightSum).Within(0.01);
    }

    [Test]
    public async Task Songs_AreInScalesOfTheTable_TheSameAllSongLong()
    {
        var scales = new HashSet<string>();
        for (var seed = 0; seed < 30; seed++)
        {
            var timeline = SongGenerator.GenerateSong(seed).TrackEventStateTimelineMap.CommonStateTimelineMap
                .GetStateTimeline(StateKinds.ScaleOffsets);

            // one value from the start, never changed
            await Assert.That(timeline.Count).IsEqualTo(1);
            var offsets = timeline[0].Value;
            var scale = AllScales.SingleOrDefault(x => x.Offsets.SequenceEqual(offsets));
            await Assert.That(scale).IsNotNull();
            scales.Add(scale!.Name);
        }

        // 30 songs reach well beyond minor and major
        await Assert.That(scales.Count).IsGreaterThanOrEqualTo(4);
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task EveryPitchedNote_IsInTheSongsScaleAndKey_WithTheStepsRaisedWhereTheyAre(int seed)
    {
        var song = SongGenerator.GenerateSong(seed);
        var common = song.TrackEventStateTimelineMap.CommonStateTimelineMap;
        var offsets = common.GetStateTimeline(StateKinds.ScaleOffsets)[0].Value;
        var key = common.GetStateTimeline(StateKinds.KeyOffset).GetEffectiveValueAt(0);
        var raisedSteps = common.GetStateTimeline(StateKinds.RaisedScaleSteps);
        var approaches = common.GetStateTimeline(StateKinds.ChordApproach);
        var bassProgram = ((PitchInstrumentTrack)song.TrackDefinitions[6]).InstrumentCode;

        var tracks = Render.RenderSong(song).Tracks.Where(x => !x.IsPercussionInstrument).ToArray();

        await Assert.That(tracks.Sum(x => x.NoteTimeline.Count)).IsGreaterThan(0);
        foreach (var track in tracks)
        foreach (var note in track.NoteTimeline)
        {
            // a bass note leading into the next chord by a semitone leaves the scale on purpose
            var approach = (ChordApproach)approaches.GetEffectiveValueAt(note.Position);
            var isChromaticApproach = track.PitchInstrumentCode == bassProgram
                && note.Position % 4 >= 3
                && approach is ChordApproach.HalfStepBelow or ChordApproach.HalfStepAbove;
            if (isChromaticApproach)
                continue;

            var scale = Rmg.Core.Rendering.Render.RaiseScaleSteps(offsets, raisedSteps.GetEffectiveValueAt(note.Position));
            var pitchClasses = scale.Select(x => (x + key) % 12).ToHashSet();
            await Assert.That(pitchClasses).Contains(note.Value.Offset % 12).Because($"position {note.Position}");
        }
    }
}
