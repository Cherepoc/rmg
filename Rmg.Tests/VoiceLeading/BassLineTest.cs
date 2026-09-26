using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.VoiceLeading;

public sealed class BassLineTest
{
    // C1 to B2, the bass's range
    private const int MinNote = 24;
    private const int MaxNote = 47;

    private static readonly ImmutableArray<int> Major = [0, 2, 4, 5, 7, 9, 11];

    /// <summary>The chord on a step of C major, its root in the octave from C2.</summary>
    private static ChordContext ChordOn(int rootStep) =>
        new(step => 36 + Major[(rootStep + step).Mod(7)] + 12 * (int)Math.Floor((rootStep + step) / 7.0));

    private static readonly ChordContext C = ChordOn(0);
    private static readonly ChordContext F = ChordOn(3);
    private static readonly ChordContext G = ChordOn(4);

    private static BassLine CreateLine() => new(MinNote, MaxNote, note => note);

    [Test]
    [Arguments(40, 38, 40)]
    [Arguments(40, 47, 40)]
    [Arguments(41, 26, 29)]
    [Arguments(36, 42, 36)]
    public async Task Notes_TakeTheOctaveNearestTheNoteBefore(int note, int previous, int expected)
    {
        await Assert.That(CreateLine().GetNearest(note, previous)).IsEqualTo(expected);
    }

    [Test]
    public async Task FirstNoteOfABar_LandsOnTheRootThirdOrFifth_AsTheBarAsks()
    {
        // from C2, F's root is a fourth up, its third A is nearest below, and its fifth C stays
        foreach (var (arrival, expected) in new[] { (ChordArrival.Root, 41), (ChordArrival.Third, 33), (ChordArrival.Fifth, 36) })
        {
            var line = CreateLine();
            line.Place(36, C, 0, F, 4, ChordArrival.Root, ChordApproach.None);
            var placed = line.Place(38, F, 4, null, null, arrival, ChordApproach.None);
            await Assert.That(placed).IsEqualTo(expected).Because(arrival.ToString());
        }
    }

    [Test]
    public async Task FreeArrival_KeepsTheLinesNote()
    {
        var line = CreateLine();
        line.Place(36, C, 0, F, 4, ChordArrival.Root, ChordApproach.None);

        await Assert.That(line.Place(38, F, 4, null, null, ChordArrival.Free, ChordApproach.None)).IsEqualTo(38);
    }

    [Test]
    [Arguments(ChordApproach.HalfStepBelow, 40)]
    [Arguments(ChordApproach.HalfStepAbove, 42)]
    [Arguments(ChordApproach.ScaleStep, 40)]
    [Arguments(ChordApproach.Fifth, 36)]
    [Arguments(ChordApproach.Anticipation, 41)]
    public async Task LastBeatBeforeTheBarLine_LeadsIntoTheNextRoot(ChordApproach approach, int expected)
    {
        // C, then E in the last beat, then F: from below, so a step leads in from E
        var line = CreateLine();
        line.Place(36, C, 0, C, 3, ChordArrival.Root, ChordApproach.None);

        var placed = line.Place(40, C, 3, F, 4, ChordArrival.Root, approach);

        await Assert.That(placed).IsEqualTo(expected);
    }

    [Test]
    public async Task ScaleStep_FromAbove_LeadsInFromTheStepAbove()
    {
        // coming down from A to G, the step above G is A
        var line = CreateLine();
        line.Place(45, ChordOn(5), 0, ChordOn(5), 3.5, ChordArrival.Root, ChordApproach.None);

        await Assert.That(line.Place(45, ChordOn(5), 3.5, G, 4, ChordArrival.Root, ChordApproach.ScaleStep)).IsEqualTo(45);
    }

    [Test]
    public async Task EarlierInTheBar_OrBeforeANoteInTheSameBar_DoesNotLeadIn()
    {
        var line = CreateLine();
        line.Place(36, C, 0, C, 2, ChordArrival.Root, ChordApproach.None);

        // beat 3 is not the last beat, and a note in the last beat followed by another in the same bar is not either
        await Assert.That(line.Place(40, C, 2, F, 4, ChordArrival.Root, ChordApproach.HalfStepBelow)).IsEqualTo(40);
        await Assert.That(BassLine.IsInLastBeatBeforeChange(3, 3.5)).IsFalse();
        await Assert.That(BassLine.IsInLastBeatBeforeChange(3, 8)).IsTrue();
    }

    [Test]
    public async Task FirstNote_PlaysAsDrawn()
    {
        var line = new BassLine(MinNote, MaxNote, note => note + 1000);

        await Assert.That(line.Place(36, C, 0, null, null, ChordArrival.Root, ChordApproach.None)).IsEqualTo(1036);
    }

    [Test]
    public async Task Songs_BassLinesMoveByLittle()
    {
        // the unit tests above check where the notes land; across whole songs the line keeps to small steps
        var leaps = new List<int>();
        for (var seed = 0; seed < 20; seed++)
        {
            var song = SongGenerator.GenerateSong(seed);
            var program = ((PitchInstrumentTrack)song.TrackDefinitions[6]).InstrumentCode;
            var notes = Render.RenderSong(song).Tracks.First(x => !x.IsPercussionInstrument && x.PitchInstrumentCode == program)
                .NoteTimeline.ToArray();
            leaps.AddRange(notes.Zip(notes.Skip(1)).Select(x => Math.Abs(x.Second.Value.Offset - x.First.Value.Offset)));
        }

        await Assert.That(leaps.Count).IsGreaterThan(0);
        await Assert.That(leaps.Average()).IsLessThan(4);
        await Assert.That(leaps.Count(x => x > 7) / (double)leaps.Count).IsLessThan(0.05);
    }

    [Test]
    public async Task Leading_FollowsTheBassInstrument()
    {
        var byLeading = new Dictionary<double, List<double>>();
        for (var seed = 0; seed < 80; seed++)
        {
            var song = SongGenerator.GenerateSong(seed);
            var program = ((PitchInstrumentTrack)song.TrackDefinitions[6]).InstrumentCode;
            var instrument = Rmg.Core.Composition.InstrumentRoles.Bass.Instruments.Single(x => x.Program == program);
            var approaches = song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.ChordApproach);
            var bars = (int)(song.Duration / 4);
            var leading = Enumerable.Range(0, bars).Count(x => approaches.GetEffectiveValueAt(x * 4.0) != 0) / (double)bars;

            if (!byLeading.TryGetValue(instrument.Leading, out var list))
                byLeading[instrument.Leading] = list = [];
            list.Add(leading);
        }

        await Assert.That(byLeading[BassLeadingLayers.Walking].Average()).IsGreaterThan(byLeading[BassLeadingLayers.Plain].Average() + 0.15);
    }
}
