using Rmg.Core.Composition;

namespace Rmg.Tests.Genres;

public sealed class GenrePresetsTest
{
    [Test]
    public async Task EveryPreset_IsAGenre_ByTheChoicesNames_ItsStepsFrom0To63()
    {
        var errors = new List<string>();
        foreach (var preset in GenrePresets.All)
        {
            foreach (var (choice, options) in preset.Choices)
            {
                var known = GenreChoices.All.FirstOrDefault(x => x.Name == choice);
                if (known is null)
                    errors.Add($"{preset.Name}: no choice {choice}");
                else
                    errors.AddRange(options.Where(x => known.Options.All(o => o.Name != x.Key) || x.Value is < 0 or > 63).Select(x => $"{preset.Name}: {choice} {x.Key} {x.Value}"));
            }

            if (errors.Count == 0)
                _ = GenrePresets.Genre(preset);
        }

        await Assert.That(errors).IsEmpty().Because(string.Join("; ", errors));
        await Assert.That(GenrePresets.All.Select(x => x.Id).Distinct().Count()).IsEqualTo(GenrePresets.All.Count);
        await Assert.That(GenrePresets.All.Count).IsEqualTo(10);
    }

    [Test]
    public async Task APreset_MakesSongs()
    {
        foreach (var preset in GenrePresets.All)
        foreach (var @base in new[] { 0.0, 1.0 })
            await Assert.That(TestCorpus.Get(1, new SongOverrides(Base: @base, Genre: GenrePresets.Genre(preset))).Song.Notes!.Values.Sum(x => x.Count)).IsGreaterThan(0).Because(preset.Name);
    }
}

public sealed class GenrePresetsReportTest
{
    private const int Count = 64;

    [Test]
    [Explicit]
    public async Task EveryPreset_AgainstNone()
    {
        var none = TestCorpus.Range(Count);
        void Print(string name, IReadOnlyList<CorpusSong> songs)
        {
            GenreReportTest.Report(name, [..songs.Select(GenreReportTest.Measure)]);
            var kits = songs.Where(x => x.PlaysDrums).GroupBy(x => Rmg.Core.Songs.GeneralMidi.KitName(x.Rendered.Tracks.Single(t => t.IsPercussionInstrument).PitchInstrumentCode));
            Console.WriteLine("  kit: " + string.Join(", ", kits.OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {100.0 * x.Count() / songs.Count:0}%")));
            var shapes = songs.SelectMany(GenreReportTest.Shapes).ToArray();
            Console.WriteLine("  shapes: " + string.Join(", ", shapes.GroupBy(x => x.Name).OrderByDescending(x => x.Count()).Take(6).Select(x => $"{x.Key} {100.0 * x.Count() / shapes.Length:0}%")));
            GenreRhythmReportTest.Report(name, [..songs.Select(GenreRhythmReportTest.Falls)]);
        }

        Print("none", none);
        foreach (var preset in GenrePresets.All)
            Print(preset.Name, TestCorpus.InParallel(Enumerable.Range(0, Count), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(preset)))));
        await Task.CompletedTask;
    }
}
