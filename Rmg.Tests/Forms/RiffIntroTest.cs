using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Forms;

/// <summary>The riff opening the song alone, the band coming in part by part after it.</summary>
public sealed class RiffIntroTest
{
    private static Genre Always(string preset) => GenrePresets.With(preset, GenreChoices.RiffIntro, ("songs", 1));

    private static bool OpensOnTheRiff(CorpusSong song) => song.Map.Intro is { Kind: IntroKind.Entries, Entries: [{ Part.Role: TrackRole.Riff, Entry: 0 }, ..] };

    /// <summary>
    ///     A song opening on its riff plays its riff alone in the first bar, nothing else struck there but its twin and the drums' fill
    ///     leading the band in, in its second half; every other part comes in a bar later at least.
    /// </summary>
    [Test]
    public async Task ASongOpeningOnItsRiff_PlaysTheRiffAloneInItsFirstBar()
    {
        var opened = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Always("rock")))))
        {
            if (!OpensOnTheRiff(song))
                continue;
            opened++;
            var intro = song.Map.Intro;
            var start = intro.Window.IsBefore ? 0 : intro.Duration;
            var bar = song.Map.Sections[0].Meter.BarDuration;
            await Assert.That(intro.Entries.Skip(1).All(x => x.Entry >= bar - 1e-9)).IsTrue().Because($"seed {song.Seed}");
            // but the riff's twin, which harmonises it, and the drums' fill leading the band in, in the bar's second half
            var others = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role is not (TrackRole.Riff or TrackRole.RiffTwin))
                .SelectMany(x => x.Value.Where(n => n.Position >= start - 1e-9 && n.Position < start + bar - 1e-9)
                    .Where(n => song.Song.TrackDefinitions[x.Key].Role != TrackRole.Drum || n.Position < start + bar / 2 - 1e-9))
                .Count();
            await Assert.That(others).IsEqualTo(0).Because($"seed {song.Seed}");
        }

        await Assert.That(opened).IsGreaterThan(8);
    }

    /// <summary>How many songs of the riff-driven presets open on their riff.</summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var name in new[] { "rock", "metal", "nuMetal", "djent", "blackMetal" })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(name))));
            var always = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Always(name))));
            Console.WriteLine($"{name}: songs opening on the riff {songs.Average(x => OpensOnTheRiff(x) ? 1.0 : 0):P0}, every song asked to {always.Average(x => OpensOnTheRiff(x) ? 1.0 : 0):P0}");
        }

        await Task.CompletedTask;
    }
}
