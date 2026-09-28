using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.Forms;

public sealed class SongEndingTest
{
    private const int MelodyTrack = 5;
    private const int BassTrack = 6;

    private static readonly IReadOnlyList<CorpusSong> Songs = TestCorpus.Range(40).ToArray();

    private static EndingSpan Ending(CorpusSong song) => song.Map.Ending;

    private static int Tonic(CorpusSong song) =>
        song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(0).GetStateValue(StateKinds.KeyOffset).Mod(12);

    [Test]
    public async Task ClosedEndings_LandOnTheTonic()
    {
        var closed = Songs.Where(x => Ending(x).Kind != EndingKind.Open).ToArray();
        var bassOnTonic = closed.Count(x => x.Notes(BassTrack)[^1].Value.Offset.Mod(12) == Tonic(x));
        var melodyOnTonic = closed.Count(x => x.Notes(MelodyTrack)[^1].Value.Offset.Mod(12) == Tonic(x));

        await Assert.That(closed.Length).IsGreaterThan(20);
        await Assert.That(bassOnTonic).IsEqualTo(closed.Length);
        await Assert.That(melodyOnTonic).IsEqualTo(closed.Length);
    }

    [Test]
    [Arguments(9, 7)]
    [Arguments(-2, 0)]
    [Arguments(11, 14)]
    [Arguments(-4, -7)]
    public async Task AMelodysLastNote_LandsOnTheRoot_InItsRegister(int step, int root)
    {
        var note = StateMap.FromStates([StateKinds.ScaleStep.CreateState(step), StateKinds.Velocity.CreateState(0.5)]).ToTimelineItem(0);
        var plain = StateMap.FromStates([StateKinds.Velocity.CreateState(0.5)]).ToTimelineItem(0);

        var landed = SongFormGenerator.LandOnRoot(note).Value;
        await Assert.That(landed.GetStateValue(StateKinds.ScaleStep)).IsEqualTo(root);
        await Assert.That(landed.GetStateValue(StateKinds.Velocity)).IsEqualTo(0.5);
        await Assert.That(SongFormGenerator.LandOnRoot(plain)).IsEqualTo(plain);
    }

    [Test]
    public async Task TheFinalChord_PlaysOnTheLine_AndIsHeld()
    {
        foreach (var song in Songs.Where(x => Ending(x).Kind != EndingKind.Open))
        {
            var bass = song.Notes(BassTrack)[^1];

            await Assert.That(bass.Position).IsEqualTo(Ending(song).Start);
            await Assert.That(bass.Value.Duration).IsEqualTo(Ending(song).Held);
            await Assert.That(song.Song.Duration).IsEqualTo(Ending(song).Start + Ending(song).Duration);
        }
    }

    [Test]
    public async Task Stops_SilenceTheBand_BeforeTheFinalChord()
    {
        var stops = Songs.Where(x => Ending(x).Kind == EndingKind.Stop).ToArray();
        foreach (var song in stops)
        {
            var from = Ending(song).Start - Ending(song).Stop;
            var sounding = song.Rendered.Tracks
                .SelectMany(x => x.NoteTimeline)
                .Where(x => x.Position < Ending(song).Start && x.Position + (x.Value.Duration > 0.25 ? x.Value.Duration : 0) > from + 1e-9)
                .ToArray();

            await Assert.That(sounding).IsEmpty();
        }

        await Assert.That(stops.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task Ritardandos_SlowTheBarBeforeTheEnding()
    {
        var slowing = Songs.Where(x => Ending(x).SlowsDown).ToArray();
        foreach (var song in slowing)
        {
            var tempo = song.Rendered.TempoTimeline;
            var before = tempo.GetEffectiveValueAt(Ending(song).Start - 5);

            await Assert.That(tempo.GetEffectiveValueAt(Ending(song).Start - 1)).IsLessThan(before);
            await Assert.That(tempo.GetEffectiveValueAt(Ending(song).Start)).IsEqualTo(before * FormLayers.Ritardando[^1]).Within(1e-9);
        }

        await Assert.That(slowing.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task WildSongs_EndOpenOrStoppedMoreOften()
    {
        double Share(double chanceScale)
        {
            var weights = FormLayers.WeighEndings(Tilt.Of(chanceScale, 1));
            return weights.Where(x => FormLayers.AdventurousEndings.Contains(x.Value)).Sum(x => x.Weight) / weights.Sum(x => x.Weight);
        }

        await Assert.That(Share(0.25)).IsLessThan(Share(1));
        await Assert.That(Share(4)).IsGreaterThan(Share(1));
    }

    [Test]
    public async Task EndingBar_HoldsEachPitchedTracksFirstNote_OnTheRoot()
    {
        var velocity = StateMap.FromStates([StateKinds.Velocity.CreateState(0.5)]);
        EventStateTimelineMap<StateMap> Track(params double[] positions) =>
            EventTimeline.Create(32, positions.Select(x => StateMap.Default.ToTimelineItem(x))).ToEventStateTimelineMap(velocity);
        var drum = DrumGroups.GetTrackNumber(DrumDefinitions.Kick);
        var section = new GeneratedSection(
            TrackEventStateTimelineMap.Create(
                32,
                [
                    new KeyValuePair<int, EventStateTimelineMap<StateMap>>(MelodyTrack, Track(1.5, 2, 5)),
                    new KeyValuePair<int, EventStateTimelineMap<StateMap>>(BassTrack, Track(6, 7)),
                    new KeyValuePair<int, EventStateTimelineMap<StateMap>>(drum, Track(0, 1, 2))
                ],
                StateMap.FromStates([StateKinds.ChordArrival.CreateState((int)ChordArrival.Third)]).ToStateTimelineMap(32)
            ),
            new RhythmicUnconventionality(0.5),
            FillGrooves.FromSource(ResolvedRhythm.DefaultState),
            0,
            false
        );

        var ending = SongFormGenerator.CreateEnding(
            section,
            [],
            8,
            8,
            new Dictionary<int, TrackRole> { [MelodyTrack] = TrackRole.Melody, [BassTrack] = TrackRole.Bass, [drum] = TrackRole.Drum }
        );
        var melody = ending.TrackTimelineMap[MelodyTrack].EventTimeline.Single();

        await Assert.That(ending.Duration).IsEqualTo(8);
        await Assert.That(melody.Position).IsEqualTo(0);
        await Assert.That(melody.Value.GetStateValue(StateKinds.HeldDuration)).IsEqualTo(8);
        await Assert.That(ending.TrackTimelineMap[drum].EventTimeline).IsEmpty();
        // the bass rests in the home bar, and plays its first note of the section
        await Assert.That(ending.TrackTimelineMap[BassTrack].EventTimeline.Single().Position).IsEqualTo(0);
        await Assert.That(ending.CommonStateTimelineMap.GetEffectiveStateMapAt(0).GetStateValue(StateKinds.ChordArrival))
            .IsEqualTo((int)ChordArrival.Root);
    }
}
