using Rmg.Core;
using Rmg.Core.Events;
using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Forms;

/// <summary>A build and its drop: the band out for the bar before a lift, but for the drums' fill, and back on it.</summary>
public sealed class DropTest
{
    // the electronic songs, made once for the tests of this class
    // House's songs asked to drop at every lift and silently, as its own drop seldom and filtered oftener
    private static readonly Lazy<CorpusSong[]> Electronic = new(() =>
    {
        var genre = GenrePresets.With("electronic", GenreChoices.Drop, ("lifts", 1), ("rolls", 1));
        genre = genre with { Choices = genre.Choices.SetItem(GenreChoices.DropKind.Name, GenrePresets.With("electronic", GenreChoices.DropKind, ("silent", 1), ("filtered", 0)).Choices[GenreChoices.DropKind.Name]) };
        return TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre)));
    });

    [Test]
    public async Task ADrop_SilencesTheBand_ForTheBarBeforeIt_AndTheBandComesBackInItsFirstBar()
    {
        var drops = 0;
        foreach (var song in Electronic.Value)
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
        foreach (var song in Electronic.Value)
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

    /// <summary>A genre whose builds roll no snare, Djent's, its drops built to by the band alone.</summary>
    [Test]
    public async Task ABuild_AGenreRollsNot_LeavesTheSnareOutOfIt()
    {
        var genre = GenrePresets.With("electronic", GenreChoices.Drop, ("lifts", 1), ("rolls", 0));
        genre = genre with { Choices = genre.Choices.SetItem(GenreChoices.DropKind.Name, GenrePresets.With("electronic", GenreChoices.DropKind, ("silent", 1), ("filtered", 0)).Choices[GenreChoices.DropKind.Name]) };
        var drops = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))))
        {
            var snare = FormLayers.BuildRollDrums.Select(DrumGroups.GetTrackNumber).FirstOrDefault(song.Song.Notes!.ContainsKey, -1);
            foreach (var at in song.Trace.Where(x => x.Point == TracePoints.Drop).Select(x => (double)x.Value!))
            {
                drops++;
                var bar = song.Song.Map!.MeterAt(at - 1e-6).BarDuration;
                // no bar of 32nds before the silence
                await Assert.That(snare < 0 || song.Song.Notes[snare].Count(x => x.Position >= at - 2 * bar - 1e-9 && x.Position < at - bar - 1e-9) < 8 * bar - 1).IsTrue().Because($"seed {song.Seed} at {at}");
            }
        }

        await Assert.That(drops).IsGreaterThan(8);
    }

    [Test]
    public async Task AFilteredDrop_KeepsTheBandPlaying_TheMixClosingOverTheBarBefore_AndOpeningOnIt()
    {
        var drops = 0;
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("techno"))));
        foreach (var song in songs)
        foreach (var at in song.Trace.Where(x => x.Point == TracePoints.FilteredDrop).Select(x => (double)x.Value!))
        {
            drops++;
            var bar = song.Song.Map!.MeterAt(at - 1e-6).BarDuration;
            // the band playing on there, the drums at least, where a section of drums alone drops into the next, its notes
            // sounding in the bar, a sparse section's held
            var played = song.Song.Notes!.Values.SelectMany(x => x).ToArray();
            await Assert.That(played.Count(x => x.Position < at - 1e-9 && x.Position + x.Value.Duration > at - bar + 1e-9)).IsGreaterThan(2).Because($"seed {song.Seed} at {at}");
            var mix = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(CompositionStateKinds.MixClosed);
            await Assert.That(mix.GetEffectiveValueAt(at - 0.25)).IsGreaterThan(0.8).Because($"seed {song.Seed} at {at}");
            await Assert.That(mix.GetEffectiveValueAt(at)).IsLessThan(0.05).Because($"seed {song.Seed} at {at}");
        }

        var silent = songs.Sum(x => x.Trace.Count(t => t.Point == TracePoints.Drop));
        Console.WriteLine($"of 32 Techno songs' drops, {drops} filtered and {silent} silent");
        await Assert.That(drops).IsGreaterThan(5);
    }

    /// <summary>
    ///     Every drop, silent or filtered, takes one riser ending on it, on a channel of its own beside its part's, so that
    ///     over a filtered drop the part plays on under it.
    /// </summary>
    [Test]
    public async Task EveryDrop_TakesARiser_OnAChannelOfItsOwn_ItsPartPlayingOnOverAFilteredOne()
    {
        int silent = 0, filtered = 0, playingOn = 0;
        // Techno's songs asked to drop at every lift, as its own drop seldom
        var genre = GenrePresets.With("techno", GenreChoices.Drop, ("lifts", 1), ("rolls", 1));
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre)));
        foreach (var song in songs)
        {
            var risers = song.Rendered.Tracks.Where(x => x.IsRiser).ToArray();
            var filteredDrops = song.Trace.Where(x => x.Point == TracePoints.FilteredDrop).Select(x => (double)x.Value!).ToHashSet();
            var drops = song.Trace.Where(x => x.Point == TracePoints.Drop).Select(x => (double)x.Value!).Concat(filteredDrops).ToArray();
            if (drops.Length == 0)
            {
                await Assert.That(risers).IsEmpty();
                continue;
            }

            // one part carries them all, on a channel apart from its own and its second take's
            await Assert.That(risers.Length).IsEqualTo(1).Because($"seed {song.Seed}");
            var riser = risers[0];
            await Assert.That(riser.PitchInstrumentCode).IsEqualTo(Risers.Program);
            await Assert.That(song.Rendered.GetPartChannels()[riser.Role]).IsNotEqualTo(song.Rendered.GetRiserChannels()[riser.Role]);
            var own = song.Rendered.Tracks.Single(x => x.Role == riser.Role && !x.IsDouble && !x.IsRiser);
            foreach (var drop in drops)
            {
                var bar = song.Song.Map!.MeterAt(drop - 1e-6).BarDuration;
                var swing = Rmg.Core.Rendering.Render.GetSwing(song.Song);
                // its swell over the bar before, ending on the drop
                await Assert.That(riser.NoteTimeline.Count(x => Math.Abs(x.Position + x.Value.Duration - swing.Apply(drop)) < 1e-6)).IsEqualTo(1).Because($"seed {song.Seed} at {drop}");
                if (!filteredDrops.Contains(drop))
                {
                    silent++;
                    continue;
                }

                filtered++;
                if (own.NoteTimeline.Any(x => x.Position >= swing.Apply(drop - bar) - 1e-9 && x.Position < swing.Apply(drop) - 1e-9))
                    playingOn++;
            }
        }

        Console.WriteLine($"of 32 Techno songs' drops, {silent} silent and {filtered} filtered, the riser's part playing on under {playingOn} of these");
        // the riser's part playing on under a filtered drop, a calibration (measured over 32: 21 of 62, and 15 of some 65 at offset 716000000)
        await Assert.That(playingOn).IsGreaterThan(filtered / 8);
        await Assert.That(silent).IsGreaterThan(5);
        await Assert.That(filtered).IsGreaterThan(5);
    }

    /// <summary>A drop whose bar before it is no section's, an intro's, still takes its riser by the meter there.</summary>
    [Test]
    public async Task ADrop_AfterAnIntro_TakesItsRiser()
    {
        // a song found to drop in after its intro, a Trap opening on its hook, which no preset's songs both do, its case
        // asserted so that it fails rather than passes untested once the songs are drawn again
        using var trace = StateTrace.Start(explains: false);
        var song = SongGenerator.GenerateSong(5730926807999, new SongOverrides(Genre: GenrePresets.With("trap", GenreChoices.Intro, ("hook", 1))));
        await Assert.That(song.Map!.Origin).IsGreaterThan(0);
        await Assert.That(trace.Entries.Any(x => x.Point is TracePoints.Drop or TracePoints.FilteredDrop && Math.Abs((double)x.Value! - song.Map.Origin) < 1e-9)).IsTrue();
        await Assert.That(song.Notes!.Values.SelectMany(x => x).Any(x => x.Position < song.Map.Origin && x.Value.State.GetStateValue(CompositionStateKinds.Program) == Risers.Program + 1)).IsTrue();
    }
}
