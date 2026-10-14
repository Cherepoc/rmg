using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Forms;

/// <summary>One part opening the song alone, the band coming in part by part after it.</summary>
public sealed class OpeningAloneTest
{
    private static Genre Always(string preset) => GenrePresets.With(preset, GenreChoices.OpeningAlone, ("songs", 1));

    /// <summary>The part a song opens on alone, where its entries start with it; none where it opens otherwise.</summary>
    private static TrackRole? OpensOn(CorpusSong song) =>
        song.Trace.Single(x => x.Point == TracePoints.OpeningAlone).Value is TrackRole part && song.Map.Intro is { Kind: IntroKind.Entries, Entries: [{ Entry: 0 } first, ..] } && first.Part.Role == part
            ? part
            : null;

    /// <summary>
    ///     A song opening on one part plays it alone in the first bar, nothing else struck there but the riff's twin with
    ///     the riff and the drums' fill leading the band in, in its second half; every other part comes in a bar later at least.
    /// </summary>
    /// <param name="preset">A genre whose songs open on its riff, its drums, its bass or its chords.</param>
    [Test]
    [Arguments("rock")]
    [Arguments("funk")]
    [Arguments("ballad")]
    public async Task ASongOpeningOnOnePart_PlaysItAloneInItsFirstBar(string preset)
    {
        var opened = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Always(preset)))))
        {
            if (OpensOn(song) is not { } part)
                continue;
            opened++;
            var intro = song.Map.Intro;
            var start = intro.Window.IsBefore ? 0 : intro.Duration;
            var bar = song.Map.Sections[0].Meter.BarDuration;
            await Assert.That(intro.Entries.Where(x => x.Part.Role != part).All(x => x.Entry >= bar - 1e-9)).IsTrue().Because($"seed {song.Seed}");
            // but the riff's twin, which harmonises it, and the drums' fill leading the band in, in the bar's second half
            bool Alone(TrackRole role) => role == part || part == TrackRole.Riff && role == TrackRole.RiffTwin;
            var others = song.Song.Notes!.Where(x => !Alone(song.Song.TrackDefinitions[x.Key].Role))
                .SelectMany(x => x.Value.Where(n => n.Position >= start - 1e-9 && n.Position < start + bar - 1e-9)
                    .Where(n => song.Song.TrackDefinitions[x.Key].Role != TrackRole.Drum || n.Position < start + bar / 2 - 1e-9))
                .Count();
            await Assert.That(others).IsEqualTo(0).Because($"seed {song.Seed} opening on its {part}");
        }

        Console.WriteLine($"{preset}: {opened} of 32 songs opening on one part");
        await Assert.That(opened).IsGreaterThan(8);
    }

    /// <summary>How many songs of the presets opening on one part open on it, and on which.</summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var name in new[] { "rock", "metal", "nuMetal", "djent", "blackMetal", "punk", "soul", "funk", "disco", "ballad" })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(name))));
            var always = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Always(name))));
            var parts = string.Join(", ", always.Select(OpensOn).Where(x => x is not null).GroupBy(x => x).Select(x => $"{x.Key} {x.Count()}"));
            Console.WriteLine($"{name}: songs opening on one part {songs.Average(x => OpensOn(x) is null ? 0 : 1.0):P0}, every song asked to {always.Average(x => OpensOn(x) is null ? 0 : 1.0):P0} ({parts})");
        }

        await Task.CompletedTask;
    }
}
