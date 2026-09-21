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
    public async Task Song_HasOneMainSnare_ThatIsSnareClapOrCrossStick()
    {
        foreach (var seed in Seeds)
        {
            var songDrums = SongDrums(seed);
            var mainSnareCount = MainSnares.Count(songDrums.Contains);

            await Assert.That(mainSnareCount).IsLessThanOrEqualTo(1);
            // without any of them the sidestick is the only snare
            if (mainSnareCount == 0)
                await Assert.That(songDrums.Contains(DrumDefinitions.CrossStick)).IsTrue();
        }
    }

    [Test]
    public async Task Song_NeverHasCrossStickWithClap()
    {
        foreach (var seed in Seeds)
        {
            var songDrums = SongDrums(seed);

            await Assert.That(songDrums.Contains(DrumDefinitions.Clap) && songDrums.Contains(DrumDefinitions.CrossStick))
                .IsFalse();
        }
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
    public async Task CrossStick_IsInMinorityOfSongs_AlonePairedWithSnareAndAbsent()
    {
        var withCrossStick = Seeds.Where(seed => SongDrums(seed).Contains(DrumDefinitions.CrossStick)).ToArray();
        var alone = withCrossStick.Count(seed => !MainSnares.Any(SongDrums(seed).Contains));
        var withSnare = withCrossStick.Length - alone;

        await Assert.That(alone).IsGreaterThan(0);
        await Assert.That(withSnare).IsGreaterThan(0);
        // less than a third of the songs
        await Assert.That(withCrossStick.Length).IsLessThan(Seeds.Count() / 3);
    }

    [Test]
    public async Task EveryMainSnare_CrossStick_AndEveryPercussion_CanBeInSong()
    {
        var seen = Enumerable.Range(0, 5000).SelectMany(seed => SongDrums(seed)).ToHashSet();

        foreach (var drum in MainSnares.Append(DrumDefinitions.CrossStick).Concat(DrumGroups.Percussion.Drums))
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
