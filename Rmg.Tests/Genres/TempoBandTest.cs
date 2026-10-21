using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>A genre's tempo in bands (<see cref="SongGenerator.TempoBands" />): a song in one third of its range, as a jazz record is a ballad, a medium or a burner.</summary>
public sealed class TempoBandTest
{
    private static double[] Tempos(Genre genre) =>
    [
        ..TestCorpus.InParallel(Enumerable.Range(0, 256), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre)))
            .Select(x => Meter.BeatsPerSecond(x.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.Tempo).GetEffectiveValueAt(x.Map.Sections[0].Start)) * 60)
    ];

    /// <summary>
    ///     Jazz's songs fast oftener with its bands, against the same songs over its range whole: its burners, above 180, and
    ///     its ballads, below 90, more, the space between them less, over 256 songs, as 128 once measured the ballads fewer
    ///     (seen at three corpora: above 180 17-25% against 9-12%, below 90 24-29% against 15-23%).
    /// </summary>
    [Test]
    public async Task JazzsBands_PlayBurnersAndBalladsOftener_ThanItsRangeWhole()
    {
        var (bands, whole) = (Tempos(GenrePresets.Genre("jazz")), Tempos(GenrePresets.Without("jazz", GenreChoices.TempoBand.Name)));
        double Share(double[] tempos, Func<double, bool> where) => tempos.Count(where) / (double)tempos.Length;
        Console.WriteLine($"Jazz: above 180 {Share(bands, x => x >= 180):P0} (whole {Share(whole, x => x >= 180):P0}), below 90 {Share(bands, x => x < 90):P0} ({Share(whole, x => x < 90):P0}), 100 to 120 {Share(bands, x => x is >= 100 and < 120):P0} ({Share(whole, x => x is >= 100 and < 120):P0})");
        await Assert.That(Share(bands, x => x >= 180)).IsGreaterThan(Share(whole, x => x >= 180));
        await Assert.That(Share(bands, x => x < 90)).IsGreaterThan(Share(whole, x => x < 90));
    }
}
