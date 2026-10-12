using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

public sealed class GenreTest
{
    // a genre of amounts alike at every end
    private static Genre Of(params (GenreChoice Choice, (string Option, double Amount)[] Options)[] choices) =>
        new(choices.ToImmutableDictionary(x => x.Choice.Name, x => x.Options.ToImmutableDictionary(y => y.Option, y => Alike(y.Amount))));

    private static ByConvention Alike(double amount) => new(amount, amount, amount);

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
            await Assert.That(ByConvention.Weigh(Options, conventionality, null)).IsEquivalentTo(ByConvention.Weigh(Options, conventionality), CollectionOrdering.Matching);
    }

    [Test]
    public async Task AnOptionOutOfThePool_NeverPlays_AndOneAlone_AlwaysDoes_HoweverPlainOrWild()
    {
        foreach (var conventionality in new[] { 0, 0.5, 1 })
        {
            var plainOnly = ByConvention.Weigh(Options, conventionality, x => x == "plain" ? Alike(1) : null);
            await Assert.That(plainOnly.Select(x => x.Value)).IsEquivalentTo(["plain"], CollectionOrdering.Matching);
            var oddOnly = ByConvention.Weigh(Options, conventionality, x => x == "odd" ? Alike(1) : null);
            await Assert.That(oddOnly.Select(x => x.Value)).IsEquivalentTo(["odd"], CollectionOrdering.Matching);
        }
    }

    [Test]
    public async Task APoolsWeights_AreItsOwnAtEveryEnd_InPlaceOfTheSongs()
    {
        // the odd option the plain end's and the plain one the wild's, the other way round from the songs, alike in the middle
        Func<string, ByConvention?> pool = x => x == "odd" ? new ByConvention(1, 0.5, 0) : new ByConvention(0, 0.5, 1);
        await Assert.That(Share(ByConvention.Weigh(Options, 0, pool), "odd")).IsEqualTo(1);
        await Assert.That(Share(ByConvention.Weigh(Options, 0.5, pool), "odd")).IsEqualTo(0.5).Within(1e-9);
        await Assert.That(Share(ByConvention.Weigh(Options, 1, pool), "odd")).IsEqualTo(0);
        // and a pool of the songs' own weighs them as they do
        foreach (var conventionality in new[] { 0, 0.25, 0.5, 0.75, 1 })
            await Assert.That(Share(ByConvention.Weigh(Options, conventionality, x => Options.Single(o => o.Item1 == x).Item2), "odd"))
                .IsEqualTo(Share(ByConvention.Weigh(Options, conventionality), "odd")).Within(1e-9);
    }

    [Test]
    public async Task AnOptionOnlyTheWildEndWeighs_PlaysOnlyTheWilder_AndAPoolOfThemAlone_AtEveryConventionality()
    {
        Func<string, ByConvention?> wildOnly = x => x == "odd" ? new ByConvention(0, 0, 1) : Alike(1);
        await Assert.That(Share(ByConvention.Weigh(Options, 0.5, wildOnly), "odd")).IsEqualTo(0);
        await Assert.That(Share(ByConvention.Weigh(Options, 0.75, wildOnly), "odd")).IsGreaterThan(0);
        await Assert.That(Share(ByConvention.Weigh(Options, 1, wildOnly), "odd")).IsEqualTo(0.5).Within(1e-9);
        // an end none weighs takes the middle's, and the middle with none the nearer end's
        foreach (var conventionality in new[] { 0, 0.5, 1 })
            await Assert.That(ByConvention.Weigh(Options, conventionality, x => x == "odd" ? new ByConvention(0, 0, 1) : null).Select(x => x.Value)).IsEquivalentTo(["odd"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task ARolesOwnWeights_LeanByThePoolsOverTheSongs()
    {
        // a home's own weights, the pool weighing the odd option twice as the songs do at every end and the plain half
        Func<string, (ByConvention, ByConvention)?> lean = x => x == "odd" ? (Alike(0.2), Alike(0.1)) : (Alike(0.5), Alike(1));
        var own = new[] { ("plain", Alike(1)), ("odd", Alike(1)) };
        await Assert.That(Share(ByConvention.Lean(own, 0.5, lean), "odd")).IsEqualTo(2 / 2.5).Within(1e-9);
        // where the songs never play an option the pool keeps, the pool's weight alone, though the role's own is 0
        Func<string, (ByConvention, ByConvention)?> plainKept = x => x == "odd" ? (Alike(1), Alike(1)) : (Alike(1), Alike(0));
        var oddOnly = new[] { ("plain", Alike(0)), ("odd", Alike(1)) };
        await Assert.That(Share(ByConvention.Lean(oddOnly, 0.5, plainKept), "plain")).IsEqualTo(0.5).Within(1e-9);
    }

    [Test]
    public async Task AChance_IsItsOwnAsTheSongsHaveIt_OrTheGenres()
    {
        var chance = new ByConvention(0, 0.2, 1);
        foreach (var conventionality in new[] { 0, 0.25, 0.5, 0.75, 1 })
        {
            await Assert.That(chance.At(conventionality, null)).IsEqualTo(chance.At(conventionality));
            await Assert.That(chance.At(conventionality, default(ByConvention))).IsEqualTo(0);
            await Assert.That(chance.At(conventionality, Alike(1))).IsEqualTo(1);
        }

        await Assert.That(chance.At(0.5, new ByConvention(0.1, 0.3, 0.6))).IsEqualTo(0.3).Within(1e-9);
    }

    [Test]
    public async Task AGenre_IsReadFromItsChoicesNames_AndWhatIsNotAGenre_IsNot()
    {
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, ByConvention>> Choices(string choice, string option, ByConvention amounts) =>
            new Dictionary<string, IReadOnlyDictionary<string, ByConvention>> { [choice] = new Dictionary<string, ByConvention> { [option] = amounts } };

        var pool = Genre.Read(Choices("meter", "3/4", Alike(1)), out _)!.Pool<string>(GenreChoices.Meter, x => x)!;
        await Assert.That(pool("3/4")).IsEqualTo(Alike(1));
        await Assert.That(pool("4/4")).IsNull();
        await Assert.That(Genre.Read(Choices("meter", "3/4", new ByConvention(0, 0, 1)), out _)!.Pool<string>(GenreChoices.Meter, x => x)!("3/4")).IsEqualTo(new ByConvention(0, 0, 1));
        await Assert.That(Genre.None.Pool<string>(GenreChoices.Meter, x => x)).IsNull();
        foreach (var (choice, option, amounts) in new[] { ("kazoo", "3/4", Alike(1)), ("meter", "1/1", Alike(1)), ("meter", "3/4", new ByConvention(0, 1.5, 0)) })
        {
            await Assert.That(Genre.Read(Choices(choice, option, amounts), out var error)).IsNull();
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
            // every default as the songs have it: a pool's heaviest 1 at every end that weighs any, a range's the songs', a
            // value as it is
            if (choice.Kind == GenreChoiceKind.Pool)
                foreach (var end in new Func<ByConvention, double>[] { x => x.Plain, x => x.Tuned, x => x.Wild })
                    await Assert.That(choice.Options.Max(x => end(x.Own))).IsIn([0.0, 1.0]).Because(choice.Name);
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
        var drummed = 0;
        foreach (var seed in Enumerable.Range(0, 8))
        {
            var song = TestCorpus.Get(seed, new SongOverrides(Genre: genre));
            if (!song.PlaysDrums)
                continue;
            drummed++;

            await Assert.That(song.Rendered.Tracks.Single(x => x.IsPercussionInstrument).PitchInstrumentCode).IsIn([24, 25]);
            await Assert.That(TestCorpus.Get(seed).Rendered.Tracks.Single(x => x.IsPercussionInstrument).PitchInstrumentCode).IsEqualTo(0);
            var mix = new Rmg.Core.Rendering.SongMix(1, ImmutableDictionary<TrackRole, Rmg.Core.Rendering.PartMix>.Empty.Add(TrackRole.Drum, new(40, 1, null)),
                ImmutableDictionary<string, Rmg.Core.Rendering.DrumGroupMix>.Empty);
            await Assert.That(Rmg.Core.Rendering.Render.RenderSong(song.Song, mix, CancellationToken.None).Tracks.Single(x => x.IsPercussionInstrument).PitchInstrumentCode).IsEqualTo(40);
        }

        await Assert.That(drummed).IsGreaterThan(0);
    }

    [Test]
    public async Task AGenresRhythm_MovesWhereAPartsNotesFall()
    {
        var genre = Of(
            (GenreChoices.Rhythms[RhythmPart.Chords], [("speed", 1), ("offbeat", 0.75), ("fineness", 0)]),
            (GenreChoices.Rhythms[RhythmPart.Ground], [("speed", 1), ("fineness", 0), ("fullness", 1), ("repetition", 1)])
        );
        var none = TestCorpus.Measure(64, GenreRhythmReportTest.Falls);
        var moved = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => GenreRhythmReportTest.Falls(TestCorpus.Get(seed, new SongOverrides(Genre: genre))));
        double Average(IEnumerable<Dictionary<RhythmPart, GenreRhythmReportTest.Fall>> songs, RhythmPart part, Func<GenreRhythmReportTest.Fall, double> of) =>
            songs.Where(x => x.ContainsKey(part)).Average(x => of(x[part]));

        Console.WriteLine($"chords offbeat {Average(none, RhythmPart.Chords, x => x.Offbeat):F2} -> {Average(moved, RhythmPart.Chords, x => x.Offbeat):F2}, kick a beat {Average(none, RhythmPart.Ground, x => x.PerBeat):F2} -> {Average(moved, RhythmPart.Ground, x => x.PerBeat):F2}, repeated {Average(none, RhythmPart.Ground, x => x.Repeated):F2} -> {Average(moved, RhythmPart.Ground, x => x.Repeated):F2}");
        // the chords skanking on the offbeats, the kick on every beat, repeating its bars, and the melody as it was
        await Assert.That(Average(moved, RhythmPart.Chords, x => x.Offbeat)).IsGreaterThan(Average(none, RhythmPart.Chords, x => x.Offbeat) + 0.1);
        // measured 0.95 to 1.05 a beat, a kick keeping a cycle of a bar at most playing every bar already
        await Assert.That(Average(moved, RhythmPart.Ground, x => x.PerBeat)).IsGreaterThan(Average(none, RhythmPart.Ground, x => x.PerBeat) + 0.05);
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

    // a tempo's amount of beats a minute
    private static double TempoAmount(double bpm) => GenreChoices.AmountOfTempo(bpm / Meter.BaseTempo);

    private static double Bpm(CorpusSong song) =>
        song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(0).GetStateValue(Rmg.Core.Events.StateKinds.Tempo) * Meter.BaseTempo;

    [Test]
    public async Task ATempoRange_IsItsOwnAtEveryEnd()
    {
        // 70 to 85 beats a minute up to the middle, 100 to 130 at the wild end
        var (from, to) = (new ByConvention(TempoAmount(70), TempoAmount(70), TempoAmount(100)), new ByConvention(TempoAmount(85), TempoAmount(85), TempoAmount(130)));
        var genre = new Genre(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty.Add(GenreChoices.Tempo.Name,
            ImmutableDictionary<string, ByConvention>.Empty.Add("from", from).Add("to", to)));
        foreach (var seed in Enumerable.Range(0, 16))
        {
            await Assert.That(Bpm(TestCorpus.Get(seed, new SongOverrides(Base: 0, Genre: genre)))).IsBetween(70, 85);
            // a middling song's range at its feel, which strays around its base
            var middling = TestCorpus.Get(seed, new SongOverrides(Base: 0.5, Genre: genre));
            var feel = middling.Unconventionality.Facets[Facet.Feel];
            await Assert.That(Bpm(middling)).IsBetween(GenreChoices.TempoOf(from.At(feel)) * Meter.BaseTempo - 1e-6, GenreChoices.TempoOf(to.At(feel)) * Meter.BaseTempo + 1e-6)
                .Because($"seed {seed} at a feel of {feel}");
            await Assert.That(Bpm(TestCorpus.Get(seed, new SongOverrides(Base: 1, Genre: genre)))).IsBetween(100, 130);
        }
    }

    [Test]
    public async Task ATempoRange_LeansToItsFastEnd_OrItsSlow()
    {
        Genre Leaning(double lean) => new(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty.Add(GenreChoices.Tempo.Name,
            ImmutableDictionary<string, ByConvention>.Empty.Add("from", Alike(TempoAmount(80))).Add("to", Alike(TempoAmount(160))).Add("lean", Alike(lean))));
        double Average(Genre genre) => TestCorpus.InParallel(Enumerable.Range(0, 32), seed => Bpm(TestCorpus.Get(seed, new SongOverrides(Genre: genre)))).Average();

        var middle = Average(Leaning(0.5));
        await Assert.That(Average(Leaning(1))).IsGreaterThan(middle + 10);
        await Assert.That(Average(Leaning(0))).IsLessThan(middle - 10);
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

    [Test]
    public async Task ALengthRange_IsWhereItsSongsEnd_AsAShortGenreAndALongOneHaveIt()
    {
        // a genre of songs from a minute and a half to two and a half, and one from six minutes to nine
        Genre Lasting(double from, double to) => Of((GenreChoices.Length, [("from", GenreChoices.AmountOfLength(from)), ("to", GenreChoices.AmountOfLength(to))]));
        double[] Seconds(Genre genre) => TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre)).Rendered.Seconds);

        var short_ = Seconds(Lasting(90, 150));
        var long_ = Seconds(Lasting(360, 540));
        // as near as a length given comes, within 15% of the range, most of them
        await Assert.That(short_.Count(x => x is >= 90 * 0.85 and <= 150 * 1.15)).IsGreaterThanOrEqualTo(24);
        await Assert.That(long_.Count(x => x is >= 360 * 0.85 and <= 540 * 1.15)).IsGreaterThanOrEqualTo(24);
        // and a length given beats the genre's
        await Assert.That(TestCorpus.Get(0, new SongOverrides(Genre: Lasting(360, 540), Length: 120)).Rendered.Seconds).IsLessThan(200);
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
    public async Task ASectionsOwnTime_Polymeters_Answers_ALastRepeat_Doublings_AndSwitches_AreAGenres_NeverAt0()
    {
        (GenreChoice, (string, double)[]) None(GenreChoice choice) => (choice, [..choice.Options.Select(x => (x.Name, 0.0))]);
        var never = Of(None(GenreChoices.SectionChanges), None(GenreChoices.Polymeter), None(GenreChoices.SoloAnswers), None(GenreChoices.LastRepeat),
            None(GenreChoices.Doubling), None(GenreChoices.InstrumentSwitches), (GenreChoices.Parts, [("solo", 1), ("counterMelody", 1), ("riff", 1)]));
        var always = Of((GenreChoices.SoloAnswers, [("sections", 1)]), (GenreChoices.Doubling, [("octaves", 1), ("harmony", 0), ("riff", 0)]));
        var (answers, octaves) = (0, 0);
        foreach (var seed in Enumerable.Range(0, 16))
        {
            // a wild song's, which plays them oftenest
            var song = TestCorpus.Get(seed, new SongOverrides(Base: 0.9, Genre: never));
            await Assert.That(((System.Collections.IDictionary)song.Trace.Single(x => x.Point == TracePoints.Polymeter).Value!).Count).IsEqualTo(0);
            await Assert.That(((ImmutableArray<double>)song.Trace.Single(x => x.Point == TracePoints.SectionTempo).Value!).All(x => x == 1)).IsTrue();
            await Assert.That(song.Map.Sections.All(x => x.Meter.HasSameBar(song.Map.Meter))).IsTrue();
            await Assert.That(song.Trace.Where(x => x.Point == TracePoints.SoloAnswers).All(x => !(((int, bool Answers))x.Value!).Answers)).IsTrue();
            await Assert.That((bool)song.Trace.Single(x => x.Point == TracePoints.LastRepeat).Value!).IsFalse();
            await Assert.That(song.Trace.Any(x => x.Point == TracePoints.LineDoubling && (((int, TrackRole Source, TrackRole Target, int))x.Value!).Target != TrackRole.RiffTwin)).IsFalse();
            await Assert.That(song.Trace.Where(x => x.Point == TracePoints.Octave).All(x => !(((int, TrackRole, double, bool InOctaves))x.Value!).InOctaves)).IsTrue();
            await Assert.That(song.Trace.Any(x => x.Point == TracePoints.InstrumentOverTime && song.Song.TrackDefinitions[x.Track] is PitchInstrumentTrack part
                && (((int, TrackRole, int Program, ArticulationMode, int))x.Value!).Program != part.InstrumentCode)).IsFalse();

            var given = TestCorpus.Get(seed, new SongOverrides(Genre: always));
            await Assert.That(given.Trace.Where(x => x.Point == TracePoints.SoloAnswers).All(x => (((int, bool Answers))x.Value!).Answers)).IsTrue();
            await Assert.That(given.Trace.Where(x => x.Point == TracePoints.Octave).All(x => (((int, TrackRole, double, bool InOctaves))x.Value!).InOctaves)).IsTrue();
            (answers, octaves) = (answers + given.Trace.Count(x => x.Point == TracePoints.SoloAnswers), octaves + given.Trace.Count(x => x.Point == TracePoints.Octave));
        }

        // the 'always' checked on some appearances of each
        await Assert.That(answers).IsGreaterThan(0);
        await Assert.That(octaves).IsGreaterThan(0);
    }

    /// <summary>A song of a preset that names its intros opens on one of them, a hook intro among them only where it names it; opening on the riff, its band entering, aside.</summary>
    [Test]
    public async Task ASongsIntro_IsOneItsGenreNames()
    {
        foreach (var preset in GenrePresets.All.Where(x => x.Choices.ContainsKey(GenreChoices.Intro.Name)))
        {
            var named = preset.Choices[GenreChoices.Intro.Name].Where(x => x.Value.ValueKind == System.Text.Json.JsonValueKind.Array ? x.Value.EnumerateArray().Any(y => y.GetInt32() > 0) : x.Value.GetInt32() > 0)
                .Select(x => x.Key).ToHashSet();
            foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(preset)))))
            {
                var intro = song.Map.Intro;
                var opensOnRiff = intro is { Kind: IntroKind.Entries, Entries: [{ Part.Role: TrackRole.Riff }, ..] };
                await Assert.That(named.Contains(GenreChoices.Name(intro.Kind.ToString())) || opensOnRiff || intro.Kind == IntroKind.Cold && named.Count == 0)
                    .IsTrue().Because($"{preset.Id} seed {song.Seed}: {intro.Kind}");
            }
        }
    }

    [Test]
    public async Task EveryDevice_NamedAsTheSongsHaveIt_PlaysAsNoGenre()
    {
        // a device's own amounts the songs', so that a preset that names it as they have it plays it as they do
        var genre = new Genre(GenreChoices.Devices.ToImmutableDictionary(x => x.Name, x => x.Options.ToImmutableDictionary(o => o.Name, o => o.Own)));
        foreach (var seed in Enumerable.Range(0, 8))
        {
            static string[] Notes(CorpusSong song) =>
                [..song.Song.Notes!.SelectMany(x => x.Value.Select(n => $"{x.Key} {n.Position:F4} {string.Join(",", n.Value.Pitches)} {n.Value.Duration:F4}"))];
            await Assert.That(Notes(TestCorpus.Get(seed, new SongOverrides(Genre: genre)))).IsEquivalentTo(Notes(TestCorpus.Get(seed)), CollectionOrdering.Matching).Because($"seed {seed}");
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

    /// <summary>A value's plain and wild ends lean a plain and a wild song: read at the song's facet its choice follows.</summary>
    [Test]
    public async Task AValue_IsReadAtTheSongsFacet_ItsPlainEndInAPlainSong_ItsWildInAWild()
    {
        var genre = new Genre(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty
            .Add(GenreChoices.Fills.Name, ImmutableDictionary<string, ByConvention>.Empty.Add("amount", new ByConvention(0, 0.5, 1))));
        Unconventionality At(double fills) => Unconventionality.Generate(0.5, _ => new GenerationContext(0)) is var u ? u with { Facets = u.Facets.SetItem(Facet.Fills, fills) } : null!;
        await Assert.That(genre.AtSong(At(0)).Shift(GenreChoices.Fills, "amount")).IsEqualTo(-1).Within(1e-9);
        await Assert.That(genre.AtSong(At(0.5)).Shift(GenreChoices.Fills, "amount")).IsEqualTo(0).Within(1e-9);
        await Assert.That(genre.AtSong(At(1)).Shift(GenreChoices.Fills, "amount")).IsEqualTo(1).Within(1e-9);
        await Assert.That(GenreChoices.All.Where(x => x.Kind == GenreChoiceKind.Values).All(x => x.Follows is not null)).IsTrue();
    }

    /// <summary>A pool keeping options that weigh nothing at every end keeps none, and is refused as a pool keeping none is.</summary>
    [Test]
    public async Task APoolWeighingNothing_IsRefused()
    {
        var pool = new Dictionary<string, IReadOnlyDictionary<string, ByConvention>> { ["meter"] = new Dictionary<string, ByConvention> { ["4/4"] = new(0, 0, 0) } };
        await Assert.That(Genre.Read(pool, out var error)).IsNull();
        await Assert.That(error).Contains("keeps none");
    }
}
