using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Arrangements;

/// <summary>A twin-guitar break: a pitched solo played by two parts together a third apart.</summary>
public sealed class SoloTwinTest
{
    /// <summary>A preset whose every later section is a solo of one soloist, twinned.</summary>
    private static Genre Always(string preset)
    {
        var genre = GenrePresets.With(preset, GenreChoices.Solos, ("sections", 1), ("twins", 1));
        return genre with { Choices = genre.Choices.SetItem(GenreChoices.SoloCount.Name, ImmutableDictionary<string, ByConvention>.Empty.Add("one", new ByConvention(1, 1, 1))) };
    }

    /// <summary>
    ///     A twinned solo is two parts or more together, the others a third apart, playing none of the first's notes that
    ///     last less than an 8th to its next.
    /// </summary>
    [Test]
    public async Task ATwinnedSolo_IsTwoPartsTogetherAThirdApart_TheSecondOnTheHeldNotes()
    {
        var twins = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Always("metal")))))
        foreach (var (index, solo) in song.Solos.Where(x => !x.Value.IsDrumSolo && !x.Value.TradesWithDrums && x.Value.Soloists.Length > 1))
        {
            var because = $"seed {song.Seed} section {index}";
            twins++;
            await Assert.That(solo.Mode).IsEqualTo(SoloMode.Together).Because(because);
            await Assert.That(Math.Abs(solo.Step)).IsEqualTo(2).Because(because);
            var span = song.Map.Sections[index];
            // a part's notes in the solo, by their onsets
            Dictionary<double, Rmg.Core.Events.StateMap> Notes(TrackRole role) => song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role == role)
                .SelectMany(x => x.Value).Where(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9)
                .GroupBy(x => Math.Round(x.Position, 6)).ToDictionary(x => x.Key, x => x.First().Value.State);
            var first = Notes(solo.Soloists[0]);
            var onsets = first.Keys.Order().ToArray();
            foreach (var other in solo.Soloists.Skip(1))
            foreach (var (at, state) in Notes(other))
            {
                var next = onsets.FirstOrDefault(x => x > at + 1e-6, double.PositiveInfinity);
                await Assert.That(next - at).IsGreaterThanOrEqualTo(Rmg.Core.Composition.Solos.TogetherShortest - 1e-6).Because($"{because} at {at}");
                // the first's note a third away, the first playing one with every note of the twin's
                await Assert.That(first.TryGetValue(at, out var played)).IsTrue().Because($"{because} at {at}");
                await Assert.That(state.GetStateValue(Rmg.Core.Events.StateKinds.ScaleStep) - played!.GetStateValue(Rmg.Core.Events.StateKinds.ScaleStep)).IsEqualTo(solo.Step).Because($"{because} at {at}");
            }
        }

        await Assert.That(twins).IsGreaterThan(4);
    }

    /// <summary>A twin joins a soloist alone: a solo drawn with several soloists shares as the solo's share says, the same with a twin's chance of 1 as of 0.</summary>
    [Test]
    public async Task ATwin_JoinsASoloistAlone_SeveralSoloistsSharingAsTheSoloShareSays()
    {
        Genre Thrash(double twins) => GenrePresets.With("thrash", GenreChoices.Solos, ("sections", 1), ("twins", twins), ("trades", 0));
        var duels = 0;
        foreach (var (without, with) in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => (TestCorpus.Get(seed, new SongOverrides(Genre: Thrash(0))), TestCorpus.Get(seed, new SongOverrides(Genre: Thrash(1))))))
        foreach (var (index, solo) in without.Solos.Where(x => !x.Value.IsDrumSolo && x.Value.Soloists.Length > 1))
        {
            duels++;
            var twinned = with.Solos[index];
            var because = $"seed {without.Seed} section {index}";
            await Assert.That(twinned.Soloists.SequenceEqual(solo.Soloists)).IsTrue().Because(because);
            await Assert.That(twinned.Mode).IsEqualTo(solo.Mode).Because(because);
            await Assert.That(twinned.Step).IsEqualTo(solo.Step).Because(because);
        }

        // (52 to 94 at offsets 0, a million and 958 million)
        Console.WriteLine($"{duels} solos of several soloists in 64 Thrash songs");
        await Assert.That(duels).IsGreaterThan(20);
    }

    /// <summary>How many of the presets' pitched solos are played by several parts, and how many of those together.</summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var name in new[] { "metal", "rock", "thrash", "symphonic", "jazz" })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(name))));
            var solos = songs.SelectMany(x => x.Solos.Values).Where(x => !x.IsDrumSolo && !x.TradesWithDrums).ToArray();
            var several = solos.Where(x => x.Soloists.Length > 1).ToArray();
            Console.WriteLine($"{name}: pitched solos {solos.Length} in {songs.Length} songs, several parts {several.Length}, together {several.Count(x => x.Mode == SoloMode.Together)}, " +
                              $"trading {several.Count(x => x.Mode == SoloMode.Trading)}");
        }

        await Task.CompletedTask;
    }
}
