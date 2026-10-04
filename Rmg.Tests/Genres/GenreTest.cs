using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

public sealed class GenreTest
{
    private static Genre Of(params (GenreChoice Choice, (string Option, double Amount)[] Options)[] choices) =>
        new(choices.ToImmutableDictionary(x => x.Choice.Name, x => x.Options.ToImmutableDictionary(y => y.Option, y => y.Amount)));

    private static readonly (string, ByConvention)[] Options = [("plain", new ByConvention(1, 0.9, 0)), ("odd", new ByConvention(0, 0.1, 1))];

    private static double Share(IEnumerable<Weighted<string>> weights, string option)
    {
        var all = weights.ToArray();
        return all.Where(x => x.Value == option).Sum(x => x.Weight) / all.Sum(x => x.Weight);
    }

    [Test]
    public async Task NoPool_WeighsTheOptionsAsTheyAre()
    {
        foreach (var conventionality in new[] { 0, 0.3, 0.5, 0.8, 1 })
            await Assert.That(ByConvention.Weigh(Options, conventionality, null)).IsEquivalentTo(ByConvention.Weigh(Options, conventionality));
    }

    [Test]
    public async Task AnOptionOutOfThePool_NeverPlays_AndOneAlone_AlwaysDoes_HoweverPlainOrWild()
    {
        foreach (var conventionality in new[] { 0, 0.5, 1 })
        {
            var plainOnly = ByConvention.Weigh(Options, conventionality, x => x == "plain" ? 1 : null);
            await Assert.That(plainOnly.Select(x => x.Value)).IsEquivalentTo(["plain"]);
            var oddOnly = ByConvention.Weigh(Options, conventionality, x => x == "odd" ? 1 : null);
            await Assert.That(oddOnly.Select(x => x.Value)).IsEquivalentTo(["odd"]);
        }
    }

    [Test]
    public async Task APoolsWeights_AreTheMiddles_TheEndsStillChoosing_AndTheWildGivingBackWhatThePoolTook()
    {
        // the odd option a fifth as likely as the plain one in the pool's middle, the plain end's none, and the wild end's
        // but for what the pool took from the plain one's middle, 0.9 as the songs have it, 1/1.2 in the pool
        Func<string, double?> pool = x => x == "odd" ? 0.2 : 1;
        await Assert.That(Share(ByConvention.Weigh(Options, 0.5, pool), "odd")).IsEqualTo(0.2 / 1.2).Within(1e-9);
        await Assert.That(Share(ByConvention.Weigh(Options, 0, pool), "odd")).IsEqualTo(0).Within(1e-9);
        await Assert.That(Share(ByConvention.Weigh(Options, 1, pool), "odd")).IsEqualTo(1 - (0.9 - 1 / 1.2)).Within(1e-9);
    }

    [Test]
    public async Task APoolAsTheSongsHaveIt_WeighsTheOptionsAsTheyAre_AndOneAt0_PlaysOnlyTheWilder()
    {
        foreach (var conventionality in new[] { 0, 0.25, 0.5, 0.75, 1 })
        {
            var asTheyAre = ByConvention.Weigh(Options, conventionality, x => x == "odd" ? 0.1 : 0.9);
            await Assert.That(Share(asTheyAre, "odd")).IsEqualTo(Share(ByConvention.Weigh(Options, conventionality), "odd")).Within(1e-9);
        }

        // the odd option at 0: none of it up to the middle, then the more the wilder, all the songs' 0.1 given back
        Func<string, double?> wildOnly = x => x == "odd" ? 0 : 1;
        await Assert.That(Share(ByConvention.Weigh(Options, 0.5, wildOnly), "odd")).IsEqualTo(0);
        await Assert.That(Share(ByConvention.Weigh(Options, 0.75, wildOnly), "odd")).IsGreaterThan(0);
        await Assert.That(Share(ByConvention.Weigh(Options, 1, wildOnly), "odd")).IsEqualTo(0.1).Within(1e-9);
    }

