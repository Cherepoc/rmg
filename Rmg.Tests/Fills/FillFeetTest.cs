using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Fills;

/// <summary>A fill's feet (<see cref="FillLayers.Feet" />): a metal drummer's double kick, or the groove's kick, kept under a fill's run.</summary>
public sealed class FillFeetTest
{
    /// <summary>
    ///     A fill keeping a double kick under its run strikes the kick on every 16th from its start to its line, and one
    ///     keeping the groove's kick strikes it there as the groove does; the songs' own fills keep no feet.
    /// </summary>
    [Test]
    public async Task AFillsFeet_KeepTheKickUnderItsRun()
    {
        var kick = DrumGroups.GetTrackNumber(DrumDefinitions.Kick);
        var (doubles, grooves) = (0, 0);
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("thrash")))))
        foreach (var (from, line, feet) in song.Trace.Where(x => x.Point == TracePoints.FillFeet).Select(x => ((double, double, FillFeet))x.Value!))
        {
            var kicks = song.Song.TrackEventStateTimelineMap.TrackTimelineMap[kick].EventTimeline.Where(x => x.Position >= from - 1e-9 && x.Position < line - 1e-9).Select(x => Math.Round(x.Position * 4)).ToHashSet();
            if (feet == FillFeet.DoubleKick)
            {
                doubles++;
                for (var at = Math.Ceiling(from * 4 - 1e-9); at < line * 4 - 1e-9; at++)
                    await Assert.That(kicks.Contains(at)).IsTrue().Because($"seed {song.Seed}, a double kick's 16th at {at / 4}");
            }
            else
                grooves++;
        }

        Console.WriteLine($"{doubles} fills over a double kick, {grooves} over the groove's kick, in 64 Thrash songs");
        await Assert.That(doubles).IsGreaterThan(10);
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed)))
            await Assert.That(song.Trace.Any(x => x.Point == TracePoints.FillFeet)).IsFalse().Because($"seed {song.Seed}");
    }
}
