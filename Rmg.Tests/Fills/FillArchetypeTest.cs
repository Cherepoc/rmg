using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.Fills;

public sealed class FillArchetypeTest
{
    private const double Line = 8;
    private const double MinNote = 0.2;

    private static readonly int Kick = DrumGroups.GetTrackNumber(DrumDefinitions.Kick);
    private static readonly int Tom = DrumGroups.GetTrackNumber(DrumDefinitions.Tom);
    private static readonly int HiHat = DrumGroups.GetTrackNumber(DrumDefinitions.HiHat);
    private static readonly int Cymbal = DrumGroups.GetTrackNumber(DrumDefinitions.Cymbal);

    private static IEnumerable<int> Seeds => Enumerable.Range(0, 12);

    private static readonly Drummer Middle = new(0.5, FillKind.None);

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
        return new Setup(new FillGenerator(context, tracks, new RhythmicUnconventionality(0.5)), song, snare);
    }

    private static TimelineItem<StateMap>[] Events(TrackEventStateTimelineMap<StateMap> song, int track, double from, double to) =>
        song.TrackTimelineMap.TryGetValue(track, out var timeline)
            ? timeline.EventTimeline.Where(x => x.Position >= from && x.Position < to).ToArray()
            : [];

    private static int Articulation(TimelineItem<StateMap> item) => item.Value.GetStateValue(StateKinds.ArticulationIndex);

    [Test]
    public async Task EveryFill_HasASpec()
    {
        var kinds = Enum.GetValues<FillKind>().Where(x => x != FillKind.None).ToHashSet();

        await Assert.That(FillLayers.Specs.Keys.ToHashSet().SetEquals(kinds)).IsTrue();
        await Assert.That(FillLayers.Specs.Values.All(x => x.Spans.Length > 0)).IsTrue();
    }

    [Test]
    public async Task NamedSounds_AreTheDrumsOwn()
    {
        var toms = DrumSounds.TomsHighToLow.Select(DrumDefinitions.Tom.GetArticulationIndex).ToArray();

        // the toms' sounds are listed from the low floor tom up, so the run down them counts down
        await Assert.That(toms).IsEquivalentTo(Enumerable.Range(1, toms.Length).Reverse().ToArray());
        await Assert.That(DrumDefinitions.HiHat.ArticulationCodes).Contains(DrumSounds.OpenHiHat);
        await Assert.That(FillLayers.Crashes.All(x => DrumDefinitions.Cymbal.ArticulationCodes.Contains(x.Value))).IsTrue();
        await Assert.That(() => DrumDefinitions.Tom.GetArticulationIndex(DrumSounds.OpenHiHat)).Throws<ArgumentException>();
    }

    [Test]
    public async Task TomRun_RunsDownTheToms_OverTheKick()
    {
        foreach (var seed in Seeds)
        {
            var (generator, song, snare) = Create(seed);
            var filled = generator.ApplyFill(song, FillPlay.Plain(FillKind.TomRun), Line, ResolvedRhythm.Default, MinNote, Middle);
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
        var rolls = Seeds.Select(seed =>
            {
                var (generator, song, snare) = Create(seed);
                return Velocities(generator.ApplyFill(song, FillPlay.Plain(FillKind.SnareRoll), Line, ResolvedRhythm.Default, MinNote, Middle), snare);
            }
        ).ToArray();

        await Assert.That(rolls.All(x => x.Length > 1)).IsTrue();
        await Assert.That(rolls.Average(Swell)).IsGreaterThan(0.2);
    }

    /// <summary>The velocities of a track's fill notes before the line.</summary>
    private static double[] Velocities(TrackEventStateTimelineMap<StateMap> song, int track) =>
        Events(song, track, 4, Line).Select(x => x.Value.GetStateValue(StateKinds.Velocity)).Where(x => x != 0).ToArray();

    /// <summary>How much louder the second half of a fill's notes is than the first; each note is accented by its rank.</summary>
    private static double Swell(double[] velocities) =>
        velocities[(velocities.Length / 2)..].Average() - velocities[..(velocities.Length / 2)].Average();

    [Test]
    public async Task Break_SilencesTheDrums_UntilTheLine()
    {
        foreach (var seed in Seeds)
        {
            var (generator, song, snare) = Create(seed);
            var filled = generator.ApplyFill(song, FillPlay.Plain(FillKind.Break), Line, ResolvedRhythm.Default, MinNote, Middle);
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
            var filled = generator.ApplyFill(song, FillPlay.Plain(FillKind.StopTime), Line, ResolvedRhythm.Default, MinNote, Middle);
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
        var hiHat = Events(generator.ApplyFill(song, FillPlay.Plain(FillKind.Lift), Line, ResolvedRhythm.Default, MinNote, Middle), HiHat, Line - 0.5, Line).Single();

        await Assert.That(Articulation(hiHat)).IsEqualTo(DrumDefinitions.HiHat.GetArticulationIndex(DrumSounds.OpenHiHat));
    }

    [Test]
    public async Task Pickup_PlaysOverTheGroove()
    {
        foreach (var seed in Seeds)
        {
            var (generator, song, _) = Create(seed);
            var filled = generator.ApplyFill(song, FillPlay.Plain(FillKind.Pickup), Line, ResolvedRhythm.Default, MinNote, Middle);

            await Assert.That(Events(filled, HiHat, 0, 12).Length).IsEqualTo(Events(song, HiHat, 0, 12).Length);
            await Assert.That(Events(filled, Kick, 0, 12).Length).IsEqualTo(Events(song, Kick, 0, 12).Length);
        }
    }

    private static FillPlay Twisted(FillKind kind, FillTwist twists, int tuplet = 1, OddVoice? oddVoice = null) =>
        new(kind, twists, tuplet, oddVoice);

    private static int[] Toms(TrackEventStateTimelineMap<StateMap> song) => Events(song, Tom, 0, 12).Select(Articulation).ToArray();

    [Test]
    public async Task Tuplet_PlaysTheRunOnTheTuplet()
    {
        var toms = Seeds.SelectMany(seed =>
            {
                var (generator, song, _) = Create(seed);
                return Events(generator.ApplyFill(song, Twisted(FillKind.TomRun, FillTwist.Tuplet, 3), Line, ResolvedRhythm.Default, MinNote, Middle), Tom, 0, 12);
            }
        ).ToArray();

        await Assert.That(toms.All(x => Math.Abs(x.Position * 6 - Math.Round(x.Position * 6)) < 1e-6)).IsTrue();
        await Assert.That(toms.Any(x => Math.Abs(x.Position * 4 - Math.Round(x.Position * 4)) > 1e-6)).IsTrue();
    }

    [Test]
    public async Task Upward_RunsUpTheToms_AndZigzag_TakesTheHighAndLowInTurn()
    {
        foreach (var seed in Seeds)
        {
            var (generator, song, _) = Create(seed);
            var up = Toms(generator.ApplyFill(song, Twisted(FillKind.TomRun, FillTwist.Upward), Line, ResolvedRhythm.Default, MinNote, Middle));
            var zigzag = Events(generator.ApplyFill(song, Twisted(FillKind.TomRun, FillTwist.Zigzag), Line, ResolvedRhythm.Default, MinNote, Middle), Tom, 0, 12);

            await Assert.That(up.Zip(up.Skip(1)).All(x => x.Second >= x.First)).IsTrue();
            // the high toms are the last three of the six, and the run's notes take them and the low ones in turn
            await Assert.That(zigzag.Length).IsGreaterThan(1);
            await Assert.That(zigzag.Select((x, i) => (Articulation(x) > 3) == (i % 2 == 0)).All(x => x)).IsTrue();
        }
    }

    [Test]
    public async Task SlowDown_PlaysTheFinerNotesFirst()
    {
        foreach (var seed in Seeds)
        {
            var (generator, song, snare) = Create(seed);
            var roll = Events(generator.ApplyFill(song, Twisted(FillKind.SnareRoll, FillTwist.SlowDown), Line, ResolvedRhythm.Default, MinNote, Middle), snare, 4, Line)
                .Where(x => x.Value.GetStateValue(StateKinds.Velocity) != 0)
                .ToArray();
            var middle = (roll[0].Position + Line) / 2;

            // the second half keeps to 8ths
            await Assert.That(roll.Where(x => x.Position >= middle).All(x => Math.Abs(x.Position * 2 - Math.Round(x.Position * 2)) < 1e-6)).IsTrue();
        }
    }

    [Test]
    public async Task OddVoice_PlaysTheRunOnTheKick()
    {
        foreach (var seed in Seeds)
        {
            var (generator, song, _) = Create(seed);
            var filled = generator.ApplyFill(song, Twisted(FillKind.TomRun, FillTwist.OddVoice, oddVoice: OddVoice.Kick), Line, ResolvedRhythm.Default, MinNote, Middle);

            await Assert.That(Events(filled, Tom, 0, 12)).IsEmpty();
            await Assert.That(Events(filled, Kick, 4, Line).Length).IsGreaterThan(Events(song, Kick, 4, Line).Length);
        }
    }

    [Test]
    public async Task Fading_FadesTheRoll()
    {
        var rolls = Seeds.Select(seed =>
            {
                var (generator, song, snare) = Create(seed);
                return Velocities(generator.ApplyFill(song, Twisted(FillKind.SnareRoll, FillTwist.Fading), Line, ResolvedRhythm.Default, MinNote, Middle), snare);
            }
        ).ToArray();

        await Assert.That(rolls.Average(Swell)).IsLessThan(-0.2);
    }

    [Test]
    public async Task Gappy_LeavesMoreNotesOut()
    {
        int plain = 0, gappy = 0;
        foreach (var seed in Seeds)
        {
            var (generator, song, _) = Create(seed);
            plain += Events(generator.ApplyFill(song, FillPlay.Plain(FillKind.TomRun), Line, ResolvedRhythm.Default, MinNote, Middle), Tom, 0, 12).Length;
            gappy += Events(generator.ApplyFill(song, Twisted(FillKind.TomRun, FillTwist.Gappy), Line, ResolvedRhythm.Default, MinNote, Middle), Tom, 0, 12).Length;
        }

        await Assert.That(gappy).IsLessThan(plain);
    }

    [Test]
    public async Task OddSpan_StartsTheFillOffTheBeat()
    {
        foreach (var seed in Seeds)
        {
            var (generator, song, _) = Create(seed);
            var first = Events(generator.ApplyFill(song, Twisted(FillKind.TomRun, FillTwist.OddSpan), Line, ResolvedRhythm.Default, MinNote, Middle), Tom, 0, 12)[0];

            await Assert.That(first.Position % 1).IsEqualTo(0.5).Within(1e-9);
        }
    }
}