    [Test]
    public async Task AGenreOf4By4And3By4_Plays4By4AtThePlainEnd_ItsWeightsInTheMiddle_And3By4AtTheWild()
    {
        // 4/4 the plain end's meter, 3/4 one the wild end allows and 4/4 not, each end keeping to what it allows of the pool
        var genre = Of((GenreChoices.Meter, [("4/4", 1), ("3/4", 0.5)]));
        var pool = genre.Pool<Meter>(GenreChoices.Meter, Meter.NameOf);
        double ThreeFour(double conventionality)
        {
            var weights = ByConvention.Weigh(Meter.Options, conventionality, pool);
            return weights.Where(x => x.Value == Meter.ThreeFour).Sum(x => x.Weight) / weights.Sum(x => x.Weight);
        }

        await Assert.That(ThreeFour(0)).IsEqualTo(0);
        await Assert.That(ThreeFour(0.5)).IsEqualTo(0.5 / 1.5).Within(1e-9);
        // and the wild end 3/4's, but for what the pool took from 4/4's middle as the songs have it among the two
        await Assert.That(ThreeFour(1)).IsEqualTo(1 - (0.92 / 0.96 - 1 / 1.5)).Within(1e-9);
    }

    // a chance's pool: the yes at its chance in the middle, or never or always
    private static Func<bool, double?> InTheMiddle(double chance) => yes => yes ? chance : 1 - chance;
    private static readonly Func<bool, double?> Never = yes => yes ? null : 1;
    private static readonly Func<bool, double?> Always = yes => yes ? 1 : null;

    [Test]
    public async Task AChance_IsItsOwnAsTheSongsHaveIt_NeverOutOfThePool_AlwaysAlone_AndItsMiddleTheGenres()
    {
        foreach (var chance in new[] { new ByConvention(0, 0.2, 1), new ByConvention(0.8, 0.8, 0.8), new ByConvention(1, 1, 1) })
        foreach (var conventionality in new[] { 0, 0.25, 0.5, 0.75, 1 })
        {
            await Assert.That(chance.At(conventionality, InTheMiddle(chance.Tuned))).IsEqualTo(chance.At(conventionality)).Within(1e-9);
            await Assert.That(chance.At(conventionality, Never)).IsEqualTo(0);
            await Assert.That(chance.At(conventionality, Always)).IsEqualTo(1);
            await Assert.That(chance.At(conventionality, null)).IsEqualTo(chance.At(conventionality));
            await Assert.That(chance.At(0.5, InTheMiddle(0.3))).IsEqualTo(0.3).Within(1e-9);
        }
    }

    [Test]
    public async Task AChanceAt0_NeverPlaysUpToTheMiddle_ThenTheWilderTheLikelier()
    {
        // a pad, as likely in every song as the songs have it, at 0 in a genre: its 0.8 given back at the wild end
        var pad = new ByConvention(0.8, 0.8, 0.8);
        await Assert.That(pad.At(0, InTheMiddle(0))).IsEqualTo(0);
        await Assert.That(pad.At(0.5, InTheMiddle(0))).IsEqualTo(0);
        await Assert.That(pad.At(0.75, InTheMiddle(0))).IsGreaterThan(0);
        await Assert.That(pad.At(1, InTheMiddle(0))).IsEqualTo(0.8).Within(1e-9);
        // a harmony never in the plainest songs stays out of them whatever the genre's middle
        await Assert.That(new ByConvention(0, 0.15, 0.15).At(0, InTheMiddle(0.9))).IsEqualTo(0);
    }

