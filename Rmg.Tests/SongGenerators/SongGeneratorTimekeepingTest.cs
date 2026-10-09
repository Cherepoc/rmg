using Rmg.Core.Composition;

namespace Rmg.Tests.SongGenerators;

/// <summary>
///     The hi-hat keeps time: its cycles repeat, so most of its bars play the same figure in both halves, where the song is
///     in straight time; one in a tuplet or a grouping, such as fives, does not split its bars in halves.
/// </summary>
public sealed class SongGeneratorTimekeepingTest
{
    [Test]
    public async Task HiHat_MostlyRepeatsItsFigure()
    {
        int bars = 0, repeating = 0;
        var hiHat = DrumGroups.GetTrackNumber(DrumDefinitions.HiHat);
        for (var seed = 0; seed < 256; seed++)
        {
            // a song in straight time, its sections in a meter of two halves alike, as 4/4 and 6/8 are, that keep the song's
            // feel, bar by bar from each one's start
            var corpusSong = TestCorpus.Get(seed);
            var song = corpusSong.Song;
            var map = song.TrackEventStateTimelineMap.TrackTimelineMap;
            var feel = (int)corpusSong.Trace.First(x => x.Point == TracePoints.Feel).Value!;
            if (!map.TryGetValue(hiHat, out var track) || feel != Feels.Straight)
                continue;
            var ownFeels = corpusSong.Trace.Where(x => x.Point == TracePoints.Feel && x.Track == SectionGenerator.SectionTrace).Select(x => x.Section).ToHashSet();

            foreach (var span in corpusSong.Map.Sections.Where(x => x.Meter.Groups.Length == 2 && x.Meter.Groups[0] == x.Meter.Groups[1] && !ownFeels.Contains(x.SectionId)))
            foreach (var bar in track.EventTimeline.Where(x => x.Position >= span.Start && x.Position < span.End).GroupBy(x => (int)((x.Position - span.Start) / span.Meter.BarDuration)))
            {
                var (length, half) = (span.Meter.BarDuration, span.Meter.BarDuration / 2);
                var positions = bar.Select(x => Math.Round((x.Position - span.Start) % length, 3)).ToArray();
                var first = positions.Where(x => x < half).ToArray();
                var second = positions.Where(x => x >= half).Select(x => Math.Round(x - half, 3)).ToArray();
                bars++;
                if (first.SequenceEqual(second))
                    repeating++;
            }
        }

        await Assert.That(bars).IsGreaterThan(0);
        await Assert.That(repeating / (double)bars).IsGreaterThan(0.6);
    }
}
