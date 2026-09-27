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

    private static readonly Drummer Middle = new(0.5, FillPath.OneWay);

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

    /// <summary>The song's sounds of a role, in the order of their note numbers.</summary>
    private static RunSound[] SoundsOf(FillGenerator generator, DrumRole role) =>
        generator.Sounds.Sounds.TryGetValue(role, out var sounds) ? [..sounds.OrderBy(x => x.Code)] : [];

    private static FillRun Run(IEnumerable<RunSound> sounds, FillPath path = FillPath.OneWay, int width = 1, double fullness = 0.9) =>
        new([..sounds], path, width, fullness, false);

    private static FillPlay Play(FillRun run, FillTwist twists = FillTwist.None, int tuplet = 1) => new(FillKind.Run, twists, tuplet, run);

    private static TrackEventStateTimelineMap<StateMap> ApplyRun(Setup setup, FillRun run, FillTwist twists = FillTwist.None, int tuplet = 1) =>
        setup.Generator.ApplyFill(setup.Song, Play(run, twists, tuplet), Line, ResolvedRhythm.Default, MinNote, Middle);

    private static FillRun SnareRun(Setup setup) => Run([SoundsOf(setup.Generator, DrumRole.Snare).First(x => x.Track == setup.Snare)]);

    /// <summary>The velocities of a track's fill notes before the line.</summary>
    private static double[] Velocities(TrackEventStateTimelineMap<StateMap> song, int track) =>
        Events(song, track, 4, Line).Select(x => x.Value.GetStateValue(StateKinds.Velocity)).Where(x => x != 0).ToArray();

    /// <summary>How much louder the second half of a fill's notes is than the first; each note is accented by its rank.</summary>
    private static double Swell(double[] velocities) =>
        velocities[(velocities.Length / 2)..].Average() - velocities[..(velocities.Length / 2)].Average();

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
        // the toms' note numbers rise with their pitch, which a run's order of pitch goes by
        await Assert.That(DrumDefinitions.Tom.ArticulationCodes.Order().ToArray()).IsEquivalentTo(DrumSounds.TomsHighToLow.Reverse().ToArray());
        await Assert.That(DrumDefinitions.HiHat.ArticulationCodes).Contains(DrumSounds.OpenHiHat);
        await Assert.That(FillLayers.Crashes.All(x => DrumDefinitions.Cymbal.ArticulationCodes.Contains(x.Value))).IsTrue();
        await Assert.That(() => DrumDefinitions.Tom.GetArticulationIndex(DrumSounds.OpenHiHat)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Sounds_AreEverySoundOfTheSongsDrums()
    {
        var (generator, _, _) = Create(1);
        var toms = SoundsOf(generator, DrumRole.Toms);

        await Assert.That(toms.Select(x => x.Code).ToArray()).IsEquivalentTo(DrumDefinitions.Tom.ArticulationCodes.Order().ToArray());
        await Assert.That(toms.All(x => x.Track == Tom)).IsTrue();
        await Assert.That(SoundsOf(generator, DrumRole.Snare)).IsNotEmpty();
    }

    [Test]
    public async Task Run_DownTheToms_ClearsThem_AndTheGroovePlaysOn()
    {
        foreach (var seed in Seeds)
        {
            var setup = Create(seed);
            var filled = ApplyRun(setup, Run(SoundsOf(setup.Generator, DrumRole.Toms).Reverse()));
            var toms = Events(filled, Tom, 0, 12);

            await Assert.That(toms.All(x => x.Position is >= 4 and < Line)).IsTrue();
            // down the toms, whose sounds count up with their pitch
            await Assert.That(toms.Select(Articulation).Zip(toms.Skip(1).Select(Articulation)).All(x => x.Second <= x.First)).IsTrue();
            // the drums the run does not play keep the groove
            foreach (var track in new[] { Kick, setup.Snare, HiHat })
                await Assert.That(Events(filled, track, 0, 12).Length).IsEqualTo(Events(setup.Song, track, 0, 12).Length);
        }
    }

    [Test]
    public async Task Run_OnTheSnareAlone_IsARollThatSwells_InPlaceOfTheSnaresGroove()
    {
        var rolls = Seeds.Select(seed =>
            {
                var setup = Create(seed);
                var filled = ApplyRun(setup, SnareRun(setup));
                var roll = Velocities(filled, setup.Snare);
                // the snare's own notes are left out where the roll plays
                var first = Events(filled, setup.Snare, 4, Line).First(x => x.Value.GetStateValue(StateKinds.Velocity) != 0).Position;
                return (Velocities: roll, IsTheRollAlone: Events(filled, setup.Snare, first, Line).Length == Velocities(filled, setup.Snare).Length);
            }
        ).ToArray();

        await Assert.That(rolls.All(x => x.Velocities.Length > 1 && x.IsTheRollAlone)).IsTrue();
        await Assert.That(rolls.Average(x => Swell(x.Velocities))).IsGreaterThan(0.2);
    }

    [Test]
    public async Task Run_PlaysTheGroovesCycle_ARankFiner()
    {
        // a backbeat: a cycle of two beats, its strongest note on the second, down to quarters
        var backbeat = new ResolvedRhythm(0.5, 0.25, 1, 0, 0.5, 0.5);
        foreach (var seed in Seeds)
        {
            var setup = Create(seed);
            var run = Run([SoundsOf(setup.Generator, DrumRole.Toms)[0]], fullness: 0.95);
            var filled = setup.Generator.ApplyFill(setup.Song, Play(run), Line, backbeat, MinNote, Middle);
            var toms = Events(filled, Tom, 0, 12);

            // 8ths, a rank finer than the backbeat's quarters
            await Assert.That(toms.All(x => Math.Abs(x.Position * 2 - Math.Round(x.Position * 2)) < 1e-6)).IsTrue();
        }
    }

    [Test]
    public async Task FillRhythm_KeepsThePhase_AndTheTempoLimitsTheFinestNotes()
    {
        var backbeat = new ResolvedRhythm(0.5, 0.25, 1, 0, 0.5, 0.5);
        var rhythm = FillRhythm.Of(backbeat, 2, 1, 0.2);
        var notes = rhythm.Play(new GenerationContext(1), 1, _ => 1, Line, 4, Line, rhythm.MaxRank);

        // 16ths, and at a limit of 8ths, 8ths
        await Assert.That(rhythm.Fine).IsEqualTo(0.25);
        await Assert.That(FillRhythm.Of(backbeat, 2, 1, 0.5).Fine).IsEqualTo(0.5);
        // the strongest notes on the backbeats
        await Assert.That(notes.Where(x => x.Rank == 0).Select(x => x.Position).ToArray()).IsEquivalentTo([5.0, 7.0]);
    }

    [Test]
    public async Task Walks_GoOneWay_Turn_Loop_OrStepAtRandom()
    {
        var context = new GenerationContext(1);

        await Assert.That(FillSounds.Walk(context, FillPath.OneWay, 3, 6)).IsEquivalentTo([0, 0, 1, 1, 2, 2]);
        await Assert.That(FillSounds.Walk(context, FillPath.Turn, 3, 5)).IsEquivalentTo([0, 1, 2, 1, 0]);
        await Assert.That(FillSounds.Walk(context, FillPath.Loop, 3, 7)).IsEquivalentTo([0, 1, 2, 0, 1, 2, 0]);
        await Assert.That(FillSounds.Walk(context, FillPath.Random, 1, 4)).IsEquivalentTo([0, 0, 0, 0]);

        var random = FillSounds.Walk(context, FillPath.Random, 6, 2_000);
        var steps = random.Zip(random.Skip(1), (a, b) => Math.Abs(b - a)).ToArray();
        await Assert.That(random.All(x => x is >= 0 and < 6)).IsTrue();
        await Assert.That(steps.Count(x => x == 1) / (double)steps.Length).IsGreaterThan(FillLayers.NeighbourStepChance * 0.9);
    }

    [Test]
    public async Task AWiderWindow_PlaysSeveralSoundsTogether()
    {
        foreach (var seed in Seeds)
        {
            var setup = Create(seed);
            var tom = SoundsOf(setup.Generator, DrumRole.Toms)[0];
            var snare = SoundsOf(setup.Generator, DrumRole.Snare).First(x => x.Track == setup.Snare);
            var filled = ApplyRun(setup, Run([tom, snare], FillPath.Loop, 2));
            var toms = Events(filled, Tom, 0, 12).Select(x => x.Position).ToArray();

            // the snare with a tom, a drum a sound at a time
            await Assert.That(toms).IsNotEmpty();
            await Assert.That(toms.All(x => Events(filled, setup.Snare, x, x + 1e-6).Length == 1)).IsTrue();
        }
    }

    [Test]
    public async Task ACymbal_PlaysOnlyTheStrongNotes()
    {
        foreach (var seed in Seeds)
        {
            var setup = Create(seed);
            var crash = SoundsOf(setup.Generator, DrumRole.Cymbal).First(x => x.Code == DrumSounds.CrashCymbal1);
            var filled = ApplyRun(setup, Run([crash, SoundsOf(setup.Generator, DrumRole.Toms)[0]], FillPath.Loop, 2));

            // a cycle of a beat down to 16ths, of which the crash keeps to the 8ths
            await Assert.That(Events(filled, Cymbal, 0, 12).All(x => Math.Abs(x.Position * 2 - Math.Round(x.Position * 2)) < 1e-6)).IsTrue();
            await Assert.That(Events(filled, Tom, 0, 12).Length).IsGreaterThan(Events(filled, Cymbal, 0, 12).Length);
        }
    }

    [Test]
    public async Task DrawnRuns_MostlyPlayTheSnareAndTheToms_InTheirOrder_WhereTheRhythmIsPlain()
    {
        var setup = Create(1);
        var plain = Enumerable.Range(0, 2_000).Select(_ => setup.Generator.DrawRun(Middle, 0.25)).ToArray();
        var wild = Enumerable.Range(0, 2_000).Select(_ => setup.Generator.DrawRun(Middle, 4)).ToArray();
        double Conventional(FillRun[] runs) =>
            runs.Count(x => x.Sounds.All(s => FillLayers.ConventionalRoles.Contains(s.Role))) / (double)runs.Length;
        double InOrder(FillRun[] runs)
        {
            var codes = runs.Select(x => x.Sounds.Where(s => s.Role == DrumRole.Toms).Select(s => s.Code).ToArray()).Where(x => x.Length > 2).ToArray();
            return codes.Count(x => x.Zip(x.Skip(1)).All(p => p.Second < p.First) || x.Zip(x.Skip(1)).All(p => p.Second > p.First)) / (double)codes.Length;
        }

        await Assert.That(plain.All(x => x.Sounds.Length > 0)).IsTrue();
        await Assert.That(Conventional(plain)).IsGreaterThan(0.9);
        await Assert.That(Conventional(wild)).IsLessThan(Conventional(plain) - 0.2);
        await Assert.That(InOrder(plain)).IsGreaterThan(0.95);
        await Assert.That(InOrder(wild)).IsLessThan(InOrder(plain) - 0.3);
        // a roll on the snare alone, now and then
        await Assert.That(plain.Count(x => x.Sounds is [{ Role: DrumRole.Snare }]) / (double)plain.Length).IsGreaterThan(0.1);
    }

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
        var hiHat = Events(generator.ApplyFill(song, FillPlay.Plain(FillKind.Lift), Line, ResolvedRhythm.Default, MinNote, Middle), HiHat, Line - 0.5, Line)
            .Single();

        await Assert.That(Articulation(hiHat)).IsEqualTo(DrumDefinitions.HiHat.GetArticulationIndex(DrumSounds.OpenHiHat));
    }

    [Test]
    public async Task Tuplet_PlaysTheRunOnTheTuplet()
    {
        var toms = Seeds.SelectMany(seed =>
            {
                var setup = Create(seed);
                return Events(ApplyRun(setup, Run(SoundsOf(setup.Generator, DrumRole.Toms)), FillTwist.Tuplet, 3), Tom, 0, 12);
            }
        ).ToArray();

        await Assert.That(toms.All(x => Math.Abs(x.Position * 6 - Math.Round(x.Position * 6)) < 1e-6)).IsTrue();
        await Assert.That(toms.Any(x => Math.Abs(x.Position * 4 - Math.Round(x.Position * 4)) > 1e-6)).IsTrue();
    }

    [Test]
    public async Task SlowDown_PlaysTheFinerNotesFirst()
    {
        foreach (var seed in Seeds)
        {
            var setup = Create(seed);
            var roll = Events(ApplyRun(setup, SnareRun(setup), FillTwist.SlowDown), setup.Snare, 4, Line)
                .Where(x => x.Value.GetStateValue(StateKinds.Velocity) != 0)
                .ToArray();
            var middle = (roll[0].Position + Line) / 2;

            // the second half keeps to 8ths
            await Assert.That(roll.Where(x => x.Position >= middle).All(x => Math.Abs(x.Position * 2 - Math.Round(x.Position * 2)) < 1e-6)).IsTrue();
        }
    }

    [Test]
    public async Task Fading_FadesTheRoll()
    {
        var rolls = Seeds.Select(seed =>
            {
                var setup = Create(seed);
                return Velocities(ApplyRun(setup, SnareRun(setup), FillTwist.Fading), setup.Snare);
            }
        ).ToArray();

        await Assert.That(rolls.Average(Swell)).IsLessThan(-0.2);
    }

    [Test]
    public async Task OddSpan_StartsTheFillOffTheBeat()
    {
        foreach (var seed in Seeds)
        {
            var setup = Create(seed);
            var first = Events(ApplyRun(setup, Run(SoundsOf(setup.Generator, DrumRole.Toms)), FillTwist.OddSpan), Tom, 0, 12)[0];

            await Assert.That(first.Position % 1).IsEqualTo(0.5).Within(1e-9);
        }
    }
}
