using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.Fills;

public sealed class FillArchetypeTest
{
    private const double Line = 8;
    private const double Grid = 0.25;

    private static readonly int Kick = DrumGroups.GetTrackNumber(DrumDefinitions.Kick);
    private static readonly int Tom = DrumGroups.GetTrackNumber(DrumDefinitions.Tom);
    private static readonly int HiHat = DrumGroups.GetTrackNumber(DrumDefinitions.HiHat);
    private static readonly int Cymbal = DrumGroups.GetTrackNumber(DrumDefinitions.Cymbal);

    private static IEnumerable<int> Seeds => Enumerable.Range(0, 12);

    private sealed record Setup(FillGenerator Generator, TrackEventStateTimelineMap<StateMap> Song, int Snare);

    /// <summary>A groove of kick on the beats, snare on the backbeats and hi-hat in 8ths, for three bars.</summary>
    private static Setup Create(int seed)
    {
        var context = new GenerationContext(seed);
        var tracks = SongTracks.Create(context, new RhythmicUnconventionality(0.5));
        // the fills play the snare itself, when the song has a sidestick too
        var snare = new[] { DrumDefinitions.AcousticSnare, DrumDefinitions.ElectricSnare, DrumDefinitions.Clap, DrumDefinitions.CrossStick }
            .Where(tracks.SongDrums.Contains)
            .Select(DrumGroups.GetTrackNumber)
            .First();
        EventStateTimelineMap<StateMap> Hits(IEnumerable<double> positions) =>
            EventTimeline.Create(12, positions.Select(x => StateMap.Default.ToTimelineItem(x))).ToEventStateTimelineMap(StateMap.Default);
        var song = TrackEventStateTimelineMap.Create(
            12,
            [
                new KeyValuePair<int, EventStateTimelineMap<StateMap>>(Kick, Hits(Enumerable.Range(0, 12).Select(x => (double)x))),
                new KeyValuePair<int, EventStateTimelineMap<StateMap>>(snare, Hits(Enumerable.Range(0, 6).Select(x => 2.0 * x + 1))),
                new KeyValuePair<int, EventStateTimelineMap<StateMap>>(HiHat, Hits(Enumerable.Range(0, 24).Select(x => 0.5 * x)))
            ],
            StateTimelineMap.Create(12)
        );
        return new Setup(new FillGenerator(context, tracks), song, snare);
    }

    private static TimelineItem<StateMap>[] Events(TrackEventStateTimelineMap<StateMap> song, int track, double from, double to) =>
        song.TrackTimelineMap.TryGetValue(track, out var timeline)
            ? timeline.EventTimeline.Where(x => x.Position >= from && x.Position < to).ToArray()
            : [];

    private static int Articulation(TimelineItem<StateMap> item) => item.Value.GetStateValue(StateKinds.ArticulationIndex);

    [Test]
    public async Task TomDown_RunsFromTheHighTomToTheFloorTom()
    {
        var toms = Enumerable.Range(0, 8).Select(x => FillGenerator.TomDown(x, 8)).ToArray();

        await Assert.That(toms[0]).IsEqualTo(FillLayers.TomCount);
        await Assert.That(toms[^1]).IsGreaterThanOrEqualTo(1);
        await Assert.That(toms.Zip(toms.Skip(1)).All(x => x.Second <= x.First)).IsTrue();
    }

    [Test]
    public async Task TomRun_RunsDownTheToms_OverTheKick()
    {
        foreach (var seed in Seeds)
        {
            var (generator, song, snare) = Create(seed);
            var filled = generator.ApplyFill(song, FillKind.TomRun, Line, Grid);
            var toms = Events(filled, Tom, 0, 12);
            var from = toms[0].Position;

            await Assert.That(toms.All(x => x.Position is >= 4 and < Line)).IsTrue();
            await Assert.That(toms.Select(Articulation).Zip(toms.Skip(1).Select(Articulation)).All(x => x.Second <= x.First)).IsTrue();
            // the hands leave the groove for the toms, the foot keeps the kick
            await Assert.That(Events(filled, HiHat, from, Line)).IsEmpty();
            await Assert.That(Events(filled, snare, from, Line)).IsEmpty();
            await Assert.That(Events(filled, Kick, from, Line).Length).IsEqualTo(Events(song, Kick, from, Line).Length);
            await Assert.That(Events(filled, HiHat, Line, 12).Length).IsEqualTo(Events(song, HiHat, Line, 12).Length);
        }
    }

    [Test]
    public async Task SnareRoll_SwellsIntoTheLine()
    {
        foreach (var seed in Seeds)
        {
            var (generator, song, snare) = Create(seed);
            var roll = Events(generator.ApplyFill(song, FillKind.SnareRoll, Line, Grid), snare, 4, Line)
                .Where(x => x.Value.GetStateValue(StateKinds.Velocity) != 0)
                .ToArray();
            var velocities = roll.Select(x => x.Value.GetStateValue(StateKinds.Velocity)).ToArray();

            await Assert.That(roll.Length).IsGreaterThan(1);
            await Assert.That(velocities.Zip(velocities.Skip(1)).All(x => x.Second >= x.First)).IsTrue();
        }
    }

    [Test]
    public async Task Break_SilencesTheDrums_UntilTheLine()
    {
        foreach (var seed in Seeds)
        {
            var (generator, song, snare) = Create(seed);
            var filled = generator.ApplyFill(song, FillKind.Break, Line, Grid);
            var silent = new[] { Kick, snare, HiHat }.Select(x => Events(filled, x, Line - 1, Line).Length).Sum();

            await Assert.That(silent).IsEqualTo(0);
            await Assert.That(Events(filled, Kick, 0, 4).Length).IsEqualTo(4);
            await Assert.That(Events(filled, Kick, Line, 12).Length).IsEqualTo(4);
        }
    }

    [Test]
    public async Task StopTime_HitsTogetherOnce_ThenStops()
    {
        foreach (var seed in Seeds)
        {
            var (generator, song, snare) = Create(seed);
            var filled = generator.ApplyFill(song, FillKind.StopTime, Line, Grid);
            // the crash is the fill's own, where the drums stop
            var from = Events(filled, Cymbal, 0, 12).Single().Position;
            var hits = new[] { Kick, snare, HiHat, Cymbal }.SelectMany(x => Events(filled, x, from, Line).Select(e => (x, e.Position))).ToArray();

            await Assert.That(hits.All(x => x.Position.IsEqualToByEpsilon(from))).IsTrue();
            await Assert.That(hits.Select(x => x.x).ToHashSet().SetEquals([Kick, snare, Cymbal])).IsTrue();
        }
    }

    [Test]
    public async Task Lift_OpensTheHiHat_OnTheLastOffBeat()
    {
        var (generator, song, _) = Create(1);
        var hiHat = Events(generator.ApplyFill(song, FillKind.Lift, Line, Grid), HiHat, Line - 0.5, Line).Single();

        await Assert.That(Articulation(hiHat)).IsEqualTo(FillLayers.OpenHiHat);
    }

    [Test]
    public async Task Pickup_PlaysOverTheGroove()
    {
        foreach (var seed in Seeds)
        {
            var (generator, song, _) = Create(seed);
            var filled = generator.ApplyFill(song, FillKind.Pickup, Line, Grid);

            await Assert.That(Events(filled, HiHat, 0, 12).Length).IsEqualTo(Events(song, HiHat, 0, 12).Length);
            await Assert.That(Events(filled, Kick, 0, 12).Length).IsEqualTo(Events(song, Kick, 0, 12).Length);
        }
    }
}
