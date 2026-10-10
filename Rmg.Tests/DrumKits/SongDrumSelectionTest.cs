using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.DrumKits;

public sealed class SongDrumSelectionTest
{
    private static IEnumerable<int> Seeds => Enumerable.Range(0, 1000);

    private static ImmutableArray<PercussionInstrumentDefinition> SongDrums(int seed, DrumSetup setup = DrumSetup.KitAndPercussion)
    {
        return DrumSetups.SelectSongDrums(new GenerationContext((ulong)seed), setup, null);
    }

    private static PercussionInstrumentDefinition[] MainSnares =>
        [DrumDefinitions.AcousticSnare, DrumDefinitions.ElectricSnare];

    [Test]
    public async Task EveryGroup_ButThePercussionAndTheCalls_HasAtLeastOneDrumInSong()
    {
        foreach (var seed in Seeds)
        {
            var songDrums = SongDrums(seed);

            foreach (var group in DrumGroups.All.Where(x => x != DrumGroups.Percussion && x != DrumGroups.Calls))
                await Assert.That(group.Drums.Any(songDrums.Contains)).IsTrue();
        }
    }

    [Test]
    public async Task TheCalls_AreInAFewSongs_AndNeverLeadARole()
    {
        var withCalls = Seeds.Count(seed => DrumGroups.Calls.Drums.Any(SongDrums(seed).Contains)) / (double)Seeds.Count();
        Console.WriteLine($"songs with a call {withCalls:P1}");
        // measured: 18% of 1000 songs, October 2026
        await Assert.That(withCalls).IsBetween(0.1, 0.3);

        foreach (var seed in Seeds)
        {
            var context = new GenerationContext((ulong)seed);
            var kit = DrumKitGenerator.SelectKit(context, SongDrums(seed), default, false, SongDrums(seed).ToDictionary(x => x, x => x.MainRole));

            await Assert.That(kit.Leads.Values.Any(DrumGroups.Calls.Drums.Contains)).IsFalse();
        }
    }

    [Test]
    public async Task Song_HasOneSnare_AcousticOrElectric_AndSometimesAClap()
    {
        foreach (var seed in Seeds)
            await Assert.That(MainSnares.Count(SongDrums(seed).Contains)).IsEqualTo(1);
        var withClap = Seeds.Count(seed => SongDrums(seed).Contains(DrumDefinitions.Clap)) / (double)Seeds.Count();
        Console.WriteLine($"songs with a clap {withClap:P1}");
        // measured: 29% of 1000 songs, October 2026
        await Assert.That(withClap).IsBetween(0.2, 0.4);
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
    public async Task ASong_HasTheTom_ItsHiHat_MostlyARide_AndAFewOfTheOtherTimekeepers()
    {
        var songs = Seeds.Select(seed => SongDrums(seed)).ToArray();
        foreach (var songDrums in songs)
        {
            await Assert.That(songDrums.Contains(DrumDefinitions.Tom)).IsTrue();
            await Assert.That(songDrums.Contains(DrumDefinitions.HiHat)).IsTrue();
            await Assert.That(songDrums.Count(new[] { DrumDefinitions.Tambourine, DrumDefinitions.Cabasa, DrumDefinitions.Maracas }.Contains)).IsLessThanOrEqualTo(2);
        }

        var ride = songs.Count(x => x.Contains(DrumDefinitions.Ride)) / (double)songs.Length;
        Console.WriteLine($"songs with a ride {ride:P1}");
        // measured: 76% of 1000 songs, October 2026
        await Assert.That(ride).IsBetween(0.65, 0.85);
    }

    [Test]
    public async Task AKitSong_HasNoPercussion_AndOneOfTheKitAndPercussion_OneToThree()
    {
        int Count(int seed, DrumSetup setup) => DrumGroups.Percussion.Drums.Count(SongDrums(seed, setup).Contains);

        await Assert.That(Seeds.All(seed => Count(seed, DrumSetup.Kit) == 0)).IsTrue();
        var counts = Seeds.Select(seed => Count(seed, DrumSetup.KitAndPercussion)).ToArray();
        await Assert.That(counts.Min()).IsEqualTo(1);
        await Assert.That(counts.Max()).IsEqualTo(3);
    }

    [Test]
    public async Task APercussionSong_HasNoDrumKit_ADrumForEveryRole_AndMore()
    {
        foreach (var seed in Seeds)
        {
            var drums = SongDrums(seed, DrumSetup.Percussion);

            await Assert.That(drums.All(x => x.Family.HasFlag(DrumFamily.Percussion))).IsTrue();
            foreach (var role in DrumKitGenerator.LeadRoles)
                await Assert.That(drums.Count(x => x.MainRole == role)).IsGreaterThanOrEqualTo(1);
            await Assert.That(drums.Length).IsBetween(4, 6);
        }
    }

    [Test]
    public async Task MostSongs_PlayTheKitAlone_AFew_PercussionAlone_TheMoreTheWilder()
    {
        double Share(DrumSetup setup, double unconventionality) => Seeds.Count(seed =>
            DrumSetups.Pick(new GenerationContext((ulong)seed), unconventionality, Genre.None) == setup) / (double)Seeds.Count();

        Console.WriteLine($"at the middle the kit alone {Share(DrumSetup.Kit, 0.5):P1}, percussion alone {Share(DrumSetup.Percussion, 0.5):P1}; percussion alone {Share(DrumSetup.Percussion, 0):P1} plainest, {Share(DrumSetup.Percussion, 1):P1} wildest");
        // measured: the kit alone 61%, percussion alone 7% at the middle, 0% and 33% at the ends, October 2026
        await Assert.That(Share(DrumSetup.Kit, 0.5)).IsBetween(0.55, 0.7);
        await Assert.That(Share(DrumSetup.Percussion, 0.5)).IsBetween(0.02, 0.09);
        await Assert.That(Share(DrumSetup.Percussion, 1)).IsGreaterThan(Share(DrumSetup.Percussion, 0) * 3);
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

        foreach (var drum in MainSnares.Append(DrumDefinitions.Clap).Concat(DrumGroups.Percussion.Drums))
            await Assert.That(seen.Contains(drum)).IsTrue();
    }

    [Test]
    public async Task SameSeed_ResultsIn_SameSongDrums()
    {
        await Assert.That(SongDrums(8).AsEnumerable()).IsEquivalentTo(SongDrums(8).AsEnumerable(), CollectionOrdering.Matching);
    }
}
