using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>A line's strong beats on a tension (<see cref="LinePattern.Tensions" />): Djent's synth over the guitars against Metal's lines, which have none.</summary>
public sealed class TensionReportTest
{
    /// <summary>
    ///     The share of a part's notes on the beat outside the chord they sound over, the chords part's pitches between its
    ///     changes, and how many there were; none where the song has no such part.
    /// </summary>
    internal static (int Off, int OnBeat) OffChord(CorpusSong song, TrackRole role)
    {
        var part = song.Song.TrackDefinitions.FirstOrDefault(x => x.Value.Role == role && song.Song.Notes!.ContainsKey(x.Key)).Key;
        if (part == 0 || !song.Song.Notes!.TryGetValue(song.Song.Track(TrackRole.Chords), out var chordNotes))
            return (0, 0);
        var changes = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position).ToArray();
        int Change(double position) => Array.FindLastIndex(changes, x => x <= position + 1e-9);
        var classes = chordNotes.GroupBy(x => Change(x.Position)).ToDictionary(x => x.Key, x => x.SelectMany(n => n.Value.Pitches).Select(p => p % 12).ToHashSet());
        // on a beat of its bar, the note's own rank
        var onBeat = song.Song.Notes[part].Where(x => x.Value.State.GetStateValue(CompositionStateKinds.BeatRank) == 0 && classes.ContainsKey(Change(x.Position))).ToArray();
        return (onBeat.Count(x => !x.Value.Pitches.All(p => classes[Change(x.Position)].Contains(p % 12))), onBeat.Length);
    }

    /// <summary>
    ///     A calibration: Djent's counter-melody, its strong beats on a tension 40% of them at the middle, off its chord on
    ///     more of its beats than the same songs' without its tensions, 48% to 31% over these songs; Metal's lines, which have
    ///     none, land off their power chords as often as chance and their songs have it, 29% to 45% (Report).
    /// </summary>
    [Test]
    public async Task DjentsCounterMelody_LandsOffItsChord_OftenerThanWithoutItsTensions()
    {
        var djent = GenrePresets.Genre("djent");
        var plain = djent with
        {
            Choices = djent.Choices.SetItem(GenreChoices.Tension.Name, ImmutableDictionary<string, ByConvention>.Empty
                .Add(GenreChoices.Name(nameof(TrackRole.Melody)), new ByConvention(0, 0, 0)).Add(GenreChoices.Name(nameof(TrackRole.CounterMelody)), new ByConvention(0, 0, 0)))
        };
        double Share(Genre genre) =>
            TestCorpus.InParallel(Enumerable.Range(0, 64), seed => OffChord(TestCorpus.Get(seed, new SongOverrides(Genre: genre)), TrackRole.CounterMelody))
                .Aggregate((Off: 0, OnBeat: 0), (sum, x) => (sum.Off + x.Off, sum.OnBeat + x.OnBeat)) is var (off, all) ? off / (double)Math.Max(1, all) : 0;
        var (tense, without) = (Share(djent), Share(plain));
        Console.WriteLine($"Djent's counter-melody's notes on the beat off its chord: {tense:P0}, {without:P0} without its tensions");
        await Assert.That(tense).IsGreaterThan(without + 0.05);
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
