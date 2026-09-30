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
        for (var seed = 0; seed < 32; seed++)
        {
            // in a meter of two halves alike, as 4/4 and 6/8 are, and a song in straight time
            var corpusSong = TestCorpus.Get(seed);
            var song = corpusSong.Song;
            var meter = song.Map!.Meter;
            var map = song.TrackEventStateTimelineMap.TrackTimelineMap;
            var feel = (int)corpusSong.Trace.First(x => x.Point == TracePoints.Feel).Value!;
            if (!map.TryGetValue(hiHat, out var track) || meter.Groups.Length != 2 || meter.Groups[0] != meter.Groups[1] || feel != Feels.Straight)
                continue;

            var (length, half) = (meter.BarDuration, meter.BarDuration / 2);
            foreach (var bar in track.EventTimeline.GroupBy(x => (int)(x.Position / length)))
            {
                var positions = bar.Select(x => Math.Round(x.Position % length, 3)).ToArray();
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
