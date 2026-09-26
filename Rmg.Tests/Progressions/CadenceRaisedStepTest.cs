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

        await Assert.That(Render.RaiseScaleSteps(minor, [6]).ToArray()).IsEquivalentTo([0, 2, 3, 5, 7, 8, 11]);
        await Assert.That(Render.RaiseScaleSteps(minor, [5, 6]).ToArray()).IsEquivalentTo([0, 2, 3, 5, 7, 9, 11]);
        await Assert.That(Render.RaiseScaleSteps(minor, [])).IsEqualTo(minor);
    }

    [Test]
    [Arguments(new[] { 6, 6 })]
    [Arguments(new[] { 1, 1 })]
    public async Task RaiseScaleSteps_OutOfOrder_ResultsIn_ArgumentException(int[] steps)
    {
        ImmutableArray<int> minor = [0, 2, 3, 5, 7, 8, 10];

        await Assert.That(() => Render.RaiseScaleSteps(minor, [..steps])).Throws<ArgumentException>();
    }

    [Test]
    public async Task Songs_RaiseSteps_OnlyInTheCadenceBar()
    {
        var raising = 0;
        for (var seed = 0; seed < 60; seed++)
        {
            var common = SongGenerator.GenerateSong(seed).TrackEventStateTimelineMap.CommonStateTimelineMap;

            foreach (var item in common.GetStateTimeline(StateKinds.RaisedScaleSteps).Where(x => !x.Value.IsEmpty))
            {
                raising++;
                // the last bar of a 4-bar pattern; whether it raises depends on the section's home, not the song's
                // scale: even harmonic minor has a minor fifth seen from its fourth step
                await Assert.That(item.Position % 16).IsEqualTo(12).Because($"seed {seed}");
            }
        }

        await Assert.That(raising).IsGreaterThan(0);
    }
}
