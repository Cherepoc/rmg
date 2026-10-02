using System.Text.Json;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Descriptions;

public sealed class SongDescriptionsTest
{
    private static Rmg.Core.Songs.SongDescription Describe(CorpusSong song) => SongDescriptions.Of(song.Song, song.Rendered, song.Trace);

    [Test]
    public async Task ASong_IsDescribedSectionBySection_AsItsTracePlaysIt()
    {
        foreach (var song in TestCorpus.Range(32))
        {
            var description = Describe(song);
            var structure = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;

            // every section in the order it plays, named by its role, its bars adding up to the song's
            await Assert.That(description.Sections.Select(x => x.Place)).IsEquivalentTo(Enumerable.Range(1, song.Map.Sections.Length));
            foreach (var (section, span) in description.Sections.Zip(song.Map.Sections))
            {
                var role = structure.Roles[span.SectionId];
                await Assert.That(section.Name.StartsWith(role == SectionRole.Free ? "Section " : role == SectionRole.PreChorus ? "Pre-chorus" : role.ToString())).IsTrue();
                await Assert.That(section.Bars).IsEqualTo((int)Math.Round(span.Duration / span.Meter.BarDuration));
                await Assert.That(section.Energy).IsIn(["quiet", "medium", "loud"]);
                await Assert.That(section.TimeSignature is null).IsEqualTo(span.Meter == song.Map.Meter);
            }

            await Assert.That(description.Bars).IsEqualTo(description.Sections.Sum(x => x.Bars));
            await Assert.That(description.Seconds).IsGreaterThan(0);
            // the parts that play, and every drum it names playing
            await Assert.That(description.Parts.Select(x => x.Role).Distinct().Count()).IsEqualTo(description.Parts.Length);
            await Assert.That(description.Drums is null).IsEqualTo(!song.PlaysDrums);
            await Assert.That(description.Melody is null).IsEqualTo(song.Song.TrackDefinitions.All(x => x.Value.Role != TrackRole.Melody) || song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role == TrackRole.Melody).All(x => x.Value.Count == 0));
        }
    }

    [Test]
    public async Task ASectionsChords_AreItsProgressions_EveryOneNamed_WhereTheChordsPlay()
    {
        var named = 0;
        foreach (var song in TestCorpus.Range(32))
        {
            var description = Describe(song);
            var progressions = song.Trace.Where(x => x.Point == TracePoints.Progression).GroupBy(x => x.Section).ToDictionary(x => x.Key, x => ((System.Collections.Immutable.ImmutableArray<int>)x.First().Value!).Length);
            foreach (var (section, span) in description.Sections.Zip(song.Map.Sections))
            {
                await Assert.That(section.Chords.Length).IsEqualTo(progressions.GetValueOrDefault(span.SectionId));
                named += section.Chords.Count(x => x.Name is not null);
            }
        }

        await Assert.That(named).IsGreaterThan(0);
    }

    [Test]
    public async Task TheLoudestSection_IsLoud_AndTheQuietestQuiet()
    {
        var checkedSongs = 0;
        foreach (var song in TestCorpus.Range(32))
        {
            var energies = song.Trace.Where(x => x.Point == TracePoints.SectionEnergy).GroupBy(x => x.Section).ToDictionary(x => x.Key, x => ((SectionEnergyTrace)x.First().Value!).Energy);
            if (energies.Values.Distinct().Count() < 3)
                continue;

            checkedSongs++;
            var description = Describe(song);
            var loudest = energies.MaxBy(x => x.Value).Key;
            var quietest = energies.MinBy(x => x.Value).Key;
            foreach (var (section, span) in description.Sections.Zip(song.Map.Sections))
            {
                if (span.SectionId == loudest)
                    await Assert.That(section.Energy).IsEqualTo("loud");
                if (span.SectionId == quietest)
                    await Assert.That(section.Energy).IsEqualTo("quiet");
            }
        }

        await Assert.That(checkedSongs).IsGreaterThan(8);
    }

    [Test]
    public async Task TheGeneralMidiNames_AreEveryProgramsOnce()
    {
        await Assert.That(Rmg.Core.Songs.GeneralMidi.Programs.Length).IsEqualTo(128);
        await Assert.That(Rmg.Core.Songs.GeneralMidi.Programs.Distinct().Count()).IsEqualTo(128);
        await Assert.That(Rmg.Core.Songs.GeneralMidi.Programs[0]).IsEqualTo("Acoustic Grand Piano");
        await Assert.That(Rmg.Core.Songs.GeneralMidi.Programs[127]).IsEqualTo("Gunshot");
    }

    [Test]
    public async Task DescribingASong_LeavesItAsItsSeedMakesIt()
    {
        var before = TestCorpus.Get(3).Rendered;
        var described = SongDescriptions.Describe(3, Rmg.Core.Composition.SongOverrides.None, Rmg.Core.Rendering.SongMix.None, CancellationToken.None);
        await Assert.That(described.Sections.Length).IsEqualTo(TestCorpus.Get(3).Map.Sections.Length);
        await Assert.That(before.Duration).IsEqualTo(TestCorpus.Get(3).Rendered.Duration);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var seed in new[] { 1, 6, 28 })
            Console.WriteLine(JsonSerializer.Serialize(Describe(TestCorpus.Get(seed)), new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        await Task.CompletedTask;
    }
}
