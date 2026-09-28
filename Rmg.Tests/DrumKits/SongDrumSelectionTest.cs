using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.DrumKits;

public sealed class SongDrumSelectionTest
{
    private static IEnumerable<int> Seeds => Enumerable.Range(0, 1000);

    private static ImmutableArray<PercussionInstrumentDefinition> SongDrums(int seed)
    {
        return DrumKitGenerator.SelectSongDrums(new GenerationContext(seed));
    }

    private static PercussionInstrumentDefinition[] MainSnares =>
        [DrumDefinitions.AcousticSnare, DrumDefinitions.ElectricSnare, DrumDefinitions.Clap];

    [Test]
    public async Task EveryGroup_ExceptPercussion_HasAtLeastOneDrumInSong()
    {
        foreach (var seed in Seeds)
        {
            var songDrums = SongDrums(seed);

            foreach (var group in DrumGroups.All.Where(x => x != DrumGroups.Percussion))
                await Assert.That(group.Drums.Any(songDrums.Contains)).IsTrue();
        }
    }

    [Test]
    public async Task Song_HasOneMainSnare_ThatIsSnareElectricSnareOrClap()
    {
        foreach (var seed in Seeds)
            await Assert.That(MainSnares.Count(SongDrums(seed).Contains)).IsEqualTo(1);
    }

    [Test]
    public async Task TheSnares_CrossStick_IsAStrokeOfTheirs_AndNotTheClaps()
    {
        await Assert.That(DrumDefinitions.AcousticSnare.HasStrokes && DrumDefinitions.ElectricSnare.HasStrokes).IsTrue();
        await Assert.That(DrumDefinitions.AcousticSnare.ArticulationCodes).Contains(37);
        await Assert.That(DrumDefinitions.ElectricSnare.ArticulationCodes).Contains(37);
        await Assert.That(DrumDefinitions.Clap.ArticulationCodes).DoesNotContain(37);
    }

    [Test]
    public async Task Song_AlwaysHasKickAndCymbal()
    {
        foreach (var seed in Seeds)
        {
            var songDrums = SongDrums(seed);

            await Assert.That(songDrums.Contains(DrumDefinitions.Kick)).IsTrue();
            await Assert.That(songDrums.Contains(DrumDefinitions.Cymbal)).IsTrue();
        }
    }

    [Test]
    public async Task Song_AllTimekeepersAndTomsAreAvailable()
    {
        foreach (var seed in Seeds)
        {
            var songDrums = SongDrums(seed);

            foreach (var drum in DrumGroups.Timekeepers.Drums.Concat(DrumGroups.Toms.Drums))
                await Assert.That(songDrums.Contains(drum)).IsTrue();
        }
    }

    [Test]
    public async Task Song_PercussionPool_HasUpToFourDrums_AndSometimesNone()
    {
        var counts = Seeds.Select(seed => DrumGroups.Percussion.Drums.Count(SongDrums(seed).Contains)).ToArray();

        await Assert.That(counts.Max()).IsEqualTo(4);
        await Assert.That(counts.Contains(0)).IsTrue();
        await Assert.That(counts.Contains(1)).IsTrue();
    }

    [Test]
    public async Task Vibraslap_IsSometimesInSong_AndSometimesNot()
    {
        var withVibraslap = Seeds.Count(seed => SongDrums(seed).Contains(DrumDefinitions.Vibraslap));

        await Assert.That(withVibraslap).IsGreaterThan(0);
        await Assert.That(withVibraslap).IsLessThan(Seeds.Count() / 2);
    }

    [Test]
    public async Task EveryMainSnare_AndEveryPercussion_CanBeInSong()
    {
        var seen = Enumerable.Range(0, 5000).SelectMany(seed => SongDrums(seed)).ToHashSet();

        foreach (var drum in MainSnares.Concat(DrumGroups.Percussion.Drums))
            await Assert.That(seen.Contains(drum)).IsTrue();
    }

    [Test]
    public async Task SectionDrums_AreSubsetOfSongDrums()
    {
        foreach (var seed in Seeds)
        {
            var context = new GenerationContext(seed);
            var songDrums = DrumKitGenerator.SelectSongDrums(context);

            for (var i = 0; i < 5; i++)
            {
                var sectionDrums = DrumKitGenerator.SelectActiveDrums(context, songDrums);

                await Assert.That(sectionDrums.All(songDrums.Contains)).IsTrue();
            }
        }
    }

    [Test]
    public async Task SectionDrums_NeverMixSnares_WithinASong()
    {
        foreach (var seed in Seeds)
        {
            var context = new GenerationContext(seed);
            var songDrums = DrumKitGenerator.SelectSongDrums(context);
            var usedMainSnares = Enumerable.Range(0, 20)
                .SelectMany(_ => DrumKitGenerator.SelectActiveDrums(context, songDrums))
                .Where(MainSnares.Contains)
                .Distinct();

            await Assert.That(usedMainSnares.Count()).IsLessThanOrEqualTo(1);
        }
    }

    [Test]
    public async Task SameSeed_ResultsIn_SameSongDrums()
    {
        await Assert.That(SongDrums(8).AsEnumerable()).IsEquivalentTo(SongDrums(8).AsEnumerable());
    }
}
