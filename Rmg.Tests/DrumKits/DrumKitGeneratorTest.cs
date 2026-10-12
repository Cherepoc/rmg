using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.DrumKits;

public sealed class DrumKitGeneratorTest
{
    private static IEnumerable<int> Seeds => Enumerable.Range(0, 500);

    private static (ImmutableArray<PercussionInstrumentDefinition> Song, ImmutableArray<PercussionInstrumentDefinition> Kit, SectionKit Section) Select(
        int seed,
        Tilt tilt = default,
        bool isPercussionOnly = false
    )
    {
        var context = new GenerationContext((ulong)seed);
        var songDrums = DrumSetups.SelectSongDrums(context, DrumSetup.KitAndPercussion, null);
        var kit = DrumKitGenerator.SelectKit(context, songDrums, tilt, isPercussionOnly, songDrums.ToDictionary(x => x, x => x.MainRole));
        return (songDrums, kit.Drums, kit);
    }

    private static DrumGroup GroupOf(PercussionInstrumentDefinition drum) => DrumGroups.All.Single(x => x.Drums.Contains(drum));

    [Test]
    public async Task ADrumKitSection_HasAGroundABackbeatAndTime()
    {
        foreach (var seed in Seeds)
        {
            var (song, drums, section) = Select(seed);
            // its leads, not the drums that double them
            var kit = drums.Except(section.Doubles.Keys).ToArray();

            await Assert.That(kit.Count(x => x.MainRole == DrumRole.Ground && GroupOf(x) != DrumGroups.Percussion)).IsEqualTo(1);
            await Assert.That(kit.Count(x => x.MainRole == DrumRole.Backbeat && GroupOf(x) != DrumGroups.Percussion)).IsEqualTo(1);
            // a timekeeper wherever the song has one of the kit
            if (song.Any(x => GroupOf(x) == DrumGroups.Timekeepers && x.Family.HasFlag(DrumFamily.Kit)))
                await Assert.That(section.Leads[DrumRole.Time]).IsNotNull();
        }
    }

    [Test]
    public async Task ALead_IsADrumThatPlaysItsRoleInTheSection_OrWhereNoneDoes_OneWhoseMainRoleItIs_PlayingIt()
    {
        var songDrums = ImmutableArray.Create(DrumDefinitions.Kick, DrumDefinitions.AcousticSnare, DrumDefinitions.HiHat);
        foreach (var seed in Enumerable.Range(0, 32))
        {
            // the hi-hat on the backbeat and the snare keeping time
            var swapped = DrumKitGenerator.SelectKit(new GenerationContext((ulong)seed), songDrums, default, false, new Dictionary<PercussionInstrumentDefinition, DrumRole>
            {
                [DrumDefinitions.Kick] = DrumRole.Ground, [DrumDefinitions.AcousticSnare] = DrumRole.Time, [DrumDefinitions.HiHat] = DrumRole.Backbeat
            });
            await Assert.That(swapped.Leads[DrumRole.Backbeat]).IsEqualTo(DrumDefinitions.HiHat);
            await Assert.That(swapped.Leads[DrumRole.Time]).IsEqualTo(DrumDefinitions.AcousticSnare);
            await Assert.That(swapped.Roles[DrumDefinitions.HiHat]).IsEqualTo(DrumRole.Backbeat);

            // the snare keeping time beside the hi-hat, and no drum on the backbeat: the snare plays it after all
            var doubled = DrumKitGenerator.SelectKit(new GenerationContext((ulong)seed), songDrums, default, false, new Dictionary<PercussionInstrumentDefinition, DrumRole>
            {
                [DrumDefinitions.Kick] = DrumRole.Ground, [DrumDefinitions.AcousticSnare] = DrumRole.Time, [DrumDefinitions.HiHat] = DrumRole.Time
            });
            await Assert.That(doubled.Leads[DrumRole.Backbeat]).IsEqualTo(DrumDefinitions.AcousticSnare);
            await Assert.That(doubled.Roles[DrumDefinitions.AcousticSnare]).IsEqualTo(DrumRole.Backbeat);
            await Assert.That(doubled.Leads[DrumRole.Time]).IsEqualTo(DrumDefinitions.HiHat);
        }
    }