    [Test]
    public async Task AGenre_IsReadFromItsChoicesNames_AndWhatIsNotAGenre_IsNot()
    {
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> Choices(string choice, string option, double amount) =>
            new Dictionary<string, IReadOnlyDictionary<string, double>> { [choice] = new Dictionary<string, double> { [option] = amount } };

        var pool = Genre.Read(Choices("meter", "3/4", 1), out _)!.Pool<string>(GenreChoices.Meter, x => x)!;
        await Assert.That(pool("3/4")).IsEqualTo(1);
        await Assert.That(pool("4/4")).IsNull();
        await Assert.That(Genre.Read(Choices("meter", "3/4", 0), out _)!.Pool<string>(GenreChoices.Meter, x => x)!("3/4")).IsEqualTo(0);
        await Assert.That(Genre.None.Pool<string>(GenreChoices.Meter, x => x)).IsNull();
        foreach (var (choice, option, amount) in new[] { ("kazoo", "3/4", 1.0), ("meter", "1/1", 1.0), ("meter", "3/4", 1.5) })
        {
            await Assert.That(Genre.Read(Choices(choice, option, amount), out var error)).IsNull();
            await Assert.That(error).IsNotNull();
        }
    }

    [Test]
    public async Task EveryChoice_AndEveryOptionOfOne_HasANameOfItsOwn()
    {
        await Assert.That(GenreChoices.All.Select(x => x.Name).Distinct().Count()).IsEqualTo(GenreChoices.All.Length);
        foreach (var choice in GenreChoices.All)
        {
            await Assert.That(choice.Options.Select(x => x.Name).Distinct().Count()).IsEqualTo(choice.Options.Length).Because(choice.Name);
            await Assert.That(choice.Options.All(x => x.Name.Length > 0 && !x.Name.Contains(' '))).IsTrue().Because(choice.Name);
            // every default as the songs have it: a pool's heaviest 1, a range's the songs', a value as it is
            if (choice.Kind == GenreChoiceKind.Pool)
                await Assert.That(choice.Options.Max(x => x.Default)).IsEqualTo(1).Because(choice.Name);
            else if (choice.Kind == GenreChoiceKind.Range)
                await Assert.That(choice["from"].Default).IsLessThanOrEqualTo(choice["to"].Default).Because(choice.Name);
            else if (choice.Kind == GenreChoiceKind.Values)
                await Assert.That(choice.Options.All(x => x.Default == 0.5)).IsTrue().Because(choice.Name);
            await Assert.That(choice.Options.All(x => x.Default is >= 0 and <= 1)).IsTrue().Because(choice.Name);
        }
    }

    [Test]
    public async Task AGenresPools_HoldHoweverWildTheSong()
    {
        var genre = Of(
            (GenreChoices.Meter, [("3/4", 1)]),
            (GenreChoices.Scale, [("dorian", 1), ("mixolydian", 0.5)]),
            (GenreChoices.Instruments[TrackRole.Bass], [("acousticBass", 1)]),
            (GenreChoices.Instruments[TrackRole.Melody], [("flute", 1), ("clarinet", 1)]),
            (GenreChoices.Tempo, [("from", GenreChoices.AmountOfTempo(70 / Meter.BaseTempo)), ("to", GenreChoices.AmountOfTempo(85 / Meter.BaseTempo))]),
            (GenreChoices.DrumPieces, [..DrumPieces.All.Where(x => x.Name != "cowbell").Select(x => (x.Name, 1.0))])
        );
        var melody = new[] { 73, 71 };
        foreach (var seed in Enumerable.Range(0, 16))
        foreach (var @base in new[] { 0.0, 0.5, 1.0 })
        {
            var song = TestCorpus.Get(seed, new SongOverrides(Base: @base, Genre: genre));

            await Assert.That(Meter.NameOf(song.Map.Meter)).IsEqualTo("3/4");
            await Assert.That(song.Song.Draws!.Scale.Name).IsIn(["Dorian", "Mixolydian"]);
            // a tempo the songs never draw, below 90 beats a minute, a whole number of them
            var bpm = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(0).GetStateValue(Rmg.Core.Events.StateKinds.Tempo) * Meter.BaseTempo;
            await Assert.That(bpm).IsBetween(70, 85);
            await Assert.That(bpm).IsEqualTo(Math.Round(bpm)).Within(1e-9);
            var pitched = song.Song.TrackDefinitions.Values.OfType<PitchInstrumentTrack>().ToDictionary(x => x.Role, x => x.InstrumentCode);
            await Assert.That(pitched[TrackRole.Bass]).IsEqualTo(32);
            await Assert.That(pitched[TrackRole.Melody]).IsIn(melody);
            // and every switch over the song keeps to them
            foreach (var (_, role, program, _, _) in song.Trace.Where(x => x.Point == TracePoints.InstrumentOverTime)
                         .Select(x => ((int, TrackRole Role, int Program, ArticulationMode, int))x.Value!))
            {
                if (role == TrackRole.Bass)
                    await Assert.That(program).IsEqualTo(32);
                if (role == TrackRole.Melody)
                    await Assert.That(program).IsIn(melody);
            }

            await Assert.That(song.Song.Draws.DrumPieces).DoesNotContain("cowbell");
        }
    }

