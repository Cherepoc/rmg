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

    [Test]
    public async Task ABuild_RollsTheSnare_TwiceAsFastInItsLastBar_BeforeTheSilence()
    {
        int builds = 0, rolled = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("electronic")))))
        {
            var snare = FormLayers.BuildRollDrums.Select(DrumGroups.GetTrackNumber).FirstOrDefault(song.Song.Notes!.ContainsKey, -1);
            if (snare < 0)
                continue;
            foreach (var at in song.Trace.Where(x => x.Point == TracePoints.Drop).Select(x => (double)x.Value!))
            {
                builds++;
                var bar = song.Song.Map!.MeterAt(at - 1e-6).BarDuration;
                int Hits(double from, double to) => song.Song.Notes[snare].Count(x => x.Position >= from - 1e-9 && x.Position < to - 1e-9);
                // its last bar before the silence in 16ths, twice the bar before's 8ths
                if (Hits(at - 2 * bar, at - bar) >= 4 * bar - 1 && Hits(at - 3 * bar, at - 2 * bar) >= 2 * bar - 1)
                    rolled++;
            }
        }

        Console.WriteLine($"{rolled} of {builds} builds rolled");
        await Assert.That(builds).IsGreaterThan(8);
        await Assert.That(rolled / (double)builds).IsGreaterThan(0.8);
    }
}
