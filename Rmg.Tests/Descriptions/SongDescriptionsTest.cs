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
            await Assert.That(description.Sections.Select(x => x.Place)).IsEquivalentTo(Enumerable.Range(1, song.Map.Sections.Length), CollectionOrdering.Matching);
            foreach (var (section, span) in description.Sections.Zip(song.Map.Sections))
            {
                var role = structure.Roles[span.SectionId];
                await Assert.That(section.Name.StartsWith(role == SectionRole.Free ? "Section " : role == SectionRole.PreChorus ? "Pre-chorus" : role.ToString())).IsTrue();
                await Assert.That(section.Bars).IsEqualTo((int)Math.Round(span.Duration / span.Meter.BarDuration));
                await Assert.That(section.Energy).IsIn(["quiet", "medium", "loud"]);
                await Assert.That(section.TimeSignature is null).IsEqualTo(span.Meter.HasSameBar(song.Map.Meter));
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
        static byte[] Midi()
        {
            using var stream = new MemoryStream();
            Rmg.Core.Midi.Write(Rmg.Core.Rendering.Render.RenderSong(Rmg.Core.Composition.SongGenerator.GenerateSong(3)), stream, null);
            return stream.ToArray();
        }

        var before = Midi();
        var described = SongDescriptions.Describe(3, Rmg.Core.Composition.SongOverrides.None, Rmg.Core.Rendering.SongMix.None, CancellationToken.None);
        await Assert.That(described.Sections.Length).IsEqualTo(TestCorpus.Get(3).Map.Sections.Length);
        // the song made again after it is described, as it was
        await Assert.That(Midi().SequenceEqual(before)).IsTrue();
    }

    [Test]
    public async Task TheBusiestSection_IsBusy_AndTheSparsestSparse()
    {
        var checkedSongs = 0;
        foreach (var song in TestCorpus.Range(32))
        {
            var densities = song.Map.Sections.Select(span => song.Song.Notes!.Values.Sum(x => x.Count(n => n.Position >= span.Start && n.Position < span.End)) / span.Duration).ToArray();
            if (densities.Distinct().Count() < 3)
                continue;

            checkedSongs++;
            var description = Describe(song);
            await Assert.That(description.Sections[Array.IndexOf(densities, densities.Max())].Density).IsEqualTo("busy");
            await Assert.That(description.Sections[Array.IndexOf(densities, densities.Min())].Density).IsEqualTo("sparse");
        }

        await Assert.That(checkedSongs).IsGreaterThan(16);
    }

    [Test]
    public async Task ABurst_IsWhereAPartRunsFourNotesEvenly_FasterThan16ths_AndTheSongsFastestRunsAtLeastAsFast()
    {
        var bursts = 0;
        foreach (var song in TestCorpus.Range(64))
        {
            var description = Describe(song);
            foreach (var (section, span) in description.Sections.Zip(song.Map.Sections))
            {
                // four notes of one part, each the same short gap after the last, the drums' pieces as one part
                var runs = song.Song.TrackDefinitions.Where(x => song.Song.Notes!.ContainsKey(x.Key)).GroupBy(x => x.Value.Role == TrackRole.RiffTwin ? TrackRole.Riff : x.Value.Role)
                    .Select(part => part.SelectMany(x => song.Song.Notes![x.Key].Select(n => n.Position)).Where(x => x >= span.Start && x < span.End).Distinct().Order().ToArray())
                    .Any(on => Enumerable.Range(0, Math.Max(0, on.Length - 3)).Any(i =>
                        on[i + 1] - on[i] < 0.25 - 1e-6 && Math.Abs(on[i + 2] - on[i + 1] - (on[i + 1] - on[i])) < 1e-6 && Math.Abs(on[i + 3] - on[i + 2] - (on[i + 1] - on[i])) < 1e-6));
                await Assert.That(section.Burst is not null).IsEqualTo(runs);
                if (section.Burst is not { } burst)
                    continue;

                bursts++;
                await Assert.That(burst.Section).IsEqualTo(section.Place);
                // its parts those that play, or the drums' fill out of a breakdown, whose drums rest, into the next section
                await Assert.That(burst.Parts.All(x => section.Parts.Contains(x) || x == "drums" && section.Drums == "resting")).IsTrue()
                    .Because($"seed {song.Seed}, section {section.Place}: burst {string.Join(", ", burst.Parts)}, playing {string.Join(", ", section.Parts)}");
                await Assert.That(burst.NotesPerSecond).IsLessThanOrEqualTo(description.Fastest!.NotesPerSecond);
            }
        }

        await Assert.That(bursts).IsGreaterThan(4);
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