    [Test]
    public async Task TomsAndCymbal_ColourFewSections()
    {
        var kits = Seeds.Select(x => Select(x).Kit).ToArray();
        double ShareWith(DrumGroup group) => kits.Count(kit => kit.Any(group.Drums.Contains)) / (double)kits.Length;

        // they play mostly in fills and landings
        Console.WriteLine($"toms in {ShareWith(DrumGroups.Toms):P1} of the kits, accent cymbals in {ShareWith(DrumGroups.Accents):P1}");
        // measured 7.0% and 1.8%
        await Assert.That(ShareWith(DrumGroups.Toms)).IsBetween(0.03, 0.25);
        await Assert.That(ShareWith(DrumGroups.Accents)).IsBetween(0.01, 0.15);
    }

    [Test]
    public async Task AKit_IsTheSongsDrums_Distinct_AndFarFewer()
    {
        foreach (var seed in Seeds)
        {
            var (song, kit, section) = Select(seed);

            await Assert.That(kit.All(song.Contains)).IsTrue();
            await Assert.That(kit.Distinct().Count()).IsEqualTo(kit.Length);
            await Assert.That(kit.Length).IsLessThan(DrumGroups.AllDrums.Length / 2);
            // a drum bound to a lead plays with it, not as its group's colour
            foreach (var colour in kit.Except(section.Doubles.Keys).GroupBy(GroupOf).Where(x => !x.Key.HoldsARole))
                await Assert.That(colour.Count()).IsLessThanOrEqualTo(colour.Key.MaxActiveDrums);
        }
    }

    [Test]
    public async Task ASectionOfPercussionOnly_PlaysOnlyPercussion_AndLeadsItsRolesFromIt()
    {
        foreach (var seed in Seeds)
        {
            var (song, kit, section) = Select(seed, isPercussionOnly: true);
            var percussion = song.Where(x => x.Family.HasFlag(DrumFamily.Percussion)).ToArray();

            await Assert.That(kit.All(x => x.Family.HasFlag(DrumFamily.Percussion))).IsTrue();
            await Assert.That(kit.Length).IsEqualTo(Math.Min(percussion.Length, PercussionSections.MaxActiveDrums));
            // every role its drums hold led, by one of them
            foreach (var role in kit.Select(x => x.MainRole).Where(x => x != DrumRole.Colour).Distinct())
                await Assert.That(section.Leads.ContainsKey(role)).IsTrue().Because($"seed {seed}, {role}");
            await Assert.That(section.Leads.Values.All(kit.Contains)).IsTrue().Because($"seed {seed}");
        }
    }

    [Test]
    public async Task ASectionOfMoreEnergy_PlaysMoreColour_AndTheLoudDrumsMoreOften()
    {
        (double Drums, double Loud, double Quiet) Measure(Tilt tilt)
        {
            var kits = Seeds.Select(seed => Select(seed, tilt)).ToArray();
            // the quiet drums as a share of the kit, where the song has one to play, as a quieter section has fewer drums
            // of every kind
            var quiet = kits.Where(x => x.Song.Any(d => d.Loudness < 0)).ToArray();
            return (kits.Average(x => x.Kit.Length), kits.Average(x => x.Kit.Count(d => d.Loudness > 0)),
                quiet.Average(x => x.Kit.Count(d => d.Loudness < 0) / (double)Math.Max(1, x.Kit.Length)));
        }

        var even = Measure(Tilt.None);
        var loud = Measure(Tilt.Of(8, 1));
        var quiet = Measure(Tilt.Of(8, -1));

        await Assert.That(loud.Drums).IsGreaterThan(even.Drums);
        await Assert.That(quiet.Drums).IsLessThan(even.Drums);
        await Assert.That(loud.Loud).IsGreaterThan(even.Loud);
        await Assert.That(quiet.Quiet).IsGreaterThan(even.Quiet);
    }

    [Test]
    public async Task EveryDrum_CanBeSelected_AcrossSeeds()
    {
        var seen = Enumerable.Range(0, 30000).SelectMany(seed => Select(seed, isPercussionOnly: seed % 5 == 0).Kit).ToHashSet();

        await Assert.That(seen.Count).IsEqualTo(DrumGroups.AllDrums.Length);
    }

    [Test]
    public async Task SameSeed_ResultsIn_SameKit()
    {
        await Assert.That(Select(3).Kit.AsEnumerable()).IsEquivalentTo(Select(3).Kit.AsEnumerable(), CollectionOrdering.Matching);
    }

    [Test]
    public async Task DrumTrackNumbers_AreUnique_AndOutsideOfPitchTracks()
    {
        var numbers = DrumGroups.AllDrums.Select(DrumGroups.GetTrackNumber).ToArray();

        await Assert.That(numbers.Distinct().Count()).IsEqualTo(numbers.Length);
        await Assert.That(numbers.Min()).IsGreaterThanOrEqualTo(DrumGroups.FirstTrackNumber);
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
