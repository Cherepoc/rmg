using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.Arrangements;

/// <summary>A drum solo played as a pitched solo is (<see cref="DrumSolos" />), the kit its scale, gesture by gesture by the drummer's vocabulary.</summary>
public sealed class DrumSoloTest
{
    /// <summary>Songs of wild solos, their form, melody and fills, where the drums solo as often as any part.</summary>
    private static SongOverrides Wild => new(Facets: TestCorpus.FacetsOf((Facet.Form, 1), (Facet.Melody, 1), (Facet.Fills, 1)));

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
        // the feet's chance 0.7 (DrumSolos.Feet), against the mechanism breaking (measured: 70% to 75% at offsets 0 and a million)
        Console.WriteLine($"{solos} drum solos, the feet keeping an ostinato in {feet / (double)solos:P0}, the kick stacked in {stacking / (double)Math.Max(1, others):P0} of the others");
        await Assert.That(feet / (double)solos).IsBetween(0.4, 0.95);
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
        await Assert.That(calm).IsGreaterThan(0);
        await Assert.That(calmCrashed / (double)calm).IsLessThan(0.2);
    }

    /// <summary>
    ///     A kit's solo opens on its section's groove, its theme, and a solo that plays to its section's end builds into the
    ///     next, its hits growing louder through its build, but for the beats' own accents, a 0.06 at most.
    /// </summary>
    [Test]
    public async Task AKitsSolo_OpensOnItsGroove_AndBuildsIntoTheNextSection()
    {
        var (kick, crash) = (DrumGroups.GetTrackNumber(DrumDefinitions.Kick), DrumGroups.GetTrackNumber(DrumDefinitions.Cymbal));
        var (solos, built) = (0, 0);
        foreach (var song in Songs)
        foreach (var (span, plan, feet, solo) in KitSolos(song))
        {
            solos++;
            await Assert.That(plan.First(x => x.Gesture != SoloGesture.Pause).Gesture).IsEqualTo(SoloGesture.Quote).Because($"seed {song.Seed} at {span.Start}");
            if (solo.Build is not var (from, to))
                continue;
            built++;
            await Assert.That(to).IsEqualTo(span.End).Because($"seed {song.Seed} at {span.Start}");
            // the hands' drums, the feet's left to them and the crash a landing's
            var feetTracks = feet ? DrumGroups.Kick.Tracks.Concat(DrumGroups.Timekeepers.Tracks).ToHashSet() : [];
            var hits = song.Song.Notes!.Where(x => song.IsDrum(x.Key) && x.Key != crash && !feetTracks.Contains(x.Key)).SelectMany(x => x.Value)
                .Where(x => x.Position >= from - 1e-9 && x.Position < to - 1e-9).OrderBy(x => x.Position).ToArray();
            await Assert.That(hits.Length).IsGreaterThan(2).Because($"seed {song.Seed} at {span.Start}");
            await Assert.That(hits[^1].Value.Velocity).IsGreaterThan(hits[0].Value.Velocity).Because($"seed {song.Seed} at {span.Start}");
            await Assert.That(hits.Zip(hits.Skip(1)).All(x => x.Second.Value.Velocity - x.First.Value.Velocity >= -0.06)).IsTrue().Because($"seed {song.Seed} at {span.Start}");
        }

        Console.WriteLine($"{solos} kit solos, {built} building into the next section");
        await Assert.That(built).IsGreaterThan(solos / 4);
    }

    /// <summary>
    ///     A drum solo over the band's hits: the band strikes them together, every bar or every second, the drums soloing on
    ///     between, so that no pitched part starts a note but on a hit, a moment either way as the band's timing has it, but
    ///     in its last bar, where the bass walks the band back in with the drums as it does out of stop time.
    /// </summary>
    [Test]
    public async Task ADrumSoloOverHits_LeavesTheBandItsHitsAlone()
    {
        var genre = Rmg.Tests.Genres.GenrePresets.With("rock", GenreChoices.Solos, ("sections", 1), ("hits", 1));
        var (solos, struck) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, Wild with { Genre = genre })))
        foreach (var (index, solo) in song.Solos.Where(x => x.Value is { IsDrumSolo: true, HitEvery: > 0 }))
        {
            solos++;
            var span = song.Map.Sections[index];
            var every = span.Meter.BarDuration * solo.HitEvery;
            var notes = song.Song.Notes!.Where(x => !song.IsDrum(x.Key)).SelectMany(x => x.Value).Where(x => x.Position >= span.Start + 0.25 && x.Position < span.LastBar - 1e-9).ToArray();
            struck += notes.Length;
            foreach (var note in notes)
            {
                var off = (note.Position - span.Start) % every;
                await Assert.That(Math.Min(off, every - off)).IsLessThan(0.1).Because($"seed {song.Seed} at {note.Position}");
            }
        }

        Console.WriteLine($"{solos} drum solos over hits, {struck} of the band's notes struck");
        await Assert.That(solos).IsGreaterThan(3);
        await Assert.That(struck).IsGreaterThan(0);
    }

    /// <summary>
    ///     A kit's solo over the band's hits catches every one of them with the kick and a crash, as a drummer does, its hands
    ///     clear of a 16th either side, soloing between them, up to its build.
    /// </summary>
    [Test]
    public async Task AKitsSoloOverHits_CatchesEveryHit_WithTheKickAndACrash()
    {
        var genre = Rmg.Tests.Genres.GenrePresets.With("rock", GenreChoices.Solos, ("sections", 1), ("hits", 1));
        var (kick, cymbal) = (DrumGroups.GetTrackNumber(DrumDefinitions.Kick), DrumGroups.GetTrackNumber(DrumDefinitions.Cymbal));
        var hands = new[] { DrumDefinitions.AcousticSnare, DrumDefinitions.ElectricSnare, DrumDefinitions.Tom }.Select(DrumGroups.GetTrackNumber).ToHashSet();
        var caught = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, Wild with { Genre = genre })))
        foreach (var (span, plan, _, solo) in DrumSolos(song).Where(x => x.Solo.Soloists.Contains(0) && song.Solos[x.Solo.Index].HitEvery > 0))
        {
            var every = span.Meter.BarDuration * song.Solos[solo.Index].HitEvery;
            bool StrikesAt(int track, double at) => song.Song.Notes!.GetValueOrDefault(track)?.Any(x => Math.Abs(x.Position - at) < 1e-6) ?? false;
            for (var at = span.Start; at < plan.Max(x => x.End) - 1e-9; at += every)
            {
                caught++;
                var because = $"seed {song.Seed}, the hit at {at}";
                await Assert.That(StrikesAt(kick, at) && StrikesAt(cymbal, at)).IsTrue().Because(because);
                await Assert.That(song.Song.Notes!.Where(x => hands.Contains(x.Key)).SelectMany(x => x.Value).Any(x => Math.Abs(x.Position - at) < 0.25 - 1e-6)).IsFalse().Because(because);
            }
        }

        Console.WriteLine($"{caught} hits caught");
        await Assert.That(caught).IsGreaterThan(10);
    }

    /// <summary>
    ///     A kit's held gesture but a release a roll on one drum, its strokes growing or fading steadily through it, as a
    ///     drummer swells into the next gesture or eases off, but for the beats' own accents on it, a 0.06 at most.
    /// </summary>
    [Test]
    public async Task AKitsHeldGesture_IsARoll_SwellingOrFadingThroughIt()
    {
        var (kick, crash) = (DrumGroups.GetTrackNumber(DrumDefinitions.Kick), DrumGroups.GetTrackNumber(DrumDefinitions.Cymbal));
        var rolls = 0;
        foreach (var song in Songs)
        foreach (var (span, plan, _, _) in KitSolos(song))
        foreach (var held in plan.Where(x => x.Gesture == SoloGesture.Held && !x.IsRelease && x.End - x.Start >= 1 - 1e-9))
        {
            var strokes = song.Song.Notes!.Where(x => song.IsDrum(x.Key) && x.Key != kick && x.Key != crash)
                .SelectMany(x => x.Value.Select(n => (Track: x.Key, n.Position, Velocity: n.Value.Velocity)))
                .Where(x => x.Position >= held.Start - 1e-9 && x.Position < held.End - 1e-9).OrderBy(x => x.Position).ToArray();
            // but a keeper's and the feet's under it, which play their own
            var rolled = strokes.GroupBy(x => x.Track).MaxBy(x => x.Count())!.ToArray();
            rolls++;
            await Assert.That(rolled.Length).IsGreaterThanOrEqualTo(4).Because($"seed {song.Seed} at {held.Start}");
            var way = Math.Sign(rolled[^1].Velocity - rolled[0].Velocity);
            await Assert.That(way).IsNotEqualTo(0).Because($"seed {song.Seed} at {held.Start}");
            await Assert.That(rolled.Zip(rolled.Skip(1)).All(x => (x.Second.Velocity - x.First.Velocity) * way >= -0.06)).IsTrue().Because($"seed {song.Seed} at {held.Start}");
        }

        await Assert.That(rolls).IsGreaterThan(20);
    }

    [Test]
    public async Task ADrumSolosRuns_GoRoundItsKit_InTheDrummersCells_TheirFirstHitsTheLouder_ItsPausesTheHandsSilent()
    {
        var (solos, colours, first, rest) = (0, 0, new List<double>(), new List<double>());
        foreach (var song in Songs)
        {
            if (KitSolos(song).Length == 0)
                continue;

            var drummer = song.Soloists[TrackRole.Drum];
            var drums = song.Song.Notes!.Where(x => song.IsDrum(x.Key)).ToArray();
            foreach (var (span, plan, feet, solo) in KitSolos(song))
            {
                solos++;
                var kick = DrumGroups.GetTrackNumber(DrumDefinitions.Kick);
                // the band's hits under it, which it catches with the kick and a crash (AKitsSoloOverHits_CatchesEveryHit_WithTheKickAndACrash)
                var every = song.Solos[solo.Index].HitEvery * span.Meter.BarDuration;
                bool OnABandHit(double position) => every > 0 && ((position - span.Start) % every + every) % every is var off && Math.Min(off, every - off) < 1e-6;
                // all but the last bar, which the fill into the next section plays, or its build does
                await Assert.That(plan[^1].End).IsLessThanOrEqualTo(span.LastBar + 1e-9);
                foreach (var segment in plan)
                {
                    var hits = drums.SelectMany(x => x.Value.Where(n => n.Position >= segment.Start - 1e-9 && n.Position < segment.End - 1e-9 && !OnABandHit(n.Position)).Select(n => (Track: x.Key, Note: n))).ToArray();
                    // a pause the hands silent, and the feet too unless they keep time
                    if (segment.Gesture == SoloGesture.Pause)
                        await Assert.That(hits.Where(x => !(feet && x.Track == kick))).IsEmpty().Because($"seed {song.Seed} at {segment.Start}");
                    if (segment.Gesture is not (SoloGesture.Run or SoloGesture.Lick))
                        continue;

                    await Assert.That(drummer.Shapes).Contains(segment.Shape);
                    await Assert.That(segment.Sizes.All(drummer.CellSizes.Contains)).IsTrue();
                    // and a climax's gesture its crash on its first hit, a displaced motif's a 16th after the segment's start, and a
                    // loud accent's cymbal of colour, the china's, the splash's, the open hi-hat's or the ride's bell
                    bool OnFirst(double position) => Math.Abs(position - segment.Start) < 1e-9 || Math.Abs(position - segment.Start - 0.25) < 1e-9;
                    bool Hands(int track) => DrumGroups.GroupOf(DrumGroups.GetDrum(track)) is var group && (group == DrumGroups.Snare || group == DrumGroups.Toms || group == DrumGroups.Kick);
                    double Heard(TimelineItem<RealizedNote> note) => Rmg.Core.Rendering.Render.ToMidiVelocity(note.Value.Velocity);
                    foreach (var (track, note) in hits.Where(x => !(segment.Intensity >= Rmg.Core.Composition.DrumSolos.ClimaxIntensity && OnFirst(x.Note.Position) && x.Track == DrumGroups.GetTrackNumber(DrumDefinitions.Cymbal))))
                    {
                        // a cymbal of colour struck with a loud accent of the hands, at its loudness
                        if (note.Value.Pitches[0] is 52 or 55 or 46 or 53)
                        {
                            colours++;
                            await Assert.That(Heard(note) >= Rmg.Core.Composition.DrumSolos.StabLoudness - 1e-6
                                              && hits.Any(x => Hands(x.Track) && Math.Abs(x.Note.Position - note.Position) < 1e-9)).IsTrue().Because($"seed {song.Seed}, a colour at {note.Position}");
                            continue;
                        }

                        await Assert.That(Hands(track)).IsTrue().Because($"seed {song.Seed}, {DrumGroups.GetDrum(track).Name} at {segment.Start}");
                        // a tap on the snare's rim a quiet one
                        if (note.Value.Pitches[0] == 37)
                            await Assert.That(Heard(note)).IsLessThan(Rmg.Core.Composition.DrumSolos.CrossStickBelow + 1e-6).Because($"seed {song.Seed}, a rim at {note.Position}");
                    }
                    // a lick's cell on every note's place: the first of every cell the louder
                    if (segment.Gesture == SoloGesture.Lick && !segment.GoesOn)
                    {
                        // counted by onset, a kick stacked under another drum's accent one with it, each its loudest; its cells
                        // of its sizes in turn, a grouping drummer's of several
                        var starts = new HashSet<int>();
                        for (var (at, cell) = (0, 0); at < 1000; at += segment.Sizes[cell++ % segment.Sizes.Length])
                            starts.Add(at);
                        foreach (var (onset, i) in hits.Where(x => !(feet && x.Track == kick)).GroupBy(x => Math.Round(x.Note.Position, 6)).OrderBy(x => x.Key)
                                     .Select((x, i) => (x.Max(y => y.Note.Value.Velocity), i)))
                            (starts.Contains(i) ? first : rest).Add(onset);
                    }
                }
            }
        }

        await Assert.That(solos).IsGreaterThan(5);
        await Assert.That(first.Average()).IsGreaterThan(rest.Average());
        Console.WriteLine($"{solos} drum solos, {colours} cymbals of colour");
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
            {
                var (kept, soloed) = (new List<double>(), new List<double>());
                foreach (var (track, notes) in song.Song.Notes!.Where(x => song.IsDrum(x.Key)))
                foreach (var note in notes.Where(n => n.Position >= segment.Start - 1e-9 && n.Position < segment.End - 1e-9))
                {
                    await Assert.That(soloTracks.Contains(track) || keptTracks.Contains(track)).IsTrue().Because($"seed {song.Seed}, {DrumGroups.GetDrum(track).Name} at {note.Position}");
                    if (keptTracks.Contains(track))
                        kept.Add(note.Value.Velocity);
                    // the hands', but the kick where the feet keep time on it
                    else if (!(solo.Feet && DrumGroups.Kick.Drums.Select(DrumGroups.GetTrackNumber).Contains(track)))
                        soloed.Add(note.Value.Velocity);
                }

                // the time's strongest beat under the gesture's accents, as heard, however loud the section
                if (kept.Count > 0 && soloed.Count > 0)
                    await Assert.That(kept.Max()).IsLessThan(soloed.Max()).Because($"seed {song.Seed}, the gesture at {segment.Start}");
                keeping.AddRange(kept);
                soloing.AddRange(soloed);
            }
        }

        await Assert.That(percussionSoloists).IsGreaterThan(10);
        await Assert.That(keptPlain).IsEqualTo(othersPlain);
        await Assert.That(othersPlain).IsGreaterThan(10);
        await Assert.That(keptWild).IsEqualTo(0);
        await Assert.That(keeping.Average()).IsLessThan(soloing.Average());
    }

    /// <summary>
    ///     A jazz drum solo's feet on the hi-hat's pedal, on 2 and 4 alone, where the feet keep time (<see cref="Rmg.Core.Composition.DrumSolos.Feet" />),
    ///     but where the solo quotes the groove; and the hands' stickings round the snare and the toms.
    /// </summary>
    [Test]
    public async Task AJazzDrumSolosFeet_PlayTheHiHatsPedalOnTwoAndFour_TheHandsTheSnareAndToms()
    {
        var (hat, tom) = (DrumGroups.GetTrackNumber(DrumDefinitions.HiHat), DrumGroups.GetTrackNumber(DrumDefinitions.Tom));
        var (solos, pedals, roundTheKit) = (0, 0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, Wild with { Genre = Rmg.Tests.Genres.GenrePresets.Genre("jazz") })))
        foreach (var (span, plan, feet, _) in KitSolos(song).Where(x => x.Feet && x.Span.Meter.Sixteenths == 16))
        {
            solos++;
            // the hands round the toms, two of them at least, where the song's kit has toms
            var struck = plan.Where(x => x.Gesture is SoloGesture.Run or SoloGesture.Lick)
                .SelectMany(x => song.Song.Notes!.GetValueOrDefault(tom)?.Where(n => n.IsWithin(x.Start, x.End)) ?? []).SelectMany(x => x.Value.Pitches).Distinct().Count();
            roundTheKit += struck >= 2 ? 1 : 0;
            foreach (var segment in plan.Where(x => x.Gesture != SoloGesture.Quote))
            foreach (var hit in song.Song.Notes!.GetValueOrDefault(hat)?.Where(x => x.Position >= segment.Start - 1e-9 && x.Position < segment.End - 1e-9) ?? [])
            {
                pedals++;
                var beat = (hit.Position - span.Start) % span.Meter.BarDuration;
                await Assert.That(Math.Abs(beat - 1) < 1e-6 || Math.Abs(beat - 3) < 1e-6).IsTrue().Because($"seed {song.Seed} at {hit.Position}");
            }
        }

        Console.WriteLine($"{solos} jazz kit solos with the feet keeping time, {pedals} pedal strokes, {roundTheKit} round two toms or more");
        await Assert.That(solos).IsGreaterThan(2);
        await Assert.That(pedals).IsGreaterThan(10);
        await Assert.That(roundTheKit).IsGreaterThan(solos / 2);
    }

    /// <summary>A cascade, Metal's: both hands down the toms from the highest and the kick, its runs and licks never on the snare.</summary>
    [Test]
    public async Task ACascade_GoesDownTheToms_AndTheKick_NeverTheSnare()
    {
        var genre = new Genre(ImmutableDictionary<string, ImmutableDictionary<string, Rmg.Core.Probabilities.ByConvention>>.Empty.Add(GenreChoices.Stickings.Name,
            ImmutableDictionary<string, Rmg.Core.Probabilities.ByConvention>.Empty.Add(GenreChoices.Name(nameof(Sticking.Cascade)), new(1, 1, 1))));
        var tom = DrumGroups.GetTrackNumber(DrumDefinitions.Tom);
        var (gestures, down) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, Wild with { Genre = genre })))
        foreach (var (_, plan, _, _) in KitSolos(song).Where(x => !x.Feet && song.Song.Notes!.GetValueOrDefault(tom)?.Count > 0))
        foreach (var segment in plan.Where(x => x.Gesture is SoloGesture.Run or SoloGesture.Lick))
        {
            gestures++;
            // the snare the kit has, the acoustic or the electric
            await Assert.That(DrumGroups.SnareHeads.Sum(snare => song.Song.Notes!.GetValueOrDefault(snare)?.Count(x => x.IsWithin(segment.Start, segment.End)) ?? 0)).IsEqualTo(0).Because($"seed {song.Seed} at {segment.Start}");
            var toms = song.Song.Notes![tom].Where(x => x.IsWithin(segment.Start, segment.End)).Select(x => x.Value.Pitches[0]).ToArray();
            down += toms.Length < 2 || toms[1] <= toms[0] ? 1 : 0;
        }

        Console.WriteLine($"{gestures} cascading runs and licks, {down} going down from their first tom");
        await Assert.That(gestures).IsGreaterThan(5);
        await Assert.That(down).IsEqualTo(gestures);
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
                var pitched = song.Song.Notes!.Where(x => !song.IsDrum(x.Key)).Sum(x => x.Value.Count(n => n.Position >= at - 1e-9 && n.Position < end - 1e-9));
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
        await Assert.That(drumTurns).IsGreaterThan(3);
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

    /// <summary>How often the songs of a genre, as they are drawn, play a drum solo or trade with the drums, and how long each is, in bars.</summary>
    [Test]
    [Explicit]
    public async Task HowOftenReport()
    {
        foreach (var genre in new string?[] { null, "rock", "pop", "punk", "metal", "thrash", "funk", "blues", "jazz", "latin", "dnb", "surf" })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, genre is null ? new SongOverrides() : new SongOverrides(Genre: Rmg.Tests.Genres.GenrePresets.Genre(genre))));
            var full = songs.SelectMany(x => x.Solos.Where(s => s.Value.IsDrumSolo).Select(s => x.Map.Sections[s.Key].BarCount)).ToArray();
            var trades = songs.SelectMany(x => x.Solos.Where(s => s.Value.TradesWithDrums).Select(s => x.Map.Sections[s.Key].BarCount)).ToArray();
            string Bars(int[] bars) => bars.Length == 0 ? "none" : $"{bars.Average():F1} bars ({bars.Min()}-{bars.Max()})";
            var overHits = songs.Sum(x => x.Solos.Values.Count(s => s is { IsDrumSolo: true, HitEvery: > 0 }));
            Console.WriteLine($"  {genre ?? "the songs' own"}: a drum solo in {songs.Count(x => x.Solos.Values.Any(s => s.IsDrumSolo)) / 64.0:P0} of songs, {Bars(full)}, over the band's hits {overHits} of {full.Length}; " +
                              $"trading with the drums in {songs.Count(x => x.Solos.Values.Any(s => s.TradesWithDrums)) / 64.0:P0}, {Bars(trades)}");
        }

        await Task.CompletedTask;
    }

    /// <summary>The drum solos' gestures, cells and hits a beat, and how far their intensity swings, of songs of wild solos.</summary>
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
        var hitsABeat = solos.Select(x => x.Song.Song.Notes!.Where(n => x.Song.IsDrum(n.Key)).Sum(n => n.Value.Count(h => h.Position >= x.Span.Start && h.Position < x.Plan[^1].End)) / (x.Plan[^1].End - x.Span.Start)).ToArray();
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
        var handsABeat = solos.Select((x, i) => x.Song.Song.Notes!.Where(n => x.Song.IsDrum(n.Key) && n.Key != kick).Sum(n => n.Value.Count(h => h.Position >= x.Span.Start && h.Position < x.Plan[^1].End)) / (x.Plan[^1].End - x.Span.Start)).ToArray();
        var pausing = solos.Select((x, i) => x.Plan.Any(p => p.Gesture == SoloGesture.Pause && kicks[i].Any(h => h.Position >= p.Start - 1e-9 && h.Position < p.End - 1e-9))).ToArray();
        // by the song's drum setup: how many solos, and the drums playing in them
        foreach (var setup in solos.GroupBy(x => x.Song.Song.Draws!.DrumSetup).OrderBy(x => x.Key))
            Console.WriteLine($"  {setup.Key}: {setup.Count()} solos of {Songs.Count(x => x.Song.Draws!.DrumSetup == setup.Key && x.PlaysDrums)} songs; drums " +
                              string.Join(", ", setup.SelectMany(x => x.Song.Song.Notes!.Where(n => x.Song.IsDrum(n.Key) && n.Value.Any(h => h.Position >= x.Span.Start && h.Position < x.Plan[^1].End)).Select(n => DrumGroups.GetDrum(n.Key).Name))
                                  .GroupBy(x => x).OrderByDescending(x => x.Count()).Take(10).Select(x => $"{x.Key} {x.Count()}")));
        Console.WriteLine($"  the kick {kicks.Select((x, i) => x.Length / bars[i]).Average():F2} hits a bar, velocity {kicks.SelectMany(x => x).Select(x => Rmg.Core.Rendering.Render.ToMidiVelocity(x.Value.Velocity)).DefaultIfEmpty().Average():F2}; " +
                          $"under a pause in {pausing.Count(x => x) / (double)solos.Length:P0} of the solos; the hands {handsABeat.Average():F2} hits a beat");
        // its arc, its runs and licks: the hands' hits a beat and their loudness by the intensity's third, and the intensity by the solo's third
        var arc = solos.SelectMany(x => x.Plan.Where(p => p.Gesture is SoloGesture.Run or SoloGesture.Lick).Select(p =>
        {
            var hits = x.Song.Song.Notes!.Where(n => x.Song.IsDrum(n.Key) && n.Key != kick).SelectMany(n => n.Value).Where(h => h.Position >= p.Start - 1e-9 && h.Position < p.End - 1e-9).ToArray();
            return (p.Intensity, Place: (p.Start - x.Span.Start) / (x.Plan[^1].End - x.Span.Start), ABeat: hits.Length / (p.End - p.Start), Velocity: hits.Select(h => Rmg.Core.Rendering.Render.ToMidiVelocity(h.Value.Velocity)).DefaultIfEmpty().Average());
        })).ToArray();
        Console.WriteLine($"  the arc: by intensity, " + string.Join(", ", arc.GroupBy(x => Math.Min(2, (int)(x.Intensity * 3))).OrderBy(x => x.Key)
            .Select(x => $"third {x.Key + 1} {x.Average(y => y.ABeat):F2} hits a beat at {x.Average(y => y.Velocity):F2}")) + "; by place, intensity " +
            string.Join(", ", arc.GroupBy(x => Math.Min(2, (int)(x.Place * 3))).OrderBy(x => x.Key).Select(x => $"{x.Average(y => y.Intensity):F2}")));
        // its loudness: the hands' hits by the solo's third, how widely they spread, a run's or a lick's loudest over its
        // middle, and the crashes a solo
        var crash = DrumGroups.GetTrackNumber(DrumDefinitions.Cymbal);
        var hands = solos.SelectMany(x => x.Song.Song.Notes!.Where(n => x.Song.IsDrum(n.Key) && n.Key != kick && n.Key != crash).SelectMany(n => n.Value)
            .Where(h => h.Position >= x.Span.Start && h.Position < x.Plan[^1].End)
            .Select(h => (Place: (h.Position - x.Span.Start) / (x.Plan[^1].End - x.Span.Start), Velocity: Rmg.Core.Rendering.Render.ToMidiVelocity(h.Value.Velocity)))).ToArray();
        var mean = hands.Average(x => x.Velocity);
        var gaps = solos.SelectMany(x => x.Plan.Where(p => p.Gesture is SoloGesture.Run or SoloGesture.Lick).Select(p => x.Song.Song.Notes!.Where(n => x.Song.IsDrum(n.Key) && n.Key != kick && n.Key != crash)
            .SelectMany(n => n.Value).Where(h => h.Position >= p.Start - 1e-9 && h.Position < p.End - 1e-9).Select(h => Rmg.Core.Rendering.Render.ToMidiVelocity(h.Value.Velocity)).Order().ToArray())
            .Where(v => v.Length >= 4).Select(v => v[^1] - v[v.Length / 2])).ToArray();
        var crashes = solos.Select(x => x.Song.Song.Notes!.GetValueOrDefault(crash)?.Count(h => h.Position >= x.Span.Start && h.Position < x.Plan[^1].End) ?? 0).ToArray();
        Console.WriteLine($"  the theme: solos opening on the groove {solos.Count(x => x.Plan.FirstOrDefault(p => p.Gesture != SoloGesture.Pause)?.Gesture == SoloGesture.Quote) / (double)solos.Length:P0}, " +
                          $"quotes a solo {solos.Average(x => x.Plan.Count(p => p.Gesture == SoloGesture.Quote)):F1}; building into the next section {solos.Count(x => x.Solo.Build is not null) / (double)solos.Length:P0}");
        // its colours: the cymbals of colour and the snare's rim struck in it, by their notes
        int Struck(params int[] codes) => solos.Sum(x => x.Song.Song.Notes!.Where(n => x.Song.IsDrum(n.Key)).SelectMany(n => n.Value)
            .Count(h => h.Position >= x.Span.Start && h.Position < x.Plan[^1].End && codes.Contains(h.Value.Pitches[0])));
        Console.WriteLine($"  colours a solo: the china {Struck(52) / (double)solos.Length:F1}, the splash {Struck(55) / (double)solos.Length:F1}, the open hi-hat {Struck(46) / (double)solos.Length:F1}, " +
                          $"the ride's bell {Struck(53) / (double)solos.Length:F1}, the snare's rim {Struck(37) / (double)solos.Length:F1}");
        Console.WriteLine($"  loudness: the hands by the solo's third {string.Join(", ", hands.GroupBy(x => Math.Min(2, (int)(x.Place * 3))).OrderBy(x => x.Key).Select(x => $"{x.Average(y => y.Velocity):F2}"))}, " +
                          $"spread {Math.Sqrt(hands.Average(x => (x.Velocity - mean) * (x.Velocity - mean))):F2}; a run's loudest over its middle {gaps.DefaultIfEmpty().Average():F2}; crashes {crashes.Average():F1} a solo, {crashes.Sum() / bars.Sum():F2} a bar");
        await Task.CompletedTask;
    }
}
