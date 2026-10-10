using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>Devices only a genre plays, each forced on the same songs as without: a squealing pinch, the chords struck as stabs, a part's level.</summary>
public sealed class GenreOnlyDevicesTest
{
    private static Genre With(string preset, GenreChoice choice, ImmutableDictionary<string, ByConvention> options)
    {
        var genre = GenrePresets.Genre(preset);
        return genre with { Choices = genre.Choices.SetItem(choice.Name, options) };
    }

    private static ImmutableDictionary<string, ByConvention> Only(string option, double amount = 1) =>
        ImmutableDictionary<string, ByConvention>.Empty.Add(option, new ByConvention(amount, amount, amount));

    /// <summary>A squealing pinch plays on the part's own program, 19 or 24 semitones over the note a harmonic's pinch plays.</summary>
    [Test]
    public async Task ASquealingPinch_SoundsOnThePartsOwnProgram_ATwelfthOrTwoOctavesUp()
    {
        var squeals = 0;
        foreach (var seed in Enumerable.Range(0, 32))
        {
            var squealing = TestCorpus.Get(seed, new SongOverrides(Genre: With("metal", GenreChoices.PinchSound, Only(GenreChoices.Name(nameof(PinchSound.Squeal))))));
            var harmonics = TestCorpus.Get(seed, new SongOverrides(Genre: With("metal", GenreChoices.PinchSound, Only(GenreChoices.Name(nameof(PinchSound.Harmonics))))));
            foreach (var (track, notes) in squealing.Song.Notes!)
            {
                var plain = harmonics.Song.Notes![track].Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Pinch) == 1).ToDictionary(x => Math.Round(x.Position, 6), x => x.Value.Pitches[0]);
                foreach (var note in notes.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Pinch) == 1))
                {
                    squeals++;
                    var because = $"seed {seed}, track {track} at {note.Position}";
                    await Assert.That(note.Value.State.GetStateValue(CompositionStateKinds.Program)).IsNotEqualTo(Expression.GuitarHarmonics + 1).Because(because);
                    await Assert.That(note.Value.Pitches[0] - plain[Math.Round(note.Position, 6)]).IsIn(19, 24).Because(because);
                }
            }
        }

        await Assert.That(squeals).IsGreaterThan(0);
    }

    /// <summary>The chords struck as stabs: every note of the parts that hold chords a 16th at most before the ending, the pad's as it was.</summary>
    [Test]
    public async Task Stabs_StrikeTheChordsShort()
    {
        var stabbed = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: With("reggae", GenreChoices.Articulation, Only("stabs"))))))
        foreach (var (track, notes) in song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role is TrackRole.Chords or TrackRole.Rhythm))
        foreach (var note in notes.Where(x => x.Position < song.Map.Ending.Start - 1e-9))
        {
            stabbed++;
            await Assert.That(note.Value.Duration).IsLessThanOrEqualTo(Articulations.StabLength + 1e-9).Because($"seed {song.Seed}, track {track} at {note.Position}");
        }

        await Assert.That(stabbed).IsGreaterThan(100);
    }

    /// <summary>A part leveled up plays louder than as the songs have it, over the same songs.</summary>
    [Test]
    public async Task APartLeveledUp_PlaysLouder()
    {
        double Loudness(double level) => TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: With("pop", GenreChoices.Levels, Only("melody", level)))))
            .SelectMany(x => x.Song.Notes![x.Song.Track(TrackRole.Melody)]).Average(x => (double)x.Value.Velocity);
        var (up, own) = (Loudness(1), Loudness(0.5));
        Console.WriteLine($"the melody's velocity {up:F3} leveled up, {own:F3} as the songs have it");
        await Assert.That(up).IsGreaterThan(own);
    }
}