    [Test]
    public async Task ASongsSections_BarsAndFills_KeepToTheGenresPools_TheWildestToo()
    {
        var genre = Of(
            (GenreChoices.Meter, [("4/4", 1), ("3/4", 0.5)]),
            (GenreChoices.Scale, [("dorian", 1), ("major", 1)]),
            (GenreChoices.Feel, [("straight", 1), ("tuplets3", 0.5)])
        );
        var feels = Feels.Weights.Select(x => x.Feel).Where(x => Feels.NameOf(x) is "straight" or "tuplets3").ToHashSet();
        var changed = 0;
        foreach (var seed in Enumerable.Range(0, 8))
        {
            var song = TestCorpus.Get(seed, new SongOverrides(Base: 1, Genre: genre));
            var meters = (ImmutableDictionary<int, Meter>)song.Trace.Single(x => x.Point == TracePoints.SectionMeter).Value!;
            await Assert.That(meters.Values.All(x => Meter.NameOf(x) is "4/4" or "3/4")).IsTrue();
            var scales = song.Trace.Where(x => x.Point == TracePoints.SectionScale).Select(x => ((Scale)x.Value!).Name).ToArray();
            await Assert.That(scales.All(x => x is "Dorian" or "Major")).IsTrue();
            var played = song.Song.Notes!.Values.SelectMany(x => x).Select(x => Feels.Of(x.Value.State)).Distinct().ToArray();
            await Assert.That(played.All(feels.Contains)).IsTrue();
            changed += scales.Distinct().Count() > 1 || meters.Values.Distinct().Count() > 1 ? 1 : 0;
        }

        // and the wildest change them within the pools
        await Assert.That(changed).IsGreaterThan(0);
    }

    [Test]
    public async Task AGenresChords_KeepToItsShapes_TheirHomesAndCadencesToo()
    {
        var genre = Of((GenreChoices.ChordShape, [("seventh", 1), ("ninth", 1), ("sus4", 0.5)]));
        foreach (var seed in Enumerable.Range(0, 8))
        foreach (var @base in new[] { 0.0, 0.5, 1.0 })
        {
            var shapes = GenreReportTest.Shapes(TestCorpus.Get(seed, new SongOverrides(Base: @base, Genre: genre))).Select(x => x.Name).Distinct().ToArray();
            await Assert.That(shapes.All(x => x is "Seventh" or "Ninth" or "Sus4")).IsTrue().Because(string.Join(", ", shapes));
        }
    }

    [Test]
    public async Task ADrumKitsSound_IsOneOfTheGenres_TheStandardWithNone_AndTheMixWinsOverIt()
    {
        var genre = Of((GenreChoices.Kit, [("tr808", 1), ("electronic", 1)]));
        foreach (var seed in Enumerable.Range(0, 8))
        {
            var song = TestCorpus.Get(seed, new SongOverrides(Genre: genre));
            if (!song.PlaysDrums)
                continue;

            await Assert.That(song.Rendered.Tracks.Single(x => x.IsPercussionInstrument).PitchInstrumentCode).IsIn([24, 25]);
            await Assert.That(TestCorpus.Get(seed).Rendered.Tracks.Single(x => x.IsPercussionInstrument).PitchInstrumentCode).IsEqualTo(0);
            var mix = new Rmg.Core.Rendering.SongMix(1, ImmutableDictionary<TrackRole, Rmg.Core.Rendering.PartMix>.Empty.Add(TrackRole.Drum, new(40, 1, null, true)),
                ImmutableDictionary<string, Rmg.Core.Rendering.DrumGroupMix>.Empty);
            await Assert.That(Rmg.Core.Rendering.Render.RenderSong(song.Song, mix, CancellationToken.None).Tracks.Single(x => x.IsPercussionInstrument).PitchInstrumentCode).IsEqualTo(40);
        }
    }

