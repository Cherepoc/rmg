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
}
