using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Grooves;

/// <summary>
///     A drawn swing evening out with the tempo: its pair's second note never shorter than <see cref="Groove.ShortestSecondNote" />
///     at the song's tempo, a section at a faster tempo of its own playing it a little shorter.
/// </summary>
public sealed class SwingTempoTest
{
    /// <summary>How long a song's swung pair's second note plays, in seconds; none for a straight song.</summary>
    private static double? SecondNote(CorpusSong song)
    {
        if (song.Swing.Delay <= 0)
            return null;
        var tempo = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.Tempo).GetEffectiveValueAt(0);
        return (song.Swing.Period / 2 - song.Swing.Delay) * Meter.SecondsPerBeat(tempo);
    }

    [Test]
    public async Task ADrawnSwing_LeavesItsSecondNoteATenthOfASecond_AtTheSongsTempo()
    {
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("jazz"))));
        var swung = songs.Select(x => (Song: x, Second: SecondNote(x))).Where(x => x.Second is not null).ToArray();
        var tempos = swung.Select(x => x.Song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.Tempo).GetEffectiveValueAt(0) * Meter.BaseTempo).ToArray();
        Console.WriteLine($"{swung.Length} of 64 swung; the second note {swung.Min(x => x.Second!.Value) * 1000:0} ms at the shortest; " +
                          $"the fastest, their tempo, the delay as the triplet's share and the second note: {string.Join(" ", swung.Zip(tempos).OrderByDescending(x => x.Second).Take(8)
                              .Select(x => $"{x.Second:0} bpm {6 * x.First.Song.Swing.Delay / x.First.Song.Swing.Period:0.00} {x.First.Second!.Value * 1000:0} ms"))}");
        await Assert.That(swung.Length).IsGreaterThan(16);
        foreach (var (song, second) in swung)
            await Assert.That(second!.Value).IsGreaterThanOrEqualTo(Groove.ShortestSecondNote - 1e-9).Because($"seed {song.Seed}");
    }
}
