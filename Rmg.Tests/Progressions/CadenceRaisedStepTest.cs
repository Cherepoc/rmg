using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Rendering;

namespace Rmg.Tests.Progressions;

public sealed class CadenceRaisedStepTest
{
    private static Scale ScaleNamed(string name) => Rmg.Core.Composition.Scales.All.Single(x => x.Name == name);

    [Test]
    [Arguments("Natural minor", 6)]
    [Arguments("Dorian", 6)]
    [Arguments("Mixolydian", 6)]
    [Arguments("Major", null)]
    [Arguments("Harmonic minor", null)]
    [Arguments("Phrygian", null)]
    [Arguments("Lydian", null)]
    public async Task CadenceOnTheFifth_RaisesTheSeventh_WhereItMakesTheFifthMajor(string scaleName, int? expected)
    {
        var result = Rmg.Core.Composition.Progressions.GetCadenceRaisedStep(ScaleNamed(scaleName).Offsets, 0, 4);

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(3)]
    [Arguments(-1)]
    public async Task OtherCadences_RaiseNothing(int cadenceRoot)
    {
        var result = Rmg.Core.Composition.Progressions.GetCadenceRaisedStep(Rmg.Core.Composition.Scales.NaturalMinor.Offsets, 0, cadenceRoot);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task RelativeMajorHome_RaisesNothing_AndIVHomeOfMajorRaisesItsSeventh()
    {
        var minor = Rmg.Core.Composition.Scales.NaturalMinor.Offsets;
        var major = Rmg.Core.Composition.Scales.Major.Offsets;

        // C minor from E flat is E flat major, whose fifth is major already
        await Assert.That(Rmg.Core.Composition.Progressions.GetCadenceRaisedStep(minor, 2, 4)).IsNull();
        // C major from G is G mixolydian: F, a whole step below G, is raised to F sharp
        await Assert.That(Rmg.Core.Composition.Progressions.GetCadenceRaisedStep(major, 4, 4)).IsEqualTo(3);
    }

    [Test]
    public async Task RaiseScaleSteps_RaisesEachListedStep()
    {
        ImmutableArray<int> minor = [0, 2, 3, 5, 7, 8, 10];

        await Assert.That(Realizer.RaiseScaleSteps(minor, [6]).ToArray()).IsEquivalentTo([0, 2, 3, 5, 7, 8, 11]);
        await Assert.That(Realizer.RaiseScaleSteps(minor, [5, 6]).ToArray()).IsEquivalentTo([0, 2, 3, 5, 7, 9, 11]);
        await Assert.That(Realizer.RaiseScaleSteps(minor, [])).IsEqualTo(minor);
    }

    [Test]
    [Arguments(new[] { 6, 6 })]
    [Arguments(new[] { 1, 1 })]
    public async Task RaiseScaleSteps_OutOfOrder_ResultsIn_ArgumentException(int[] steps)
    {
        ImmutableArray<int> minor = [0, 2, 3, 5, 7, 8, 10];

        await Assert.That(() => Realizer.RaiseScaleSteps(minor, [..steps])).Throws<ArgumentException>();
    }

    [Test]
    public async Task Songs_RaiseSteps_OnlyInTheCadenceChord()
    {
        var raising = 0;
        for (var seed = 0; seed < 64; seed++)
        {
            var corpusSong = TestCorpus.Get(seed);
            var (song, origin) = corpusSong;
            var common = song.TrackEventStateTimelineMap.CommonStateTimelineMap;
            var changes = corpusSong.ChordChanges;

            foreach (var item in common.GetStateTimeline(StateKinds.RaisedScaleSteps).Where(x => !x.Value.IsEmpty))
            {
                raising++;
                // the last chord of a 4-bar pattern, where the chords change and the next change starts a pattern;
                // whether it raises depends on the section's home, not the song's scale: even harmonic minor has a minor
                // fifth seen from its fourth step
                await Assert.That(changes).Contains(item.Position).Because($"seed {seed}");
                var next = changes.FirstOrDefault(x => x > item.Position, song.Map!.Sections.Max(x => x.End));
                await Assert.That((next - origin) % song.Map.Meter.PatternDuration).IsEqualTo(0).Because($"seed {seed}");
            }
        }

        await Assert.That(raising).IsGreaterThan(0);
    }

    [Test]
    public async Task ThePlainestCadences_RaiseTheSeventhWhereTheyCan_AndTheWildestNever()
    {
        (int? Raisable, bool IsRaised)[] Raises(double @base) => TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new Rmg.Core.Composition.SongOverrides(Base: @base)))
            .SelectMany(song => song.Trace.Where(x => x.Point == Rmg.Core.Composition.TracePoints.CadenceRaise).Select(x => ((int?, bool))x.Value!))
            .Where(x => x.Item1 is not null)
            .ToArray();

        var plain = Raises(0);
        await Assert.That(plain.Length).IsGreaterThan(0);
        await Assert.That(plain.All(x => x.IsRaised)).IsTrue();
        await Assert.That(Raises(1).Any(x => x.IsRaised)).IsFalse();
    }
}
