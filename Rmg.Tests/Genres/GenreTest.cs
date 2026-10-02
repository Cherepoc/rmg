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
            var plainOnly = ByConvention.Weigh(Options, conventionality, x => x == "plain" ? 1 : 0);
            await Assert.That(plainOnly.Select(x => x.Value)).IsEquivalentTo(["plain"]);
            var oddOnly = ByConvention.Weigh(Options, conventionality, x => x == "odd" ? 1 : 0);
            await Assert.That(oddOnly.Select(x => x.Value)).IsEquivalentTo(["odd"]);
        }
    }

    [Test]
    public async Task APoolsWeights_AreTheMiddles_TheEndsStillChoosing()
    {
        // the odd option a fifth as likely as the plain one in the pool's middle, and still the only one at the wild end
        var middle = Share(ByConvention.Weigh(Options, 0.5, x => x == "odd" ? 0.2 : 1), "odd");
        await Assert.That(middle).IsEqualTo(0.2 / 1.2).Within(1e-9);
        await Assert.That(Share(ByConvention.Weigh(Options, 1, x => x == "odd" ? 0.2 : 1), "odd")).IsEqualTo(1).Within(1e-9);
        await Assert.That(Share(ByConvention.Weigh(Options, 0, x => x == "odd" ? 0.2 : 1), "odd")).IsEqualTo(0).Within(1e-9);
    }

    [Test]
    public async Task AChancesWeight_IsNeverAt0_AsItIsAtAHalf_AndAlwaysAt1()
    {
        var chance = new ByConvention(0, 0.2, 1);
        foreach (var conventionality in new[] { 0, 0.25, 0.5, 0.75, 1 })
        {
            await Assert.That(chance.At(conventionality, 0)).IsEqualTo(0);
            await Assert.That(chance.At(conventionality, 0.5)).IsEqualTo(chance.At(conventionality)).Within(1e-12);
            await Assert.That(chance.At(conventionality, 1)).IsEqualTo(1).Within(1e-12);
            await Assert.That(chance.At(conventionality, null)).IsEqualTo(chance.At(conventionality));
        }

        // and between, leaning by its odds
        await Assert.That(chance.At(0.5, 0.8)).IsEqualTo(0.2 * 4 / (0.2 * 4 + 0.8)).Within(1e-12);
    }

    [Test]
    public async Task AGenre_IsReadFromItsChoicesNames_AndWhatIsNotAGenre_IsNot()
    {
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> Choices(string choice, string option, double amount) =>
            new Dictionary<string, IReadOnlyDictionary<string, double>> { [choice] = new Dictionary<string, double> { [option] = amount } };

        var pool = Genre.Read(Choices("meter", "3/4", 1), out _)!.Pool<string>(GenreChoices.Meter, x => x)!;
        await Assert.That(pool("3/4")).IsEqualTo(1);
        await Assert.That(pool("4/4")).IsEqualTo(0);
        await Assert.That(Genre.None.Pool<string>(GenreChoices.Meter, x => x)).IsNull();
        foreach (var (choice, option, amount) in new[] { ("kazoo", "3/4", 1.0), ("meter", "1/1", 1.0), ("meter", "3/4", 1.5), ("meter", "3/4", 0.0) })
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
            // every default as the songs have it: a pool's heaviest 1, a chance and a value as they are
            if (choice.Kind == GenreChoiceKind.Pool)
                await Assert.That(choice.Options.Max(x => x.Default)).IsEqualTo(1).Because(choice.Name);
            else
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
            (GenreChoices.Tempo, [("slow", 1)]),
            (GenreChoices.DrumPieces, [..DrumPieces.All.Where(x => x.Name != "cowbell").Select(x => (x.Name, 1.0))])
        );
        var melody = new[] { 73, 71 };
        foreach (var seed in Enumerable.Range(0, 16))
        foreach (var @base in new[] { 0.0, 0.5, 1.0 })
        {
            var song = TestCorpus.Get(seed, new SongOverrides(Base: @base, Genre: genre));

            await Assert.That(Meter.NameOf(song.Map.Meter)).IsEqualTo("3/4");
            await Assert.That(song.Song.Draws!.Scale.Name).IsIn(["Dorian", "Mixolydian"]);
            await Assert.That(GenreChoices.TempoBandOf(song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(0).GetStateValue(Rmg.Core.Events.StateKinds.Tempo))).IsEqualTo("slow");
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
    public async Task AGenresChances_AreNeverAt0_AndAlwaysAt1()
    {
        var genre = Of((GenreChoices.Parts, [("pad", 0), ("counterMelody", 1)]), (GenreChoices.FeelChances, [("swing", 1)]));
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
