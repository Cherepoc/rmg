using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Arrangements;

/// <summary>A drum solo played as a pitched solo is (<see cref="DrumSolos" />), the kit its scale, gesture by gesture by the drummer's vocabulary.</summary>
public sealed class DrumSoloTest
{
    /// <summary>Songs of wild solos, their form, melody and fills, where the drums solo as often as any part.</summary>
    private static SongOverrides Wild => new(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Form, 1).Add(Facet.Melody, 1).Add(Facet.Fills, 1));

    /// <summary>Every drum solo of a song, as the trace records it, with the section it plays in.</summary>
    private static (SectionSpan Span, SoloLines.Segment[] Plan, bool Feet, DrumSolo Solo)[] DrumSolos(CorpusSong song) =>
    [
        ..song.Trace.Where(x => x.Point == TracePoints.DrumSolo).Select(x => (DrumSolo)x.Value!)
            .Select(x => (song.Map.Sections[x.Index], x.Plan, x.Feet, x))
    ];

    /// <summary>Every drum solo of the kit's drummer alone, the others resting, as every song of the kit alone plays.</summary>
    private static (SectionSpan Span, SoloLines.Segment[] Plan, bool Feet, DrumSolo Solo)[] KitSolos(CorpusSong song) =>
        [..DrumSolos(song).Where(x => x.Solo.Soloists.SequenceEqual([0]) && x.Solo.Keepers.Length == 0)];

    private static readonly CorpusSong[] Songs = TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, Wild));

    /// <summary>Songs of wild solos with the kit and percussion, their groove the plainest and the wildest.</summary>
    private static CorpusSong[] WithPercussion(double groove) =>
        TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, Wild with { DrumSetup = DrumSetup.KitAndPercussion, Facets = Wild.Facets!.Add(Facet.Groove, groove) }));

    [Test]
    public async Task ADrumSolosFeet_KeepTimeInMost_TheSectionsKickSofter_ElseTheKickStackedUnderItsAccentsNowAndThen()
    {
        var kick = DrumGroups.GetTrackNumber(DrumDefinitions.Kick);
        var (solos, feet, kept, stacking, others) = (0, 0, 0, 0, 0);
        var (feetLoudness, handsLoudness) = (new List<double>(), new List<double>());
        foreach (var song in Songs)
        foreach (var (span, plan, keepsTime, _) in KitSolos(song))
        {
            var drums = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role == TrackRole.Drum).ToArray();
            var kicks = drums.Where(x => x.Key == kick).SelectMany(x => x.Value).ToArray();
            solos++;
            if (keepsTime)
            {
                // the section's kick under a pause, the hands silent, softer than the hands' calmest
                feet++;
                var pauses = plan.Where(x => x.Gesture == SoloGesture.Pause).ToArray();
                var under = kicks.Where(x => pauses.Any(p => x.Position >= p.Start - 1e-9 && x.Position < p.End - 1e-9)).ToArray();
                if (under.Length > 0)
                    kept++;
                feetLoudness.AddRange(under.Select(x => (double)x.Value.Velocity));
                handsLoudness.AddRange(drums.Where(d => d.Key != kick).SelectMany(d => d.Value).Where(h => h.Position >= span.Start && h.Position < plan[^1].End).Select(h => (double)h.Value.Velocity));
                continue;
            }

            // the kick struck with another drum in its runs and licks, which a cell's single hits never are
            others++;
            var runs = plan.Where(x => x.Gesture is SoloGesture.Run or SoloGesture.Lick).ToArray();
            var inRuns = kicks.Where(x => runs.Any(r => x.Position >= r.Start - 1e-9 && x.Position < r.End - 1e-9)).Select(x => Math.Round(x.Position, 6)).ToHashSet();
            var rest = drums.Where(x => x.Key != kick).SelectMany(x => x.Value).Select(x => Math.Round(x.Position, 6)).ToHashSet();
            if (inRuns.Any(rest.Contains))
                stacking++;
        }

        await Assert.That(solos).IsGreaterThan(10);
        await Assert.That(feet / (double)solos).IsBetween(0.55, 0.85);
        await Assert.That(kept).IsGreaterThan(feet / 3);
        await Assert.That(feetLoudness.Average()).IsLessThan(handsLoudness.Average());
        await Assert.That(stacking / (double)others).IsBetween(0.2, 0.8);
    }

    [Test]
    public async Task ADrumSolosClimax_StartsItsRunsOnACrash_ItsCalmerOnesNot()
    {
        var crashes = new HashSet<int> { DrumGroups.GetTrackNumber(DrumDefinitions.Cymbal) };
        var (climax, crashed, calm, calmCrashed) = (0, 0, 0, 0);
        foreach (var song in Songs)
        foreach (var (_, plan, _, _) in KitSolos(song))
        {
            var starts = song.Song.Notes!.Where(x => crashes.Contains(x.Key)).SelectMany(x => x.Value).Select(x => Math.Round(x.Position, 6)).ToHashSet();
            foreach (var gesture in plan.Where(x => x.Gesture is SoloGesture.Run or SoloGesture.Lick))
            {
                var on = starts.Contains(Math.Round(gesture.Start, 6));
                if (gesture.Intensity >= Rmg.Core.Composition.DrumSolos.ClimaxIntensity)
                    (climax, crashed) = (climax + 1, crashed + (on ? 1 : 0));
                else
                    (calm, calmCrashed) = (calm + 1, calmCrashed + (on ? 1 : 0));
            }
        }

        await Assert.That(climax).IsGreaterThan(10);
        await Assert.That(crashed / (double)climax).IsGreaterThan(0.8);
        await Assert.That(calmCrashed / (double)Math.Max(1, calm)).IsLessThan(0.2);
    }

    [Test]
    public async Task ADrumSolosRuns_GoRoundItsKit_InTheDrummersCells_TheirFirstHitsTheLouder_ItsPausesTheHandsSilent()
    {
        var (solos, first, rest) = (0, new List<double>(), new List<double>());
        foreach (var song in Songs)
        {
            if (KitSolos(song).Length == 0)
                continue;

            var drummer = song.Trace.Where(x => x.Point == TracePoints.Soloist).Select(x => ((TrackRole Role, Soloist Soloist))x.Value!).Single(x => x.Role == TrackRole.Drum).Soloist;
            var drums = song.Song.Notes!.Where(x => x.Key >= DrumGroups.FirstTrackNumber).ToArray();
            foreach (var (span, plan, feet, _) in KitSolos(song))
            {
                solos++;
                var kick = DrumGroups.GetTrackNumber(DrumDefinitions.Kick);
                // all but the last bar, which the fill into the next section plays
                await Assert.That(plan[^1].End).IsLessThanOrEqualTo(span.End - span.Meter.BarDuration + 1e-9);
                foreach (var segment in plan)
                {
                    var hits = drums.SelectMany(x => x.Value.Where(n => n.Position >= segment.Start - 1e-9 && n.Position < segment.End - 1e-9).Select(n => (Track: x.Key, Note: n))).ToArray();
                    // a pause the hands silent, and the feet too unless they keep time
                    if (segment.Gesture == SoloGesture.Pause)
                        await Assert.That(hits.Where(x => !(feet && x.Track == kick))).IsEmpty().Because($"seed {song.Seed} at {segment.Start}");
                    if (segment.Gesture is not (SoloGesture.Run or SoloGesture.Lick))
                        continue;

                    await Assert.That(drummer.Shapes).Contains(segment.Shape);
                    await Assert.That(segment.Sizes.All(drummer.CellSizes.Contains)).IsTrue();
                    // and a climax's gesture its crash on its first hit, a displaced motif's a 16th after the segment's start
                    bool OnFirst(double position) => Math.Abs(position - segment.Start) < 1e-9 || Math.Abs(position - segment.Start - 0.25) < 1e-9;
                    foreach (var (track, _) in hits.Where(x => !(segment.Intensity >= Rmg.Core.Composition.DrumSolos.ClimaxIntensity && OnFirst(x.Note.Position) && x.Track == DrumGroups.GetTrackNumber(DrumDefinitions.Cymbal))))
                        await Assert.That(DrumGroups.GroupOf(DrumGroups.GetDrum(track)) is var group && (group == DrumGroups.Snare || group == DrumGroups.Toms || group == DrumGroups.Kick)).IsTrue()
                            .Because($"seed {song.Seed}, {DrumGroups.GetDrum(track).Name} at {segment.Start}");
                    // a lick's cell on every note's place: the first of every cell the louder
                    if (segment.Gesture == SoloGesture.Lick && !segment.GoesOn)
                    {
                        var size = segment.Sizes[0];
                        foreach (var (hit, i) in hits.Where(x => !(feet && x.Track == kick)).OrderBy(x => x.Note.Position).Select((x, i) => (x, i)))
                            (i % size == 0 ? first : rest).Add(hit.Note.Value.Velocity);
                    }
                }
            }
        }

        await Assert.That(solos).IsGreaterThan(5);
        await Assert.That(first.Average()).IsGreaterThan(rest.Average());
    }

    [Test]
    public async Task ADrumSolosPlayers_SoloAmongTheKitAndThePercussion_TheOthersKeepingTimeSofterAtThePlainEnd_RestingAtTheWild()
    {
        var (percussionSoloists, keptPlain, othersPlain, keptWild, othersWild) = (0, 0, 0, 0, 0);
        var (soloing, keeping) = (new List<double>(), new List<double>());
        foreach (var (groove, songs) in new[] { 0.0, 1.0 }.Select(x => (x, WithPercussion(x))))
        foreach (var song in songs)
        foreach (var (span, plan, _, solo) in DrumSolos(song))
        {
            var others = solo.Tracks.Length - solo.Soloists.Length;
            if (groove == 0)
                (keptPlain, othersPlain) = (keptPlain + solo.Keepers.Length, othersPlain + others);
            else
                (keptWild, othersWild) = (keptWild + solo.Keepers.Length, othersWild + others);
            percussionSoloists += solo.Soloists.Count(x => x > 0);

            // a run's hits a soloist's, a keeper's, the feet's or a climax's crash, the keepers the softer
            var tracksOf = (int[] players) => players.SelectMany(x => solo.Tracks[x]).ToHashSet();
            var (soloTracks, keptTracks) = (tracksOf(solo.Soloists), tracksOf(solo.Keepers));
            foreach (var segment in plan.Where(x => x.Gesture is SoloGesture.Run or SoloGesture.Lick))
            foreach (var (track, notes) in song.Song.Notes!.Where(x => x.Key >= DrumGroups.FirstTrackNumber))
            foreach (var note in notes.Where(n => n.Position >= segment.Start - 1e-9 && n.Position < segment.End - 1e-9))
            {
                await Assert.That(soloTracks.Contains(track) || keptTracks.Contains(track)).IsTrue().Because($"seed {song.Seed}, {DrumGroups.GetDrum(track).Name} at {note.Position}");
                if (keptTracks.Contains(track))
                    keeping.Add(note.Value.Velocity);
                // the hands', but the kick where the feet keep time on it
                else if (!(solo.Feet && DrumGroups.Kick.Drums.Select(DrumGroups.GetTrackNumber).Contains(track)))
                    soloing.Add(note.Value.Velocity);
            }
        }

        await Assert.That(percussionSoloists).IsGreaterThan(10);
        await Assert.That(keptPlain).IsEqualTo(othersPlain);
        await Assert.That(othersPlain).IsGreaterThan(10);
        await Assert.That(keptWild).IsEqualTo(0);
        await Assert.That(keeping.Average()).IsLessThan(soloing.Average());
    }

    /// <summary>The drum solos' gestures, cells and hits a beat, and how far their intensity swings, of songs of wild solos.</summary>
    /// <summary>
    ///     A jazz drum solo's feet on the hi-hat's pedal, on 2 and 4 alone, where the feet keep time (<see cref="Rmg.Core.Composition.DrumSolos.Feet" />),
    ///     but where the solo quotes the groove; and the hands' stickings round the snare and the toms.
    /// </summary>
    [Test]
    public async Task AJazzDrumSolosFeet_PlayTheHiHatsPedalOnTwoAndFour_TheHandsTheSnareAndToms()
    {
        var hat = DrumGroups.GetTrackNumber(DrumDefinitions.HiHat);
        var (solos, pedals) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, Wild with { Genre = Rmg.Tests.Genres.GenrePresets.Genre("jazz") })))
        foreach (var (span, plan, feet, _) in KitSolos(song).Where(x => x.Feet && x.Span.Meter.Sixteenths == 16))
        {
            solos++;
            foreach (var segment in plan.Where(x => x.Gesture != SoloGesture.Quote))
            foreach (var hit in song.Song.Notes!.GetValueOrDefault(hat)?.Where(x => x.Position >= segment.Start - 1e-9 && x.Position < segment.End - 1e-9) ?? [])
            {
                pedals++;
                var beat = (hit.Position - span.Start) % span.Meter.BarDuration;
                await Assert.That(Math.Abs(beat - 1) < 1e-6 || Math.Abs(beat - 3) < 1e-6).IsTrue().Because($"seed {song.Seed} at {hit.Position}");
            }
        }

        Console.WriteLine($"{solos} jazz kit solos with the feet keeping time, {pedals} pedal strokes");
        await Assert.That(solos).IsGreaterThan(2);
        await Assert.That(pedals).IsGreaterThan(10);
    }

    /// <summary>
    ///     A pitched solo trading with the drums, Jazz's: in the drums' turns the drums solo and no pitched part plays, the
    ///     band laid out; in the others the soloists play.
    /// </summary>
    [Test]
    public async Task ASoloTradingWithTheDrums_LeavesTheirTurnsToThem_TheBandLaidOut()
    {
        var (trades, drumTurns, pitchedTurns) = (0, 0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Rmg.Tests.Genres.GenrePresets.Genre("jazz")))))
        foreach (var (index, solo) in song.Solos.Where(x => x.Value.TradesWithDrums))
        {
            trades++;
            var span = song.Map.Sections[index];
            var turn = SoloPlan.TurnOf(span);
            for (var at = span.Start; at < span.End - 1e-9; at += turn)
            {
                var end = Math.Min(span.End, at + turn);
                var pitched = song.Song.Notes!.Where(x => x.Key < DrumGroups.FirstTrackNumber).Sum(x => x.Value.Count(n => n.Position >= at - 1e-9 && n.Position < end - 1e-9));
                if (solo.TurnAt(span, at) == solo.Soloists.IndexOf(TrackRole.Drum))
                {
                    drumTurns++;
                    await Assert.That(pitched).IsEqualTo(0).Because($"seed {song.Seed} at {at}");
                }
                else
                    pitchedTurns += pitched > 0 ? 1 : 0;
            }
        }

        Console.WriteLine($"{trades} solos of 64 Jazz songs trading with the drums, {drumTurns} drums' turns, {pitchedTurns} soloists' turns played");
        await Assert.That(trades).IsGreaterThan(3);
        await Assert.That(pitchedTurns).IsGreaterThan(0);
    }

    /// <summary>A kit's solo's phrase restating the phrase before's motif now and then, as the pitched solos' phrases do.</summary>
    [Test]
    public async Task ADrumSolosPhrases_RestateThePhraseBeforesMotif_NowAndThen()
    {
        var solos = Songs.SelectMany(KitSolos).ToArray();
        var phrases = solos.Sum(x => x.Plan.Select(p => p.Phrase).Distinct().Count() - 1);
        var answered = solos.Sum(x => x.Solo.Answered.Count);
        Console.WriteLine($"of {phrases} kit solos' phrases after their first, {answered} restating the motif before");
        await Assert.That(answered).IsGreaterThan(0);
        await Assert.That(answered).IsLessThan(phrases);
        await Assert.That(solos.All(x => x.Solo.Answered.All(p => p > 0))).IsTrue();
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var solos = Songs.SelectMany(song => DrumSolos(song).Select(x => (Song: song, x.Span, x.Plan, x.Feet, x.Solo))).ToArray();
        var segments = solos.SelectMany(x => x.Plan).ToArray();
        Console.WriteLine($"  {solos.Length} drum solos of {Songs.Length} songs, {segments.Length} segments: " +
                          string.Join(", ", segments.GroupBy(x => x.Gesture).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count() / (double)segments.Length:P0}")));
        var runs = segments.Where(x => x.Gesture is SoloGesture.Run or SoloGesture.Lick).ToArray();
        Console.WriteLine($"  runs and licks: shapes {string.Join(", ", runs.GroupBy(x => x.Shape).Select(x => $"{x.Key} {x.Count() / (double)runs.Length:P0}"))}; " +
                          $"cells {string.Join(", ", runs.SelectMany(x => x.Sizes).GroupBy(x => x).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count()}"))}; speeds {string.Join(", ", runs.GroupBy(x => x.Speed).OrderBy(x => x.Key).Select(x => $"{x.Key} a beat {x.Count()}"))}");
        var hitsABeat = solos.Select(x => x.Song.Song.Notes!.Where(n => n.Key >= DrumGroups.FirstTrackNumber).Sum(n => n.Value.Count(h => h.Position >= x.Span.Start && h.Position < x.Plan[^1].End)) / (x.Plan[^1].End - x.Span.Start)).ToArray();
        Console.WriteLine($"  hits a beat {hitsABeat.Average():F2}, from {hitsABeat.Min():F2} to {hitsABeat.Max():F2}; intensity swings {solos.SelectMany(x => x.Plan.Zip(x.Plan.Skip(1), (a, b) => Math.Abs(b.Intensity - a.Intensity))).Average():F2} a segment");
        // the players of the songs with percussion: how many solo, the percussion among them, how, and the others keeping time
        var played = solos.Where(x => x.Solo.Tracks.Length > 1).ToArray();
        var others = played.Sum(x => x.Solo.Tracks.Length - x.Solo.Soloists.Length);
        Console.WriteLine($"  of {played.Length} solos with players: soloists {string.Join(", ", played.GroupBy(x => x.Solo.Soloists.Length).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count()}"))}; " +
                          $"the percussion in {played.Count(x => x.Solo.Soloists.Any(y => y > 0)) / (double)Math.Max(1, played.Length):P0}, alone in {played.Count(x => !x.Solo.Soloists.Contains(0)) / (double)Math.Max(1, played.Length):P0}; " +
                          $"several {string.Join(", ", played.Where(x => x.Solo.Soloists.Length > 1).GroupBy(x => x.Solo.Mode).Select(x => $"{x.Key} {x.Count()}"))}; the others keeping time {played.Sum(x => x.Solo.Keepers.Length) / (double)Math.Max(1, others):P0}");
        // the kick under the hands' line: its hits a bar and how loud, and the hands' hits a beat beside it
        var kick = DrumGroups.GetTrackNumber(DrumDefinitions.Kick);
        var kicks = solos.Select(x => x.Song.Song.Notes!.GetValueOrDefault(kick)?.Where(h => h.Position >= x.Span.Start && h.Position < x.Plan[^1].End).ToArray() ?? []).ToArray();
        var bars = solos.Select(x => (x.Plan[^1].End - x.Span.Start) / x.Span.Meter.BarDuration).ToArray();
        var handsABeat = solos.Select((x, i) => x.Song.Song.Notes!.Where(n => n.Key >= DrumGroups.FirstTrackNumber && n.Key != kick).Sum(n => n.Value.Count(h => h.Position >= x.Span.Start && h.Position < x.Plan[^1].End)) / (x.Plan[^1].End - x.Span.Start)).ToArray();
        var pausing = solos.Select((x, i) => x.Plan.Any(p => p.Gesture == SoloGesture.Pause && kicks[i].Any(h => h.Position >= p.Start - 1e-9 && h.Position < p.End - 1e-9))).ToArray();
        // by the song's drum setup: how many solos, and the drums playing in them
        foreach (var setup in solos.GroupBy(x => x.Song.Song.Draws!.DrumSetup).OrderBy(x => x.Key))
            Console.WriteLine($"  {setup.Key}: {setup.Count()} solos of {Songs.Count(x => x.Song.Draws!.DrumSetup == setup.Key && x.PlaysDrums)} songs; drums " +
                              string.Join(", ", setup.SelectMany(x => x.Song.Song.Notes!.Where(n => n.Key >= DrumGroups.FirstTrackNumber && n.Value.Any(h => h.Position >= x.Span.Start && h.Position < x.Plan[^1].End)).Select(n => DrumGroups.GetDrum(n.Key).Name))
                                  .GroupBy(x => x).OrderByDescending(x => x.Count()).Take(10).Select(x => $"{x.Key} {x.Count()}")));
        Console.WriteLine($"  the kick {kicks.Select((x, i) => x.Length / bars[i]).Average():F2} hits a bar, velocity {kicks.SelectMany(x => x).Select(x => (double)x.Value.Velocity).DefaultIfEmpty().Average():F2}; " +
                          $"under a pause in {pausing.Count(x => x) / (double)solos.Length:P0} of the solos; the hands {handsABeat.Average():F2} hits a beat");
        // its arc, its runs and licks: the hands' hits a beat and their loudness by the intensity's third, and the intensity by the solo's third
        var arc = solos.SelectMany(x => x.Plan.Where(p => p.Gesture is SoloGesture.Run or SoloGesture.Lick).Select(p =>
        {
            var hits = x.Song.Song.Notes!.Where(n => n.Key >= DrumGroups.FirstTrackNumber && n.Key != kick).SelectMany(n => n.Value).Where(h => h.Position >= p.Start - 1e-9 && h.Position < p.End - 1e-9).ToArray();
            return (p.Intensity, Place: (p.Start - x.Span.Start) / (x.Plan[^1].End - x.Span.Start), ABeat: hits.Length / (p.End - p.Start), Velocity: hits.Select(h => h.Value.Velocity).DefaultIfEmpty().Average());
        })).ToArray();
        Console.WriteLine($"  the arc: by intensity, " + string.Join(", ", arc.GroupBy(x => Math.Min(2, (int)(x.Intensity * 3))).OrderBy(x => x.Key)
            .Select(x => $"third {x.Key + 1} {x.Average(y => y.ABeat):F2} hits a beat at {x.Average(y => y.Velocity):F2}")) + "; by place, intensity " +
            string.Join(", ", arc.GroupBy(x => Math.Min(2, (int)(x.Place * 3))).OrderBy(x => x.Key).Select(x => $"{x.Average(y => y.Intensity):F2}")));
        await Task.CompletedTask;
    }
}
