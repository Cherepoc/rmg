using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.DrumKits;

public sealed class DrumKitGeneratorTest
{
    private static IEnumerable<int> Seeds => Enumerable.Range(0, 500);

    private static ImmutableArray<PercussionInstrumentDefinition> SelectSectionDrums(int seed)
    {
        var context = new GenerationContext(seed);
        var songDrums = DrumKitGenerator.SelectSongDrums(context);
        return DrumKitGenerator.SelectActiveDrums(context, songDrums);
    }

    private static DrumGroup GroupOf(PercussionInstrumentDefinition drum)
    {
        return DrumGroups.All.Single(x => x.Drums.Contains(drum));
    }

    [Test]
    public async Task AlwaysOnGroups_AreAlwaysActive()
    {
        foreach (var seed in Seeds)
        {
            var kit = SelectSectionDrums(seed);
            var activeGroups = kit.Select(GroupOf).ToHashSet();

            foreach (var group in DrumGroups.All.Where(x => x.IsAlwaysOn))
                await Assert.That(activeGroups.Contains(group)).IsTrue();
        }
    }

    [Test]
    public async Task ActiveGroupCount_NeverExceedsLimit_AndIncludesOptionalGroup()
    {
        var alwaysOnCount = DrumGroups.All.Count(x => x.IsAlwaysOn);
        foreach (var seed in Seeds)
        {
            var kit = SelectSectionDrums(seed);
            var groupCount = kit.Select(GroupOf).Distinct().Count();

            await Assert.That(groupCount).IsBetween(alwaysOnCount + 1, DrumKitGenerator.MaxGroupsPerSection);
        }
    }

    [Test]
    public async Task TomsAndCymbal_GrooveInFewSections()
    {
        var kits = Seeds.Select(SelectSectionDrums).ToArray();
        double ShareWith(DrumGroup group) => kits.Count(kit => kit.Any(group.Drums.Contains)) / (double)kits.Length;

        // they play mostly in fills and landings, and the timekeepers take their place
        await Assert.That(ShareWith(DrumGroups.Toms)).IsBetween(0.05, 0.25);
        await Assert.That(ShareWith(DrumGroups.Accents)).IsBetween(0.02, 0.15);
        await Assert.That(ShareWith(DrumGroups.Timekeepers)).IsGreaterThan(0.6);
    }

    [Test]
    public async Task ActiveDrums_AreDistinct_AndRespectGroupLimit()
    {
        foreach (var seed in Seeds)
        {
            var kit = SelectSectionDrums(seed);

            await Assert.That(kit.Distinct().Count()).IsEqualTo(kit.Length);
            foreach (var groupKit in kit.GroupBy(GroupOf))
                await Assert.That(groupKit.Count()).IsBetween(1, groupKit.Key.MaxActiveDrums);
        }
    }

    [Test]
    public async Task ActiveDrums_AreFarFewerThanAllDrums()
    {
        foreach (var seed in Seeds)
        {
            var kit = SelectSectionDrums(seed);

            await Assert.That(kit.Length).IsLessThan(DrumGroups.AllDrums.Length / 2);
        }
    }

    [Test]
    public async Task EveryDrum_CanBeSelected_AcrossSeeds()
    {
        var seen = Enumerable.Range(0, 30000)
            .SelectMany(seed => SelectSectionDrums(seed))
            .ToHashSet();

        await Assert.That(seen.Count).IsEqualTo(DrumGroups.AllDrums.Length);
    }

    [Test]
    public async Task SameSeed_ResultsIn_SameKit()
    {
        var first = SelectSectionDrums(3);
        var second = SelectSectionDrums(3);

        await Assert.That(first.AsEnumerable()).IsEquivalentTo(second.AsEnumerable());
    }

    [Test]
    public async Task DrumTrackNumbers_AreUnique_AndOutsideOfPitchTracks()
    {
        var numbers = DrumGroups.AllDrums.Select(DrumGroups.GetTrackNumber).ToArray();

        await Assert.That(numbers.Distinct().Count()).IsEqualTo(numbers.Length);
        await Assert.That(numbers.Min()).IsGreaterThanOrEqualTo(DrumGroups.FirstTrackNumber);
    }

    [Test]
    public async Task ASectionOfMoreEnergy_PlaysMoreGroups_AndTheLoudDrumsMoreOften()
    {
        (double Groups, double Loud, double Quiet) Measure(Tilt tilt)
        {
            var kits = Seeds.Select(seed =>
                {
                    var context = new GenerationContext(seed);
                    var songDrums = DrumKitGenerator.SelectSongDrums(context);
                    return DrumKitGenerator.SelectActiveDrums(context, songDrums, tilt);
                }
            ).ToArray();
            return (
                kits.Average(x => x.Select(GroupOf).Distinct().Count()),
                kits.Average(x => x.Count(d => d.Loudness > 0)),
                kits.Average(x => x.Count(d => d.Loudness < 0))
            );
        }

        var even = Measure(Tilt.None);
        var loud = Measure(Tilt.Of(8, 1));
        var quiet = Measure(Tilt.Of(8, -1));

        await Assert.That(loud.Groups).IsGreaterThan(even.Groups);
        await Assert.That(quiet.Groups).IsLessThan(even.Groups);
        await Assert.That(loud.Loud).IsGreaterThan(even.Loud);
        await Assert.That(quiet.Quiet).IsGreaterThan(even.Quiet);
    }

    [Test]
    public async Task AGroupsLoudness_IsItsDrumsInTheSong_TheLikelierCountingMore()
    {
        var timekeepers = DrumGroups.Timekeepers;

        await Assert.That(DrumKitGenerator.GetLoudness(DrumGroups.Accents, [DrumDefinitions.Cymbal])).IsEqualTo(1);
        await Assert.That(DrumKitGenerator.GetLoudness(timekeepers, [DrumDefinitions.HiHat, DrumDefinitions.Ride])).IsEqualTo(0.5 / 1.5).Within(1e-9);
        await Assert.That(DrumKitGenerator.GetLoudness(timekeepers, [])).IsEqualTo(0);
    }
}
