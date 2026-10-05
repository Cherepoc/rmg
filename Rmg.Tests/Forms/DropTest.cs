using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Forms;

/// <summary>A build and its drop: the band out for the bar before a lift, but for the drums' fill, and back on it.</summary>
public sealed class DropTest
{
    [Test]
    public async Task ADrop_SilencesTheBand_ForTheBarBeforeIt_AndTheBandComesBackOnIt()
    {
        var drops = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("electronic")))))
        foreach (var at in song.Trace.Where(x => x.Point == TracePoints.Drop).Select(x => (double)x.Value!))
        {
            drops++;
            var from = at - song.Song.Map!.MeterAt(at - 1e-6).BarDuration;
            var pitched = song.Rendered.Tracks.Where(x => !x.IsPercussionInstrument).SelectMany(x => x.NoteTimeline).ToArray();
            await Assert.That(pitched.Where(x => x.Position < at - 1e-9 && x.Position + (x.Value.Duration > 0.25 ? x.Value.Duration : 0) > from + 1e-9)).IsEmpty();
            await Assert.That(pitched.Any(x => x.Position >= at - 1e-9 && x.Position < at + 1)).IsTrue();
        }

        await Assert.That(drops).IsGreaterThan(8);
    }
}