    [Test]
    public async Task AGenresRhythm_MovesWhereAPartsNotesFall()
    {
        var genre = Of(
            (GenreChoices.Rhythms[RhythmPart.Chords], [("speed", 1), ("offbeat", 0.75), ("fineness", 0)]),
            (GenreChoices.Rhythms[RhythmPart.Ground], [("speed", 1), ("fineness", 0), ("fullness", 1), ("repetition", 1)])
        );
        var none = TestCorpus.Measure(16, GenreRhythmReportTest.Falls);
        var moved = TestCorpus.InParallel(Enumerable.Range(0, 16), seed => GenreRhythmReportTest.Falls(TestCorpus.Get(seed, new SongOverrides(Genre: genre))));
        double Average(IEnumerable<Dictionary<RhythmPart, GenreRhythmReportTest.Fall>> songs, RhythmPart part, Func<GenreRhythmReportTest.Fall, double> of) =>
            songs.Where(x => x.ContainsKey(part)).Average(x => of(x[part]));

        // the chords skanking on the offbeats, the kick on every beat, repeating its bars, and the melody as it was
        await Assert.That(Average(moved, RhythmPart.Chords, x => x.Offbeat)).IsGreaterThan(Average(none, RhythmPart.Chords, x => x.Offbeat) + 0.2);
        await Assert.That(Average(moved, RhythmPart.Ground, x => x.PerBeat)).IsGreaterThan(Average(none, RhythmPart.Ground, x => x.PerBeat) + 0.1);
        await Assert.That(Average(moved, RhythmPart.Ground, x => x.Repeated)).IsGreaterThan(Average(none, RhythmPart.Ground, x => x.Repeated) + 0.1);
        await Assert.That(Average(moved, RhythmPart.Melody, x => x.PerBeat)).IsEqualTo(Average(none, RhythmPart.Melody, x => x.PerBeat)).Within(0.05);
    }

    [Test]
    public async Task APartsOwnFeel_TheDrumsRoles_AndHalfTime_AreTheGenres()
    {
        var grouped = Feels.Weights.Select(x => x.Feel).Single(x => Feels.NameOf(x) == "grouped3");
        var genre = Of(
            (GenreChoices.PartFeels[RhythmPart.Time], [("grouped3", 1)]),
            (GenreChoices.DrumRoles[DrumRole.Time], [("ride", 1), ("maracas", 0.5)]),
            (GenreChoices.FeelChances, [("halfTime", 1)])
        );
        var keepers = 0;
        foreach (var seed in Enumerable.Range(0, 8))
        {
            var song = TestCorpus.Get(seed, new SongOverrides(Genre: genre));
            foreach (var (track, definition) in song.Song.TrackDefinitions.Where(x => x.Value is PercussionInstrumentTrack))
            {
                var drum = (PercussionInstrumentTrack)definition;
                // only the ride or the maracas keep time, in the tresillo's grouping
                if (DrumRoles.Of(drum.StateMap) == DrumRole.Time)
                {
                    await Assert.That(DrumPieces.NameOf(DrumGroups.GetDrum(track))).IsIn(["ride", "maracas"]);
                    await Assert.That(Feels.Of(drum.StateMap)).IsEqualTo(grouped);
                    keepers++;
                }
            }

            // every section's drums in half time
            var timeFeels = song.Trace.Where(x => x.Point == TracePoints.TimeFeel).Select(x => (int)x.Value!).ToArray();
            await Assert.That(timeFeels.All(x => x == 1)).IsTrue();
        }

        await Assert.That(keepers).IsGreaterThan(0);
    }

