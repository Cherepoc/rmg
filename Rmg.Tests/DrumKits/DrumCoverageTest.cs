using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.DrumKits;

public sealed class DrumCoverageTest
{
    private static SectionDrums Section(bool isPercussionOnly, bool playsDrums, params PercussionInstrumentDefinition[] drums) =>
        new(isPercussionOnly, new SectionKit([..drums], [..drums.Take(1)], ImmutableDictionary<PercussionInstrumentDefinition, PercussionInstrumentDefinition>.Empty), playsDrums);

    private static readonly ImmutableArray<PercussionInstrumentDefinition> Song =
        [DrumDefinitions.Kick, DrumDefinitions.AcousticSnare, DrumDefinitions.HiHat, DrumDefinitions.Cabasa, DrumDefinitions.Conga, DrumDefinitions.Bongo, DrumDefinitions.Tom];

    private static ImmutableDictionary<int, int> Once(params int[] ids) => ids.ToImmutableDictionary(x => x, _ => 1);

    [Test]
    public async Task ADrumNoSectionTook_JoinsOneThatPlaysDrums_NeverOneThatRestsThem()
    {
        foreach (var seed in Enumerable.Range(0, 32))
        {
            var sections = new Dictionary<int, SectionDrums>
            {
                [0] = Section(false, false, DrumDefinitions.Kick, DrumDefinitions.AcousticSnare),
                [1] = Section(false, true, DrumDefinitions.Kick, DrumDefinitions.AcousticSnare, DrumDefinitions.HiHat),
                [2] = Section(true, true, DrumDefinitions.Conga, DrumDefinitions.Bongo)
            }.ToImmutableDictionary();

            var covered = DrumCoverage.Cover(new GenerationContext((ulong)seed), Song, sections, Once(0, 1, 2), _ => throw new InvalidOperationException("No section plays the kit again here."));

            // the cabasa, a hand drum, in either section that plays drums; the tom, of the fills, left to them
            await Assert.That(covered[0]).IsEqualTo(sections[0]);
            await Assert.That(covered[1].Kit.Drums.Contains(DrumDefinitions.Cabasa) || covered[2].Kit.Drums.Contains(DrumDefinitions.Cabasa)).IsTrue();
            await Assert.That(covered.Values.Any(x => x.Kit.Drums.Contains(DrumDefinitions.Tom))).IsFalse();
        }
    }

    [Test]
    public async Task ADrumOfTheKit_WhereEverySectionPlaysPercussionOnly_PlaysWhereOnePlaysTheKitAgain()
    {
        var sections = new Dictionary<int, SectionDrums>
        {
            [0] = Section(true, true, DrumDefinitions.Conga, DrumDefinitions.Bongo),
            [1] = Section(true, true, DrumDefinitions.Conga, DrumDefinitions.Bongo)
        }.ToImmutableDictionary();
        ImmutableArray<PercussionInstrumentDefinition> song = [DrumDefinitions.Kick, DrumDefinitions.Conga, DrumDefinitions.Bongo];

        var covered = DrumCoverage.Cover(new GenerationContext(1), song, sections, Once(0, 1), _ => Section(false, true, DrumDefinitions.Kick, DrumDefinitions.Conga));

        await Assert.That(covered.Values.Count(x => !x.IsPercussionOnly)).IsEqualTo(1);
        await Assert.That(covered.Values.Single(x => !x.IsPercussionOnly).Kit.Drums.Contains(DrumDefinitions.Kick)).IsTrue();
    }

    [Test]
    public async Task EveryDrumTaken_LeavesTheSectionsAsTheyWere_AndDrawsNothing()
    {
        var sections = new Dictionary<int, SectionDrums>
        {
            [0] = Section(false, true, DrumDefinitions.Kick, DrumDefinitions.AcousticSnare, DrumDefinitions.HiHat, DrumDefinitions.Cabasa),
            [1] = Section(true, true, DrumDefinitions.Conga, DrumDefinitions.Bongo)
        }.ToImmutableDictionary();
        var context = new GenerationContext(1);

        var covered = DrumCoverage.Cover(context, Song, sections, Once(0, 1), _ => throw new InvalidOperationException());

        await Assert.That(covered).IsEquivalentTo(sections);
        await Assert.That(context.GenerateInt()).IsEqualTo(new GenerationContext(1).GenerateInt());
    }

    [Test]
    public async Task ASongWithDrums_PlaysEveryDrumItHas_InItsGroove_ItsFills_OrItsLandings_AllButNow_AndThen()
    {
        var songs = TestCorpus.Range(256).Where(x => x.Song.Draws!.Parts.Contains(TrackRole.Drum)).ToArray();
        var silent = songs.Count(song => song.Song.TrackDefinitions.Keys
            .Where(x => x >= DrumGroups.FirstTrackNumber)
            .Any(x => !song.Song.Notes!.TryGetValue(x, out var notes) || notes.Count == 0));

        // a drum the form leaves out of a section's later appearances, as its first one plays it, now and then (2% of
        // these songs; 82% before every drum was given a section and a landing)
        await Assert.That(silent / (double)songs.Length).IsLessThan(0.03);
    }
}
