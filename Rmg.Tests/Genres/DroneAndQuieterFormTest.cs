using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>Black metal's riff on an open string's drone, and its driven guitar acoustic or clean between the storms.</summary>
public sealed class DroneAndQuieterFormTest
{
    private static Genre With(string preset, GenreChoice choice, string option)
    {
        var genre = GenrePresets.Genre(preset);
        var own = genre.Choices.GetValueOrDefault(choice.Name, ImmutableDictionary<string, ByConvention>.Empty);
        return genre with { Choices = genre.Choices.SetItem(choice.Name, own.SetItem(option, new ByConvention(1, 1, 1))) };
    }

    /// <summary>The share of a riff's weak notes, those off its beats, on the lowest note of their chord's root it plays, in the sections given.</summary>
    private static (int OnPedal, int Weak) Pedal(CorpusSong song, IReadOnlySet<int> sections)
    {
        var (onPedal, weak) = (0, 0);
        foreach (var (track, notes) in song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role == TrackRole.Riff))
        foreach (var span in song.Map.Sections.Where(x => sections.Contains(x.SectionId) && !song.IsSolo(x)))
        {
            var inSpan = notes.Where(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9 && x.Value.Pitches.Length == 1).ToArray();
            foreach (var note in inSpan.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.BeatRank) > 0))
            {
                weak++;
                var root = Rmg.Core.Composition.Realizer.GetChord(note.Value.State).Chord.GetPitch(0);
                var lowest = inSpan.Select(x => x.Value.Pitches[0]).Where(x => ((x - root) % 12 + 12) % 12 == 0).DefaultIfEmpty(int.MinValue).Min();
                onPedal += note.Value.Pitches[0] == lowest ? 1 : 0;
            }
        }

        return (onPedal, weak);
    }

    /// <summary>A droning section's riff plays most of its weak notes on its chord's root at the bottom, far more than one that does not drone.</summary>
    [Test]
    public async Task ADroningRiff_PlaysItsWeakNotesOnItsPedal()
    {
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: With("blackMetal", GenreChoices.Riff, "drone"))));
        var drones = songs.Select(x => Pedal(x, x.Trace.Where(t => t.Point == TracePoints.Drone && (bool)t.Value!).Select(t => t.Section).ToHashSet())).ToArray();
        var (onPedal, weak) = (drones.Sum(x => x.OnPedal), drones.Sum(x => x.Weak));
        Console.WriteLine($"droning: {onPedal} of {weak} weak notes on the pedal");
        await Assert.That(weak).IsGreaterThan(100);
        await Assert.That(onPedal / (double)weak).IsGreaterThan(0.5);
    }

    /// <summary>A part playing its quieter form plays a quieter guitar in every quieter appearance's notes that keep its own instrument.</summary>
    [Test]
    public async Task AQuieterForm_IsPlayedInTheQuieterAppearances()
    {
        var found = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: With("blackMetal", GenreChoices.LouderForms, "quieter")))))
        foreach (var entry in song.Trace.Where(t => t.Point == TracePoints.QuieterForm))
        {
            found++;
            var program = (int)entry.Value!;
            await Assert.That(LouderForms.QuieterForms.Values.Any(x => x.Contains(program))).IsTrue();
            await Assert.That(song.Song.Notes![entry.Track].Any(x => x.Value.State.GetStateValue(CompositionStateKinds.Program) == program + 1)).IsTrue().Because($"seed {song.Seed}");
        }

        await Assert.That(found).IsGreaterThan(4);
    }

    /// <summary>How often the presets drone and play their quieter forms.</summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var name in new[] { "blackMetal", "folk" })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(name))));
            var drones = songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.Drone)).Average(t => (bool)t.Value! ? 1.0 : 0);
            var pedal = songs.Select(x => Pedal(x, x.Trace.Where(t => t.Point == TracePoints.Drone && (bool)t.Value!).Select(t => t.Section).ToHashSet())).ToArray();
            var quieter = songs.Average(x => x.Trace.Any(t => t.Point == TracePoints.QuieterForm) ? 1.0 : 0);
            Console.WriteLine($"{name}: sections droning {drones:P0}, their riff's weak notes on the pedal {pedal.Sum(x => x.OnPedal) / (double)Math.Max(1, pedal.Sum(x => x.Weak)):P0}; songs with a quieter form {quieter:P0}");
        }

        await Task.CompletedTask;
    }
}
