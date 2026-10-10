using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>A pump (<see cref="StrongEffect.Pump" />): the pads, the chords and the bass ducked on every beat and swelling back.</summary>
public sealed class PumpTest
{
    [Test]
    public async Task APumpingSongsPads_DuckOnTheBeat_AndSwellBackOverIt()
    {
        var pumping = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("electronic"))))
            .Where(x => ((SongEffects)x.Trace.Single(t => t.Point == TracePoints.SongEffects).Value!).Strong == StrongEffect.Pump)
            .ToArray();
        await Assert.That(pumping.Length).IsGreaterThan(3);
        int onBeat = 0, ducked = 0, offBeat = 0, full = 0;
        foreach (var song in pumping)
        foreach (var track in song.Rendered.Tracks.Where(x => x.Role is TrackRole.Pad or TrackRole.Chords or TrackRole.Bass))
        foreach (var (position, value) in track.Expression)
        {
            // just after a beat ducked, just before the next swelled back
            var inBeat = position % 1;
            if (inBeat < 0.1) { onBeat++; ducked += value < 0.6 ? 1 : 0; }
            else if (inBeat > 0.85) { offBeat++; full += value > 0.9 ? 1 : 0; }
        }

        await Assert.That(onBeat).IsGreaterThan(100);
        Console.WriteLine($"{pumping.Length} songs pumping: ducked on the beat {ducked / (double)onBeat:P0}, full before the next {full / (double)offBeat:P0}");
        // measured: ducked 89% to 90%, full 100%, at offsets 0 and a million
        await Assert.That(ducked / (double)onBeat).IsGreaterThan(0.8);
        await Assert.That(full / (double)offBeat).IsGreaterThan(0.8);
    }
}