    [Test]
    public async Task ATempoRange_LeansToItsMiddle()
    {
        var genre = Of((GenreChoices.Tempo, [("from", GenreChoices.AmountOfTempo(60 / Meter.BaseTempo)), ("to", GenreChoices.AmountOfTempo(240 / Meter.BaseTempo))]));
        var tempos = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))
            .Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(0).GetStateValue(Rmg.Core.Events.StateKinds.Tempo) * Meter.BaseTempo);

        // within it, and its middle half, from 85 to 170, likelier than its outer halves together
        await Assert.That(tempos.All(x => x is >= 60 and <= 240)).IsTrue();
        await Assert.That(tempos.Count(x => x is >= 85 and <= 170)).IsGreaterThan(tempos.Length / 2);
    }

    // the parts as the songs have them in a middling song, but those given, and those left out
    private static (GenreChoice, (string, double)[]) Parts(IReadOnlyDictionary<string, double> given, params string[] leftOut) =>
        (GenreChoices.Parts, [..GenreChoices.Parts.Options.Where(x => !leftOut.Contains(x.Name)).Select(x => (x.Name, given.GetValueOrDefault(x.Name, x.Default)))]);

    [Test]
    public async Task APartEverySongHas_IsInAMiddlingSongAsTheGenreSays_InEveryOneOrNoneByItsEnds()
    {
        var quarter = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Base: 0.5, Genre: Of(Parts(new Dictionary<string, double> { ["bass"] = 0.25 }))))
            .Song.Draws!.Parts.Contains(TrackRole.Bass));
        await Assert.That(quarter.Count(x => x)).IsBetween(6, 30);
        foreach (var seed in Enumerable.Range(0, 8))
        {
            await Assert.That(TestCorpus.Get(seed, new SongOverrides(Genre: Of(Parts(new Dictionary<string, double>(), "bass")))).Song.Draws!.Parts).DoesNotContain(TrackRole.Bass);
            await Assert.That(TestCorpus.Get(seed, new SongOverrides(Base: 0, Genre: Of(Parts(new Dictionary<string, double> { ["bass"] = 0 })))).Song.Draws!.Parts).DoesNotContain(TrackRole.Bass);
            await Assert.That(TestCorpus.Get(seed, new SongOverrides(Genre: Of(Parts(new Dictionary<string, double>())))).Song.Draws!.Parts).Contains(TrackRole.Bass);
        }
    }

    [Test]
    public async Task AGenresChances_AreNeverLeftOut_AndAlwaysAt1()
    {
        var genre = Of(Parts(new Dictionary<string, double> { ["counterMelody"] = 1 }, "pad"), (GenreChoices.FeelChances, [("swing", 1)]));
        foreach (var seed in Enumerable.Range(0, 16))
        {
            var song = TestCorpus.Get(seed, new SongOverrides(Genre: genre));
            await Assert.That(song.Song.Draws!.Parts).DoesNotContain(TrackRole.Pad);
            await Assert.That(song.Song.Draws.Parts).Contains(TrackRole.CounterMelody);
            // swung wherever the meter pairs the notes its tempo swings, 16ths at a slow one and 8ths otherwise
            var tempo = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(0).GetStateValue(Rmg.Core.Events.StateKinds.Tempo);
            var period = 60 / (Meter.BaseTempo * tempo) / 4 >= Groove.ShortestSwungNote ? 0.5 : 1;
            await Assert.That(song.Song.Draws.Swing.Delay > 0).IsEqualTo(song.Map.Meter.Pairs(period));
        }
    }

    [Test]
    public async Task WhatIsGiven_WinsOverTheGenre()
    {
        var genre = Of((GenreChoices.Meter, [("3/4", 1)]), (GenreChoices.Scale, [("dorian", 1)]));
        var song = TestCorpus.Get(3, new SongOverrides(MeterOption: 0, Scale: Rmg.Core.Composition.Scales.Major, Genre: genre));

        await Assert.That(Meter.NameOf(song.Map.Meter)).IsEqualTo("4/4");
        await Assert.That(song.Song.Draws!.Scale).IsEqualTo(Rmg.Core.Composition.Scales.Major);
    }
}
