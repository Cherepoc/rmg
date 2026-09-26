using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.VoiceLeading;

public sealed class VoiceLeaderTest
{
    // C4 to B5, about the range of a chord track
    private const int MinNote = 48;
    private const int MaxNote = 83;

    private static readonly ImmutableArray<int> CMajor = [60, 64, 67];
    private static readonly ImmutableArray<int> FMajor = [65, 69, 72];

    private static VoiceLeader CreateLeader() => new(MinNote, MaxNote, notes => notes);

    [Test]
    public async Task Smooth_CToF_HoldsTheCAndMovesTheOthersAStepOrTwo()
    {
        var result = VoiceLeader.Choose(FMajor, false, CMajor, 5, 1, MinNote, MaxNote);

        await Assert.That(result!.Value.ToArray()).IsEquivalentTo([60, 65, 69]);
    }

    [Test]
    public async Task Parallel_CToF_SlidesTheWholeShapeWithTheRoot()
    {
        var result = VoiceLeader.Choose(FMajor, false, CMajor, 5, 0, MinNote, MaxNote);

        await Assert.That(result!.Value.ToArray()).IsEquivalentTo([65, 69, 72]);
    }

    [Test]
    public async Task Parallel_TakesTheShorterWayRound()
    {
        // C to G is up a fifth or down a fourth; the shape slides down
        ImmutableArray<int> gMajor = [67, 71, 74];

        var result = VoiceLeader.Choose(gMajor, false, CMajor, 7, 0, MinNote, MaxNote);

        await Assert.That(result!.Value.ToArray()).IsEquivalentTo([55, 59, 62]);
    }

    [Test]
    public async Task FixedVoicing_MovesOnlyByOctaves()
    {
        // a quartal stack keeps its layout
        ImmutableArray<int> quartal = [62, 67, 72, 77];

        var result = VoiceLeader.Choose(quartal, true, [48, 52, 55], 2, 1, MinNote, MaxNote);

        await Assert.That(result!.Value.Select(x => x - result.Value[0]).ToArray()).IsEquivalentTo([0, 5, 10, 15]);
        await Assert.That((result.Value[0] - 62) % 12).IsEqualTo(0);
    }

    [Test]
    public async Task Layouts_AreTheInversionsInEveryOctaveThatFits()
    {
        var layouts = VoiceLeader.GetLayouts(CMajor, false, MinNote, MaxNote).ToArray();

        await Assert.That(layouts.All(x => x.All(note => note is >= MinNote and <= MaxNote))).IsTrue();
        // every inversion keeps the notes of the chord
        await Assert.That(layouts.All(x => x.Select(n => n % 12).Order().SequenceEqual(new[] { 0, 4, 7 }))).IsTrue();
        await Assert.That(layouts.Select(x => (x[0] % 12)).Distinct().Order().ToArray()).IsEquivalentTo([0, 4, 7]);
    }

    [Test]
    public async Task ChordWiderThanTheRange_HasNoLayout_AndPlaysAsDrawn()
    {
        ImmutableArray<int> wide = [48, 60, 72, 84, 96];

        await Assert.That(VoiceLeader.Choose(wide, false, CMajor, 0, 1, 60, 71)).IsNull();

        var leader = new VoiceLeader(60, 71, notes => [..notes.Select(x => x + 1000)]);
        leader.Place(CMajor, 60, false, 1, 0);
        await Assert.That(leader.Place(wide, 48, false, 1, 0)[0]).IsEqualTo(1048);
    }

    [Test]
    public async Task FirstChord_PlaysAsDrawn_AndTheSameChordAgainKeepsItsLayout()
    {
        var leader = CreateLeader();

        var first = leader.Place(CMajor, 60, false, 1, 0);
        var f = leader.Place(FMajor, 65, false, 1, 0);
        var fAgain = leader.Place(FMajor, 65, false, 1, 0);

        await Assert.That(first).IsEqualTo(CMajor);
        await Assert.That(f.ToArray()).IsEquivalentTo([60, 65, 69]);
        await Assert.That(fAgain).IsEqualTo(f);
    }

