using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Forms;

/// <summary>A riff genre's riff section: its intro coming back between its verses and closing the song.</summary>
public sealed class RiffSectionReportTest
{
    private static readonly string[] Genres = ["rock", "metal", "thrash", "funkRock", "nuMetal", "punk", "doom", "symphonic", "djent", "blackMetal"];

    /// <summary>Every appearance of a song's riff section, an intro that comes back: whether its riff plays in it; none where the song plays none.</summary>
    internal static bool[] Appearances(CorpusSong song)
    {
        var form = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
        if (form.Roles.FirstOrDefault(x => x.Value == SectionRole.Intro) is not { Value: SectionRole.Intro } intro || form.SectionIds.Count(x => x == intro.Key) < 2)
            return [];
        var notes = song.Song.Notes!.GetValueOrDefault(song.Track(TrackRole.Riff));
        return [..song.PlayedSpans.Where(x => x.SectionId == intro.Key).Select(span => notes?.Any(n => span.IsWithin(n.Position)) == true)];
    }

    /// <summary>
    ///     A riff section opens and closes the song and comes back only after a chorus a verse follows; the songs' own never
    ///     play one, a genre's device.
    /// </summary>
    [Test]
    public async Task ARiffSection_OpensAndClosesTheSong_AndComesBackBetweenAChorusAndAVerse()
    {
        var returning = 0;
        foreach (var genre in new[] { "metal", "rock" })
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))))
        {
            var form = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
            var ids = form.SectionIds;
            var intro = form.Roles.Where(x => x.Value == SectionRole.Intro).Select(x => (int?)x.Key).SingleOrDefault();
            if (intro is not { } id || ids.Count(x => x == id) < 2)
                continue;
            returning++;
            await Assert.That(ids[0] == id && ids[^1] == id).IsTrue().Because($"{genre} seed {song.Seed}");
            for (var i = 1; i < ids.Length - 1; i++)
                if (ids[i] == id)
                    await Assert.That(form.Roles[ids[i - 1]] == SectionRole.Chorus && form.Roles[ids[i + 1]] == SectionRole.Verse).IsTrue().Because($"{genre} seed {song.Seed} at {i}");
        }

        await Assert.That(returning).IsGreaterThan(20);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed)))
        {
            var form = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
            await Assert.That(form.SectionIds.GroupBy(x => x).Any(x => form.Roles[x.Key] == SectionRole.Intro && x.Count() > 1)).IsFalse().Because($"seed {song.Seed}");
        }
    }

    /// <summary>
    ///     A section over the intro's riff is a verse of a song that opens on an intro, in genres that do not loop otherwise
    ///     (<see cref="GenreChoices.Loop" />).
    /// </summary>
    [Test]
    public async Task ASectionOverTheIntrosRiff_IsAVerse_OfASongOpeningOnAnIntro()
    {
        var over = 0;
        var bars = new List<(bool Rhythm, int Kept, int Notes)>();
        foreach (var genre in new[] { "metal", "rock", "thrash" })
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))))
        {
            // and its riff as heard the intro's, bar by bar
            bars.AddRange(Rmg.Tests.Melodies.LoopLineReportTest.Bars(song, TrackRole.Riff));
            var form = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
            foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.LinesFrom))
            {
                over++;
                await Assert.That(form.Roles[entry.Section]).IsEqualTo(SectionRole.Verse).Because($"{genre} seed {song.Seed}");
                await Assert.That((int)entry.Value!).IsEqualTo(form.SectionIds[0]).Because($"{genre} seed {song.Seed}");
                await Assert.That(form.Roles[form.SectionIds[0]]).IsEqualTo(SectionRole.Intro).Because($"{genre} seed {song.Seed}");
            }
        }

        await Assert.That(over).IsGreaterThan(5);
        var rhythm = bars.Count(x => x.Rhythm) / (double)Math.Max(1, bars.Count);
        Console.WriteLine($"{over} verses over the intro's riff, {bars.Count} of their riff's bars, {rhythm:P0} on the intro's rhythm, {bars.Sum(x => x.Kept) / (double)Math.Max(1, bars.Sum(x => x.Notes)):P0} of their notes its");
        // a calibration, short of every bar as a figure answering the kick plays its own (measured: 76% of 38 bars and 100% of
        // 19, at offsets 0 and a million)
        await Assert.That(bars.Count).IsGreaterThan(10);
        await Assert.That(rhythm).IsGreaterThan(0.5);
    }

    /// <summary>A riff section plays its riff in most of its appearances, short of every one, as a section rests its riff now and then (seen 74% and 86% in 64 songs each of Rock and Metal).</summary>
    [Test]
    public async Task ARiffSection_PlaysItsRiff()
    {
        var appearances = new[] { "metal", "rock" }.SelectMany(genre => TestCorpus.InParallel(Enumerable.Range(0, 64), seed => Appearances(TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))))).SelectMany(x => x).ToArray();
        Console.WriteLine($"  a riff section's riff heard in {appearances.Count(x => x) / (double)appearances.Length:P0} of its {appearances.Length} appearances");
        await Assert.That(appearances.Count(x => x) / (double)appearances.Length).IsGreaterThan(0.6);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var genre in Genres)
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre))));
            var without = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.With(genre, GenreChoices.RiffSection, ("songs", 0)))));
            var appearances = songs.Select(Appearances).Where(x => x.Length > 0).ToArray();
            Console.WriteLine($"  {genre}: a riff section in {appearances.Length} of 64 songs, its riff heard in {appearances.SelectMany(x => x).Count(x => x) / (double)Math.Max(1, appearances.Sum(x => x.Length)):P0} of its {appearances.Sum(x => x.Length)} appearances; " +
                              $"the songs {songs.Average(x => x.Rendered.Seconds):F0} s long, {without.Average(x => x.Rendered.Seconds):F0} s without");
            // and its verses over the intro's riff, as a loop of the intro's (LoopLineReportTest)
            var verses = songs.Select(x => x.Trace.Count(t => t.Point == TracePoints.LinesFrom)).Sum();
            var bars = songs.SelectMany(x => Melodies.LoopLineReportTest.Bars(x, TrackRole.Riff)).ToArray();
            Console.WriteLine($"    {verses} sections over the intro's riff, its riff's rhythm in {bars.Count(x => x.Rhythm) / (double)Math.Max(1, bars.Length):P0} of {bars.Length} bars, its pitches in {bars.Sum(x => x.Kept) / (double)Math.Max(1, bars.Sum(x => x.Notes)):P0} of notes");
        }
        await Task.CompletedTask;
    }
}
