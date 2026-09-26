using Rmg.Core.Composition;

namespace Rmg.Tests.SongGenerators;

/// <summary>The hi-hat keeps time: its cycles repeat, so most of its bars play the same figure in both halves.</summary>
public sealed class SongGeneratorTimekeepingTest
{
    [Test]
    public async Task HiHat_MostlyRepeatsItsFigure()
    {
        int bars = 0, repeating = 0;
        var hiHat = DrumGroups.GetTrackNumber(DrumDefinitions.HiHat);
        for (var seed = 0; seed < 40; seed++)
        {
            var map = TestCorpus.Get(seed).Song.TrackEventStateTimelineMap.TrackTimelineMap;
            if (!map.TryGetValue(hiHat, out var track))
                continue;

            foreach (var bar in track.EventTimeline.GroupBy(x => (int)(x.Position / 4)))
            {
                var positions = bar.Select(x => Math.Round(x.Position % 4, 3)).ToArray();
                var first = positions.Where(x => x < 2).ToArray();
                var second = positions.Where(x => x >= 2).Select(x => Math.Round(x - 2, 3)).ToArray();
                bars++;
                if (first.SequenceEqual(second))
                    repeating++;
            }
        }

        await Assert.That(bars).IsGreaterThan(0);
        await Assert.That(repeating / (double)bars).IsGreaterThan(0.6);
    }
}
