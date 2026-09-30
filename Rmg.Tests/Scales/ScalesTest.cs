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
    public async Task Sections_AreInScalesOfTheTable_TheSongsFirstInTheSongsScale()
    {
        var scales = new HashSet<string>();
        for (var seed = 0; seed < 32; seed++)
        {
            var song = TestCorpus.Get(seed);
            var timeline = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.ScaleOffsets);
            var traced = song.Trace.Where(x => x.Point == TracePoints.SectionScale).ToDictionary(x => x.Section, x => ((Scale)x.Value!).Name);

            // every section plays the scale it drew, one of the table's, from its start
            foreach (var span in song.Map.Sections)
            {
                var scale = AllScales.SingleOrDefault(x => x.Offsets.SequenceEqual(timeline.GetEffectiveValueAt(span.Start)));
                await Assert.That(scale).IsNotNull();
                await Assert.That(scale!.Name).IsEqualTo(traced[span.SectionId]);
            }

            scales.Add(traced[song.Map.Sections[0].SectionId]);
        }

        // 30 songs reach well beyond minor and major
        await Assert.That(scales.Count).IsGreaterThanOrEqualTo(4);
    }

    [Test]
    public async Task ASectionsScale_IsTheSongs_UnlessItChanges_ToACloseOneMostOften()
    {
        var context = new GenerationContext(1);
        var plain = new HarmonicUnconventionality(0, 1, 1, 1);
        var picks = Enumerable.Range(0, 20_000).Select(_ => Rmg.Core.Composition.Scales.PickSection(context, Rmg.Core.Composition.Scales.NaturalMinor, plain)).ToArray();
        var changed = picks.Where(x => x != Rmg.Core.Composition.Scales.NaturalMinor).ToArray();

        await Assert.That(changed.Length / (double)picks.Length).IsEqualTo(Rmg.Core.Composition.Scales.SectionChangeChance).Within(0.01);
        await Assert.That(changed.Count(x => x.Distance(Rmg.Core.Composition.Scales.NaturalMinor) == 1)).IsGreaterThan(changed.Length * 3 / 4);
    }

    [Test]
    public async Task Brightness_OrdersTheModes_AndDistance_CountsTheNotesThatDiffer()
    {
        string[] order = [..AllScales.Where(x => x != Rmg.Core.Composition.Scales.HarmonicMinor).OrderByDescending(x => x.Brightness).Select(x => x.Name)];

        await Assert.That(order.SequenceEqual(["Lydian", "Major", "Mixolydian", "Dorian", "Natural minor", "Phrygian"])).IsTrue();
        await Assert.That(Rmg.Core.Composition.Scales.Major.Distance(Rmg.Core.Composition.Scales.NaturalMinor)).IsEqualTo(3);
        await Assert.That(Rmg.Core.Composition.Scales.HarmonicMinor.Distance(Rmg.Core.Composition.Scales.NaturalMinor)).IsEqualTo(1);
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task EveryPitchedNote_IsInItsSectionsScaleAndTheKey_WithTheStepsRaisedWhereTheyAre(int seed)
    {
        var song = TestCorpus.Get(seed).Song;
        var common = song.TrackEventStateTimelineMap.CommonStateTimelineMap;
        var offsets = common.GetStateTimeline(StateKinds.ScaleOffsets);
        var keys = common.GetStateTimeline(StateKinds.KeyOffset);
        var raisedSteps = common.GetStateTimeline(StateKinds.RaisedScaleSteps);
        var approaches = common.GetStateTimeline(StateKinds.ChordApproach);
        // a bass note leads into a change of chord in the beat before it
        var changes = TestCorpus.Get(seed).ChordChanges;
        var bassProgram = ((PitchInstrumentTrack)song.TrackDefinitions[6]).InstrumentCode;

        var tracks = Render.RenderSong(song).Tracks.Where(x => !x.IsPercussionInstrument).ToArray();

        await Assert.That(tracks.Sum(x => x.NoteTimeline.Count)).IsGreaterThan(0);
        foreach (var track in tracks)
        foreach (var note in track.NoteTimeline)
        {
            // a bass note leading into the next chord by a semitone leaves the scale on purpose
            var approach = (ChordApproach)approaches.GetEffectiveValueAt(note.Position);
            var isChromaticApproach = track.PitchInstrumentCode == bassProgram
                && changes.Any(x => x > note.Position && x <= note.Position + 1)
                && approach is ChordApproach.HalfStepBelow or ChordApproach.HalfStepAbove;
            if (isChromaticApproach)
                continue;

            var scale = Rmg.Core.Composition.Realizer.RaiseScaleSteps(
                offsets.GetEffectiveValueAt(note.Position),
                raisedSteps.GetEffectiveValueAt(note.Position)
            );
            var key = keys.GetEffectiveValueAt(note.Position);
            var pitchClasses = scale.Select(x => (x + key) % 12).ToHashSet();
            await Assert.That(pitchClasses).Contains(note.Value.Offset % 12).Because($"position {note.Position}");
        }
    }
}
