using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Drums;

/// <summary>Double time: the kick and the snare doubling, the drum keeping time no finer than 8ths, as a skank beat's hats.</summary>
public sealed class DoubleTimeTest
{
    [Test]
    public async Task InDoubleTime_TheDrumKeepingTime_PlaysNoFinerThanEighths()
    {
        var timekeepers = DrumGroups.Timekeepers.Drums.Select(DrumGroups.GetTrackNumber).ToHashSet();
        var (gaps, sixteenths) = (0, 0);
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("thrash")))))
        foreach (var feel in song.Trace.Where(t => t.Point == TracePoints.TimeFeel && (int)t.Value! == -1))
        foreach (var span in song.Map.Sections.Where(x => x.SectionId == feel.Section))
        foreach (var track in timekeepers.Where(song.Song.Notes!.ContainsKey))
        {
            var onsets = song.Song.Notes![track].Select(x => x.Position).Where(x => x >= span.Start - 1e-9 && x < span.End - 1e-9).Distinct().Order().ToArray();
            foreach (var gap in onsets.Zip(onsets.Skip(1)).Select(x => x.Second - x.First))
                (gaps, sixteenths) = (gaps + 1, sixteenths + (gap < 0.5 - 1e-6 ? 1 : 0));
        }

        Console.WriteLine($"{gaps} gaps between a time drum's notes in double time, {sixteenths / (double)Math.Max(1, gaps):P1} shorter than an 8th");
        // measured 3.4% at offset 0 and 6.0% at a million, 53% of the hats' gaps 16ths before the cap; fills and rolls may
        // add a few
        await Assert.That(gaps).IsGreaterThan(500);
        await Assert.That(sixteenths / (double)gaps).IsLessThan(0.1);
    }
}
