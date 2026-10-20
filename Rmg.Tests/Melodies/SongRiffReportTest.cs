using Rmg.Core.Composition;

namespace Rmg.Tests.Melodies;

/// <summary>How far a song's riff is one riff: whether its sections, each the first time it plays, open on the same figure.</summary>
public sealed class SongRiffReportTest
{
    /// <summary>
    ///     Every pair of a song's sections of one meter whose riff plays in its first half bar, two notes or more, before the
    ///     bar's end leads into the next chord as each section's chords ask: whether the half bars' onsets are the same, and whether their notes move the same way too, up, down or not, as a figure moved by scale steps
    ///     over another chord does.
    /// </summary>
    internal static (bool Rhythm, bool Figure, bool Both)[] Pairs(CorpusSong song)
    {
        if (!song.Song.Notes!.TryGetValue(song.Track(TrackRole.Riff), out var notes))
            return [];
        var playing = song.Trace.Where(x => x.Point == TracePoints.SongRiff).Select(x => x.Section).ToHashSet();
        var firsts = song.PlayedSpans.GroupBy(x => x.SectionId).Select(x => x.First()).Select(span =>
        {
            // its first half, before its last notes lead into the next chord as each section's chords ask
            var bar = notes.Where(x => x.Position >= span.Start - 1e-9 && x.Position < span.Start + span.Meter.BarDuration / 2 - 1e-9).ToArray();
            return (span.Meter, span.SectionId, Onsets: string.Join(",", bar.Select(x => Math.Round(x.Position - span.Start, 3))),
                Contour: string.Join(",", bar.Zip(bar.Skip(1), (a, b) => Math.Sign(b.Value.Pitches[0] - a.Value.Pitches[0]))), bar.Length);
        }).Where(x => x.Length >= 2).ToArray();
        return
        [
            ..firsts.SelectMany((a, i) => firsts.Skip(i + 1).Where(b => b.Meter == a.Meter).Select(b => (a.Onsets == b.Onsets, a.Onsets == b.Onsets && a.Contour == b.Contour, playing.Contains(a.SectionId) && playing.Contains(b.SectionId))))
        ];
    }

    /// <summary>
    ///     A section playing the song's riff opens on the riff the song played first: in the same songs, the pairs of sections
    ///     one of which plays it share their first half bar's rhythm far more often than those neither of which does (seen 75%
    ///     against 0% of 5 in 128 songs; of every pair in 256 songs 50%, against 12% with every section's riff its own).
    /// </summary>
    [Test]
    public async Task ASectionPlayingTheSongsRiff_OpensOnIt()
    {
        var pairs = TestCorpus.InParallel(Enumerable.Range(0, 128), seed => Pairs(TestCorpus.Get(seed))).SelectMany(x => x).ToArray();
        double Same(bool plays) => pairs.Where(x => x.Both == plays).Count(x => x.Rhythm) / (double)Math.Max(1, pairs.Count(x => x.Both == plays));
        Console.WriteLine($"  {pairs.Count(x => x.Both)} pairs both playing the song's riff, the rhythm the same in {Same(true):P0}; {pairs.Count(x => !x.Both)} others, in {Same(false):P0}");
        await Assert.That(pairs.Count(x => x.Both)).IsGreaterThan(30);
        await Assert.That(Same(true)).IsGreaterThan(Same(false) + 0.25);
    }

    /// <summary>
    ///     Every section's riff the first time it plays, a pattern of four bars or more: whether its last bar opens, its
    ///     first half's onsets, as its first does, and whether its second does.
    /// </summary>
    internal static (bool Last, bool Second)[] Turnarounds(CorpusSong song)
    {
        if (!song.Song.Notes!.TryGetValue(song.Track(TrackRole.Riff), out var notes))
            return [];
        return
        [
            ..song.PlayedSpans.GroupBy(x => x.SectionId).Select(x => x.First()).Where(x => x.Meter.PatternBarCount >= 4).Select(span =>
            {
                string Opening(int bar)
                {
                    var start = span.Start + bar * span.Meter.BarDuration;
                    return string.Join(",", notes.Where(x => x.Position >= start - 1e-9 && x.Position < start + span.Meter.BarDuration / 2 - 1e-9).Select(x => Math.Round(x.Position - start, 3)));
                }

                var first = Opening(0);
                return (first != "" && Opening(span.Meter.PatternBarCount - 1) == first, first != "" && Opening(1) == first);
            })
        ];
    }

    [Test]
    [Explicit]
    public async Task TurnaroundReport()
    {
        var sections = TestCorpus.Measure(256, Turnarounds).SelectMany(x => x).ToArray();
        Console.WriteLine($"  {sections.Length} riffs: the last bar opening as the first in {sections.Count(x => x.Last) / (double)sections.Length:P0}, the second in {sections.Count(x => x.Second) / (double)sections.Length:P0}");
        await Task.CompletedTask;
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var pairs = TestCorpus.Measure(256, Pairs).SelectMany(x => x).ToArray();
        foreach (var group in pairs.GroupBy(x => x.Both).Prepend(pairs.GroupBy(_ => true).Single()))
            Console.WriteLine($"  {group.Count()} pairs of sections{(group.Count() == pairs.Length ? "" : group.Key ? ", both the song's riff" : ", not both")}: the riff's first half bar's rhythm the same in " +
                              $"{group.Count(x => x.Rhythm) / (double)group.Count():P0}, its figure in {group.Count(x => x.Figure) / (double)group.Count():P0}");
        await Task.CompletedTask;
    }
}
