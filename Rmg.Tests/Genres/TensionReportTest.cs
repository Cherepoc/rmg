using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>A line's strong beats on a tension (<see cref="LinePattern.Tensions" />): Djent's synth over the guitars against Metal's lines, which have none.</summary>
public sealed class TensionReportTest
{
    /// <summary>
    ///     The share of a part's notes on the beat outside the chord they sound over, the chords part's pitches between its
    ///     changes, and how many there were; none where the song has no such part.
    /// </summary>
    private static (int Off, int OnBeat) OffChord(CorpusSong song, TrackRole role)
    {
        var part = song.Song.TrackDefinitions.FirstOrDefault(x => x.Value.Role == role && song.Song.Notes!.ContainsKey(x.Key)).Key;
        if (part == 0 || !song.Song.Notes!.TryGetValue(SongTracks.ChordsTrack, out var chordNotes))
            return (0, 0);
        var changes = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position).ToArray();
        int Change(double position) => Array.FindLastIndex(changes, x => x <= position + 1e-9);
        var classes = chordNotes.GroupBy(x => Change(x.Position)).ToDictionary(x => x.Key, x => x.SelectMany(n => n.Value.Pitches).Select(p => p % 12).ToHashSet());
        var onBeat = song.Song.Notes[part].Where(x => Math.Abs(x.Position - Math.Round(x.Position)) < 1e-9 && classes.ContainsKey(Change(x.Position))).ToArray();
        return (onBeat.Count(x => !x.Value.Pitches.All(p => classes[Change(x.Position)].Contains(p % 12))), onBeat.Length);
    }

    /// <summary>
    ///     A calibration: Djent's counter-melody, its strong beats on a tension 40% of them at the middle, off its chord far
    ///     more than Metal's, 58% of its notes on the beat to 22%, and 37% without its tensions (Report, October 2026).
    /// </summary>
    [Test]
    public async Task DjentsCounterMelody_LandsOffItsChordMoreThanMetals()
    {
        double Share(string genre) =>
            TestCorpus.InParallel(Enumerable.Range(0, 32), seed => OffChord(TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre))), TrackRole.CounterMelody))
                .Aggregate((Off: 0, OnBeat: 0), (sum, x) => (sum.Off + x.Off, sum.OnBeat + x.OnBeat)) is var (off, all) ? off / (double)Math.Max(1, all) : 0;
        var (djent, metal) = (Share("djent"), Share("metal"));
        Console.WriteLine($"the counter-melody's notes on the beat off its chord: Djent's {djent:P0}, Metal's {metal:P0}");
        await Assert.That(djent).IsGreaterThan(metal + 0.25);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var genre in new[] { "djent", "metal", "nuMetal" })
        foreach (var role in new[] { TrackRole.Melody, TrackRole.CounterMelody })
        {
            var counts = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => OffChord(TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre))), role));
            var (off, all) = (counts.Sum(x => x.Off), counts.Sum(x => x.OnBeat));
            Console.WriteLine($"{genre}, {role}: of {all} notes on the beat {off / (double)Math.Max(1, all):P0} off the chord, in {counts.Count(x => x.OnBeat > 0)} songs");
        }

        await Task.CompletedTask;
    }
}