    [Test]
    public async Task Reset_PlaysTheBarsFirstChordAsDrawn_AndLeadsTheRest()
    {
        var leader = CreateLeader();
        leader.Place(CMajor, 60, false, 1, 0);

        var reset = leader.Place(FMajor, 65, false, 1, 2);
        var afterInSameBar = leader.Place(CMajor, 60, false, 1, 2);

        await Assert.That(reset).IsEqualTo(FMajor);
        // C after F-A-C holds the C and moves the others down a step or two
        await Assert.That(afterInSameBar.ToArray()).IsEquivalentTo([64, 67, 72]);
    }

    [Test]
    public async Task LongProgression_DoesNotDriftToAnEdge()
    {
        // round the circle of fifths again and again, always falling a fifth: without a pull the chords would creep
        var leader = CreateLeader();
        var centre = (MinNote + MaxNote) / 2.0;
        var root = 60;
        var middles = new List<double>();
        for (var i = 0; i < 120; i++)
        {
            ImmutableArray<int> chord = [root, root + 4, root + 7];
            middles.Add(leader.Place(chord, root, false, 1, 0).Average());
            root = (root - 7 - 48) % 12 + 60;
        }

        await Assert.That(middles.All(x => Math.Abs(x - centre) < 12)).IsTrue();
    }

    [Test]
    public async Task Songs_ChordTracksRarelyJumpRegister()
    {
        int changes = 0, jumps = 0;
        for (var seed = 0; seed < 20; seed++)
        {
            var song = SongGenerator.GenerateSong(seed);
            var instrument = ((PitchInstrumentTrack)song.TrackDefinitions[4]).InstrumentCode;
            var chords = Render.RenderSong(song).Tracks
                .First(x => !x.IsPercussionInstrument && x.PitchInstrumentCode == instrument)
                .NoteTimeline.GroupBy(x => x.Position)
                .Select(x => x.Select(note => note.Value.Offset).ToArray())
                .Where(x => x.Length >= 2)
                .ToArray();
            foreach (var (a, b) in chords.Zip(chords.Skip(1)))
            {
                changes++;
                if (Math.Abs(b.Average() - a.Average()) >= 9)
                    jumps++;
            }
        }

        // only a bar that starts afresh jumps, by design
        await Assert.That(jumps / (double)changes).IsLessThan(0.05);
    }

    [Test]
    public async Task Smoothness_FollowsTheChordInstrument_AndVariesBySection()
    {
        var byInstrument = new Dictionary<double, List<double>>();
        var songsVaryingBySection = 0;
        for (var seed = 0; seed < 60; seed++)
        {
            var song = SongGenerator.GenerateSong(seed);
            var definition = (PitchInstrumentTrack)song.TrackDefinitions[4];
            var instrument = Rmg.Core.Composition.InstrumentRoles.Chords.Instruments.Single(x => x.Program == definition.InstrumentCode);
            // a section's smoothness is the track's state along the section, not a note's own
            var smoothness = song.TrackEventStateTimelineMap.TrackTimelineMap[4].StateTimelineMap
                .GetStateTimeline(StateKinds.VoiceLeading)
                .Select(x => x.Value)
                .Distinct()
                .ToArray();
            var songSmoothness = definition.StateMap.GetStateValue(StateKinds.VoiceLeading);

            if (!byInstrument.TryGetValue(instrument.VoiceLeading, out var list))
                byInstrument[instrument.VoiceLeading] = list = [];
            list.Add(songSmoothness);
            if (smoothness.Length > 1)
                songsVaryingBySection++;
        }

        await Assert.That(byInstrument[VoiceLeadingLayers.Sustained].Average()).IsGreaterThan(byInstrument[VoiceLeadingLayers.Guitar].Average() + 0.3);
        await Assert.That(songsVaryingBySection).IsGreaterThan(50);
    }
}
