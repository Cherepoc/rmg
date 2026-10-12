using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Forms;

/// <summary>The big ending (<see cref="EndingKind.Big" />): the home chord held while the drums roll, then the band's last hit together.</summary>
public sealed class BigEndingTest
{
    private static CorpusSong[] Songs(string genre, int count) =>
        TestCorpus.InParallel(Enumerable.Range(0, count), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre))));

    /// <summary>A big ending strikes its final chord, rolls the toms over it, and strikes it again at its end with the drums landing.</summary>
    [Test]
    [Arguments("rock")]
    [Arguments("metal")]
    public async Task ABigEnding_HoldsItsChord_UnderATomRoll_AndStrikesItAgain(string genre)
    {
        var tom = DrumGroups.GetTrackNumber(DrumDefinitions.Tom);
        var endings = 0;
        foreach (var song in Songs(genre, 64).Where(x => x.Map.Ending.Kind == EndingKind.Big))
        {
            endings++;
            var (start, button) = (song.Map.Ending.Start, song.Map.Ending.Start + song.Map.Ending.Held);
            bool Struck(double at) => song.Song.Notes!.Where(x => !song.IsDrum(x.Key)).Any(x => x.Value.Any(n => Math.Abs(n.Position - at) < 1e-6));
            await Assert.That(Struck(start)).IsTrue().Because($"seed {song.Seed}, its final chord at {start}");
            await Assert.That(Struck(button)).IsTrue().Because($"seed {song.Seed}, its last hit at {button}");
            if (song.Song.Notes!.TryGetValue(tom, out var toms))
                await Assert.That(toms.Count(n => n.Position > start + 1e-6 && n.Position < button - 1e-6)).IsGreaterThan(2).Because($"seed {song.Seed}, its roll");
        }

        await Assert.That(endings).IsGreaterThan(3);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var genre in new[] { "rock", "metal", "thrash", "doom", "symphonic", "punk", "blues", "soul", "funk", "country", "ska", "ballad", "nuMetal", "blackMetal", "djent", "pop" })
        {
            var songs = Songs(genre, 64);
            var big = songs.Where(x => x.Map.Ending.Kind == EndingKind.Big).ToArray();
            Console.WriteLine($"{genre}: big endings {big.Length} of {songs.Length}, held {big.Select(x => x.Map.Ending.Held).DefaultIfEmpty().Average():0.0} beats, slowing {big.Count(x => x.Map.Ending.SlowsDown)}");
        }

        await Task.CompletedTask;
    }
}
