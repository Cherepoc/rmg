using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Forms;

/// <summary>A build and its drop: the band out for the bar before a lift, but for the drums' fill, and back on it.</summary>
public sealed class DropTest
{
    [Test]
    public async Task ADrop_SilencesTheBand_ForTheBarBeforeIt_AndTheBandComesBackInItsFirstBar()
    {
        var drops = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("electronic")))))
        foreach (var at in song.Trace.Where(x => x.Point == TracePoints.Drop).Select(x => (double)x.Value!))
        {
            drops++;
            var from = at - song.Song.Map!.MeterAt(at - 1e-6).BarDuration;
            // but the riser into it (Risers.Apply)
            var pitched = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key] is PitchInstrumentTrack).SelectMany(x => x.Value)
                .Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Program) != Risers.Program + 1).ToArray();
            // nothing struck in the silent bar, its first beat too, nor a long note ringing into it
            await Assert.That(pitched.Where(x => x.Position >= from - 1e-9 && x.Position < at - 1e-9
                                                 || x.Position < from - 1e-9 && x.Value.Duration > 0.25 && x.Position + x.Value.Duration > from + 1e-9)).IsEmpty();
            // back in the drop's first bar, a part whose pattern rests on the downbeat on its next beat, the drums on it
            await Assert.That(pitched.Any(x => x.Position >= at - 1e-9 && x.Position < at + song.Song.Map!.MeterAt(at).BarDuration - 1e-9)).IsTrue().Because($"seed {song.Seed} at {at}");
        }

        await Assert.That(drops).IsGreaterThan(8);
    }

    [Test]
    public async Task ABuild_RollsTheSnare_TwiceAsFastEachBar_From8thsTo32nds_BeforeTheSilence()
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
                // its last bar before the silence in 32nds, the bar before in 16ths and the one before that in 8ths
                if (Hits(at - 2 * bar, at - bar) >= 8 * bar - 1 && Hits(at - 3 * bar, at - 2 * bar) >= 4 * bar - 1 && Hits(at - 4 * bar, at - 3 * bar) >= 2 * bar - 1)
                    rolled++;
            }
        }

        Console.WriteLine($"{rolled} of {builds} builds rolled");
        await Assert.That(builds).IsGreaterThan(8);
        await Assert.That(rolled / (double)builds).IsGreaterThan(0.8);
    }

    [Test]
    public async Task AFilteredDrop_KeepsTheBandPlaying_TheMixClosingOverTheBarBefore_AndOpeningOnIt()
    {
        var drops = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("techno")))))
        foreach (var at in song.Trace.Where(x => x.Point == TracePoints.FilteredDrop).Select(x => (double)x.Value!))
        {
            drops++;
            var bar = song.Song.Map!.MeterAt(at - 1e-6).BarDuration;
            // the band playing on there, the drums at least, where a section of drums alone drops into the next
            var played = song.Song.Notes!.Values.SelectMany(x => x).ToArray();
            await Assert.That(played.Count(x => x.Position >= at - bar - 1e-9 && x.Position < at - 1e-9)).IsGreaterThan(2).Because($"seed {song.Seed} at {at}");
            var mix = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(CompositionStateKinds.MixClosed);
            await Assert.That(mix.GetEffectiveValueAt(at - 0.25)).IsGreaterThan(0.8).Because($"seed {song.Seed} at {at}");
            await Assert.That(mix.GetEffectiveValueAt(at)).IsLessThan(0.05).Because($"seed {song.Seed} at {at}");
        }

        var silent = TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("techno"))))
            .Sum(x => x.Trace.Count(t => t.Point == TracePoints.Drop));
        Console.WriteLine($"of 32 Techno songs' drops, {drops} filtered and {silent} silent");
        await Assert.That(drops).IsGreaterThan(5);
    }

    /// <summary>A drop whose bar before it is no section's, an intro's, still takes its riser by the meter there.</summary>
    [Test]
    public async Task ADrop_AfterAnIntro_TakesItsRiser()
    {
        var song = SongGenerator.GenerateSong(5730926807999, new SongOverrides(Genre: GenrePresets.Genre("trap")));
        await Assert.That(song.Notes!.Values.SelectMany(x => x).Any(x => x.Value.State.GetStateValue(CompositionStateKinds.Program) == Risers.Program + 1)).IsTrue();
    }
}
