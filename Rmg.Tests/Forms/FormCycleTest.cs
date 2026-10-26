using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Forms;

/// <summary>A form as a cycle the song's length repeats (<see cref="WrittenForm.Repeats" />), Jazz's AABA played through as its experiment.</summary>
public sealed class FormCycleTest
{
    /// <summary>Jazz, its AABA a cycle, as heard 6-0 against its AABA once and twice on 26 October 2026.</summary>
    private static readonly Genre Cycling = GenrePresets.Genre("jazz");

    /// <summary>Jazz with its AABA played once, its other forms as they are, as it played before the cycle but for its twice through, which the cycle folded in.</summary>
    private static readonly Genre Before = GenrePresets.With("jazz", GenreChoices.Form, ("vvbv", 1), ("vcvcbc", 0.1));

    private static CorpusSong[] Songs(Genre genre, double? @base) =>
        [..TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre, Base: @base)))];

    /// <summary>The roles a song's form plays, its intro's and outro's left out.</summary>
    private static SectionRole[] Roles(CorpusSong song) =>
        song.Trace.Single(x => x.Point == TracePoints.SongForm).Value is SongStructure form
            ? [..form.SectionIds.Select(x => form.Roles[x]).Where(x => x is not (SectionRole.Intro or SectionRole.Outro))]
            : [];

    /// <summary>A rule: a song in a cycle plays it through whole, pass after pass, each pass its sections again, the same A, the same B.</summary>
    [Test]
    public async Task ACycle_PlaysWholePasses_OfTheSameSections()
    {
        var cycled = 0;
        foreach (var song in Songs(Cycling, 0))
        {
            var form = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
            var roles = Roles(song);
            if (roles.Contains(SectionRole.Chorus) || roles.Contains(SectionRole.Free))
                continue;
            cycled++;
            var because = $"seed {song.Seed}: {string.Join(" ", roles)}";
            await Assert.That(roles.Length % 4).IsEqualTo(0).Because(because);
            await Assert.That(roles.Chunk(4).All(x => x.SequenceEqual([SectionRole.Verse, SectionRole.Verse, SectionRole.Bridge, SectionRole.Verse]))).IsTrue().Because(because);
            await Assert.That(form.Roles.Count(x => x.Value is SectionRole.Verse or SectionRole.Bridge)).IsEqualTo(2).Because(because);
        }

        await Assert.That(cycled).IsGreaterThan(20);
    }

    /// <summary>A rule: a preset naming a cycle gives a length, which the cycle is played through as many times as comes nearest, as one without plays it once.</summary>
    [Test]
    public async Task APresetNamingACycle_GivesALength()
    {
        foreach (var preset in GenrePresets.All.Where(x => x.Choices.TryGetValue(GenreChoices.Form.Name, out var forms) && forms.Keys.Any(k => k.EndsWith('+'))))
            await Assert.That(preset.Choices.ContainsKey(GenreChoices.Length.Name)).IsTrue().Because(preset.Id);
    }

    /// <summary>A rule: at the plain end a cycle of three passes or more plays its head out, none of its last pass soloed.</summary>
    [Test]
    public async Task APlainCycleOfThreePasses_PlaysItsHeadOut()
    {
        var heads = 0;
        foreach (var song in Songs(Cycling, 0))
        {
            var roles = Roles(song);
            if (roles.Contains(SectionRole.Free) || roles.Length < 12)
                continue;
            heads++;
            var body = song.Map.Sections.Select((x, i) => i).Where(i => song.Trace.Single(t => t.Point == TracePoints.SongForm).Value is SongStructure form
                                                                         && form.Roles[song.Map.Sections[i].SectionId] is not (SectionRole.Intro or SectionRole.Outro)).ToArray();
            foreach (var index in body[^4..])
                await Assert.That(song.Solos.ContainsKey(index)).IsFalse().Because($"seed {song.Seed}, section {index}");
        }

        await Assert.That(heads).IsGreaterThan(20);
    }

    /// <summary>
    ///     Jazz now and with its AABA a cycle, at the plain end and as drawn: the songs with a bridge, in a free form, and
    ///     within Jazz's range of lengths, the passes a cycle plays, the sections after the first soloed, and the songs whose
    ///     last pass of the form is sung, the head played out, rather than soloed.
    /// </summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        static double Share(IEnumerable<bool> values) => values.DefaultIfEmpty().Average(x => x ? 1.0 : 0);
        foreach (var (name, genre) in new[] { ("before", Before), ("cycle", Cycling) })
            foreach (var @base in new double?[] { 0, null })
            {
                var songs = Songs(genre, @base);
                var passes = songs.Select(Roles).Where(x => x.Length > 0 && x.All(r => r is SectionRole.Verse or SectionRole.Bridge) && x.Length % 4 == 0).Select(x => x.Length / 4).ToArray();
                bool LastPassSung(CorpusSong song)
                {
                    var count = song.Map.Sections.Length;
                    return Enumerable.Range(count - 4, 4).All(i => !song.Solos.ContainsKey(i));
                }
                Console.WriteLine($"{name,-6} {(@base is { } b ? $"base {b}" : "drawn"),-7}: bridge {Share(songs.Select(x => Roles(x).Contains(SectionRole.Bridge))):P0}, " +
                                  $"free {Share(songs.Select(x => Roles(x).Contains(SectionRole.Free))):P0}, " +
                                  $"within 270-600 s {Share(songs.Select(x => x.Rendered.Seconds is >= 270 and <= 600)):P0}, " +
                                  $"AABA passes {string.Join(" ", passes.GroupBy(x => x).OrderBy(x => x.Key).Select(x => $"{x.Key}x{x.Count()}"))}, " +
                                  $"soloed after the first {songs.Where(x => x.Map.Sections.Length > 1).Average(x => x.Solos.Count / (double)(x.Map.Sections.Length - 1)):P0}, " +
                                  $"last four sung {Share(songs.Where(x => x.Map.Sections.Length >= 8).Select(LastPassSung)):P0}");
            }

        await Task.CompletedTask;
    }
}
