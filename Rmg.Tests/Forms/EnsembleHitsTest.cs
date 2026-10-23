using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Forms;

/// <summary>Ensemble hits (<see cref="StopTime" />): a section's last bar before a louder one, the band striking with the kick and resting between.</summary>
public sealed class EnsembleHitsTest
{
    /// <summary>The bars a song strikes, with the kick's onsets in each.</summary>
    private static IEnumerable<(double Start, double End, double[] Kick)> Struck(CorpusSong song)
    {
        var kicks = DrumGroups.Kick.Drums.Select(DrumGroups.GetTrackNumber).ToHashSet();
        var kick = song.Song.Notes!.Where(x => kicks.Contains(x.Key)).SelectMany(x => x.Value).Select(x => x.Position).ToArray();
        foreach (var start in song.Trace.Where(t => t.Point == TracePoints.EnsembleHits).SelectMany(t => (ImmutableArray<double>)t.Value!))
        {
            var span = song.Map.Sections.First(x => x.IsWithin(start));
            var end = Math.Min(span.End, start + span.Meter.BarDuration);
            // a short last bar's end the section's
            yield return (start, end, [..kick.Where(x => x >= start - 1e-9 && x < end - 1e-9).Distinct().Order()]);
        }
    }

    private static CorpusSong[] Songs(string genre, int count) =>
        TestCorpus.InParallel(Enumerable.Range(0, count), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre))));

    /// <summary>
    ///     A struck bar's band, its chords, pad, riff and rhythm guitar, strikes only where the kick does, before the bar's last
    ///     half, which the drums fill; of a genre's songs that stop, as a song strikes hits only where it stops at all.
    /// </summary>
    [Test]
    [Arguments("funk")]
    [Arguments("rock")]
    [Arguments("metal")]
    public async Task AStruckBarsBand_StrikesOnlyWithTheKick(string genre)
    {
        var bars = 0;
        var stopping = GenrePresets.With(genre, GenreChoices.Stops, ("songs", 1));
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: stopping))))
        foreach (var (start, end, kick) in Struck(song))
        {
            bars++;
            await Assert.That(kick.Length).IsGreaterThan(0).Because($"seed {song.Seed} at {start}");
            // its section's last bar, before another section
            var span = song.Map.Sections.First(x => x.IsWithin(start));
            await Assert.That(start).IsEqualTo(span.Bar(span.BarCount - 1).Start).Within(1e-6).Because($"seed {song.Seed} at {start}");
            await Assert.That(song.Map.Sections[^1]).IsNotEqualTo(span).Because($"seed {song.Seed} at {start}");
            foreach (var role in new[] { TrackRole.Chords, TrackRole.Pad, TrackRole.Riff, TrackRole.RiffTwin, TrackRole.Rhythm }.Where(x => !song.Absent.Contains(x)))
            foreach (var note in song.Song.Notes!.GetValueOrDefault(song.Track(role))?.Where(n => n.Position >= start - 1e-9 && n.Position < (start + end) / 2 - 1e-9) ?? [])
                await Assert.That(kick.Any(k => Math.Abs(k - note.Position) < 1e-6)).IsTrue().Because($"seed {song.Seed}, its {role} at {note.Position} in the bar struck at {start}");
        }

        await Assert.That(bars).IsGreaterThan(3);
    }

    /// <summary>The plainest of the songs strike no bar of hits, the songs' chance of them none at the plain end.</summary>
    [Test]
    public async Task ThePlainestSongs_StrikeNoBar()
    {
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Base: 0))))
            await Assert.That(Struck(song).Any()).IsFalse().Because($"seed {song.Seed}");
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var genre in new[] { "funk", "latin", "ska", "soul", "fusion", "jazz", "rock", "metal", "thrash", "punk", "pop" })
        {
            var songs = Songs(genre, 64);
            var struck = songs.SelectMany(x => Struck(x).ToArray()).ToArray();
            var sections = songs.Sum(x => x.Map.Sections.Length);
            Console.WriteLine($"{genre}: {struck.Length} bars struck in {songs.Count(x => Struck(x).Any())} of {songs.Length} songs, {struck.Length / (double)sections:P1} of the sections, " +
                              $"hits a bar {struck.Select(x => (double)x.Kick.Length).DefaultIfEmpty().Average():0.0}, from {struck.Select(x => x.Kick.Length).DefaultIfEmpty().Min()} to {struck.Select(x => x.Kick.Length).DefaultIfEmpty().Max()}");
        }

        await Task.CompletedTask;
    }
}
