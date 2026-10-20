using Rmg.Core.Composition;

namespace Rmg.Tests.Melodies;

/// <summary>How often a riff's last bar opens as its first, a turnaround's measure (<see cref="LineFills.TurnaroundChance" />).</summary>
public sealed class RiffTurnaroundReportTest
{
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
}
