using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.Melodies;

/// <summary>How faithfully a hooked riff keeps its figure: its repeated bars against their letter's first, as played.</summary>
public sealed class RiffHookReportTest
{
    /// <summary>
    ///     Every repeated bar of a hooked riff, the first time its section plays, against its letter's first bar in its
    ///     pattern: whether its chord's root is another, whether it starts its notes where the first does, whether its notes
    ///     move the same way, and how many of its notes play the first's pitch at the same place, of how many.
    /// </summary>
    internal static (bool OtherChord, bool Rhythm, bool Contour, int Kept, int Notes)[] Repeats(CorpusSong song)
    {
        if (!song.Song.Notes!.TryGetValue(song.Track(TrackRole.Riff), out var notes))
            return [];
        var common = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap;
        return
        [
            ..song.PlayedSpans.GroupBy(x => x.SectionId).Select(x => x.First()).SelectMany(span =>
            {
                var bar = span.Meter.BarDuration;
                var bars = Enumerable.Range(0, (int)Math.Round(span.Duration / bar)).Select(k =>
                {
                    var start = span.Start + k * bar;
                    var played = notes.Where(x => x.Position >= start - 1e-9 && x.Position < start + bar - 1e-9).ToArray();
                    var letter = played.Select(x => x.Value.State.GetStateValue(CompositionStateKinds.LineHookBar)).FirstOrDefault(x => x > 0);
                    return (Pattern: (int)Math.Floor(k * bar / span.Meter.PatternDuration + 1e-9), Letter: letter, Start: start, Played: played,
                        Root: common.GetEffectiveStateMapAt(start).GetStateValue(StateKinds.ChordRoot));
                }).Where(x => x.Letter > 0 && x.Played.Length >= 2).ToArray();
                return bars.GroupBy(x => (x.Pattern, x.Letter)).SelectMany(group =>
                {
                    var first = group.First();
                    double Offset(double position, double start) => Math.Round(position - start, 3);
                    string Onsets((int, int, double Start, TimelineItem<RealizedNote>[] Played, int) b) => string.Join(",", b.Played.Select(x => Offset(x.Position, b.Start)));
                    string Contour(TimelineItem<RealizedNote>[] played) => string.Join(",", played.Zip(played.Skip(1), (a, b) => Math.Sign(b.Value.Pitches[0] - a.Value.Pitches[0])));
                    var pitches = first.Played.GroupBy(x => Offset(x.Position, first.Start)).ToDictionary(x => x.Key, x => x.First().Value.Pitches[0]);
                    return group.Skip(1).Select(b => (b.Root != first.Root, Onsets(b) == Onsets(first), Contour(b.Played) == Contour(first.Played),
                        b.Played.Count(x => pitches.TryGetValue(Offset(x.Position, b.Start), out var p) && p == x.Value.Pitches[0]), b.Played.Length));
                });
            })
        ];
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var bars = TestCorpus.Measure(256, Repeats).SelectMany(x => x).ToArray();
        foreach (var group in bars.GroupBy(x => x.OtherChord).OrderBy(x => x.Key))
            Console.WriteLine($"  {group.Count()} repeated bars of a hooked riff over {(group.Key ? "another chord" : "the same chord")}: the first's rhythm in {group.Count(x => x.Rhythm) / (double)group.Count():P0}, " +
                              $"its contour in {group.Count(x => x.Contour) / (double)group.Count():P0}, its pitches in {group.Sum(x => x.Kept) / (double)group.Sum(x => x.Notes):P0} of notes");
        await Task.CompletedTask;
    }
}
