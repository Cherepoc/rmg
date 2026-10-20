using Rmg.Core.Composition;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Melodies;

/// <summary>How far a looping section's riff and bass are the section's it loops: a loop played as one groove.</summary>
public sealed class LoopLineReportTest
{
    /// <summary>
    ///     Every bar of a looping section's first appearance, of a part, two notes or more, against the bar of the
    ///     section whose chords it plays, the song's first: whether it starts its notes where that does, and how
    ///     many of its notes play that one's pitch at the same place, of how many.
    /// </summary>
    internal static (bool Rhythm, int Kept, int Notes)[] Bars(CorpusSong song, TrackRole part) => Bars(song, part, true);

    /// <summary>As <see cref="Bars(CorpusSong, TrackRole)" />, of a looping section's or, for none, of every other section's but the first's, against the first's.</summary>
    private static (bool Rhythm, int Kept, int Notes)[] Bars(CorpusSong song, TrackRole part, bool looping)
    {
        if (!song.Song.Notes!.TryGetValue(song.Track(part), out var notes))
            return [];
        // where it loops, the section whose chords it plays, the song's first, as its trace has it
        var sources = song.Trace.Where(x => x.Point == TracePoints.LinesFrom).ToDictionary(x => x.Section, x => (int)x.Value!);
        var firsts = song.PlayedSpans.Where(x => !song.IsSolo(x)).GroupBy(x => x.SectionId).Select(x => x.First()).ToArray();
        return
        [
            ..firsts.Where(x => sources.ContainsKey(x.SectionId) == looping && (looping || sources.Count > 0 && x.SectionId != sources.Values.First())).SelectMany(span =>
            {
                if (firsts.FirstOrDefault(x => x.SectionId == (looping ? sources[span.SectionId] : sources.Values.First())) is not { } source || !source.Meter.Equals(span.Meter))
                    return [];
                var bar = span.Meter.BarDuration;
                (double Offset, int Pitch)[] Bar(SectionSpan at, int k) =>
                [
                    ..notes.Where(x => x.Position >= at.Start + k * bar - 1e-9 && x.Position < at.Start + (k + 1) * bar - 1e-9)
                        .Select(x => (Math.Round(x.Position - at.Start - k * bar, 3), x.Value.Pitches[0]))
                ];
                return Enumerable.Range(0, span.Meter.PatternBarCount).Select(k => (Bar(source, k), Bar(span, k))).Where(x => x.Item1.Length >= 2 && x.Item2.Length >= 2)
                    .Select(x => (x.Item1.Select(n => n.Offset).SequenceEqual(x.Item2.Select(n => n.Offset)), x.Item2.Count(n => x.Item1.Contains(n)), x.Item2.Length));
            })
        ];
    }

    /// <summary>
    ///     A looping section's riff and bass are the first section's: in the same songs, their bars start their notes where
    ///     the first's do far more often than another section's (seen 62% against 16% of 64 songs each of Techno, the Blues
    ///     and Dub), short of every bar, as a figure that answers the kick plays its own rhythm where the section's kick rests.
    /// </summary>
    [Test]
    public async Task ALoopsRiffAndBass_PlayTheFirstSections()
    {
        var songs = new[] { "techno", "blues", "dub" }.SelectMany(genre => TestCorpus.InParallel(Enumerable.Range(0, 64),
            seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(GenrePresets.All.Single(x => x.Id == genre)))))).ToArray();
        double Same(bool looping)
        {
            var bars = songs.SelectMany(x => new[] { TrackRole.Riff, TrackRole.Bass }.SelectMany(part => Bars(x, part, looping))).ToArray();
            return bars.Count(x => x.Rhythm) / (double)Math.Max(1, bars.Length);
        }

        var (looping, other) = (Same(true), Same(false));
        Console.WriteLine($"  looping sections' riff and bass on the first's rhythm in {looping:P0}, the others' in {other:P0}");
        await Assert.That(looping).IsGreaterThan(other + 0.25);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var genre in new[] { "techno", "hipHop", "blues", "funk", "dub", "pop" })
        {
            var overrides = new SongOverrides(Genre: GenrePresets.Genre(GenrePresets.All.Single(x => x.Id == genre)));
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, overrides));
            foreach (var part in new[] { TrackRole.Riff, TrackRole.Bass })
            {
                var bars = songs.SelectMany(x => Bars(x, part)).ToArray();
                Console.WriteLine($"  {genre}'s {part}: {bars.Length} bars of looping sections, the looped section's rhythm in {bars.Count(x => x.Rhythm) / (double)Math.Max(1, bars.Length):P0}, " +
                                  $"its pitches in {bars.Sum(x => x.Kept) / (double)Math.Max(1, bars.Sum(x => x.Notes)):P0} of notes");
            }
        }
        await Task.CompletedTask;
    }
}
