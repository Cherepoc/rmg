using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Grooves;

/// <summary>A snare's ghost notes: soft 16ths off the beat around its backbeat.</summary>
public sealed class GhostsTest
{
    private static Genre With(string preset, double chance)
    {
        var genre = GenrePresets.Genre(preset);
        return genre with { Choices = genre.Choices.SetItem(GenreChoices.Ghosts.Name, ImmutableDictionary<string, ByConvention>.Empty.Add("sections", new ByConvention(chance, chance, chance))) };
    }

    /// <summary>The snare's notes a song ghosting adds to the song as it is without, which they leave as it was.</summary>
    private static (double Position, double Velocity, double Loudest)[] Added(CorpusSong with, CorpusSong without)
    {
        var snares = new[] { DrumDefinitions.AcousticSnare, DrumDefinitions.ElectricSnare }.Select(DrumGroups.GetTrackNumber).ToHashSet();
        var added = new List<(double, double, double)>();
        foreach (var (track, notes) in with.Song.Notes!.Where(x => snares.Contains(x.Key)))
        {
            var before = without.Song.Notes!.GetValueOrDefault(track)?.Select(x => Math.Round(x.Position, 6)).ToHashSet() ?? [];
            foreach (var note in notes.Where(x => !before.Contains(Math.Round(x.Position, 6))))
            {
                var bar = with.Map.SectionAt(note.Position)!;
                var start = bar.Start + Math.Floor((note.Position - bar.Start + 1e-6) / bar.Meter.BarDuration) * bar.Meter.BarDuration;
                added.Add((note.Position - start, note.Value.Velocity,
                    notes.Where(x => x.Position >= start - 1e-9 && x.Position < start + bar.Meter.BarDuration - 1e-9 && before.Contains(Math.Round(x.Position, 6))).Select(x => x.Value.Velocity).DefaultIfEmpty(double.NaN).Max()));
            }
        }

        return [..added];
    }

    /// <summary>A ghost note falls on a 16th off the beat, as heard softer than its bar's backbeat by far, or as soft as a note is.</summary>
    [Test]
    public async Task AGhostNote_IsASoft16thOffTheBeat()
    {
        var count = 0;
        foreach (var seed in Enumerable.Range(0, 16))
        {
            var (with, without) = (TestCorpus.Get(seed, new SongOverrides(Genre: With("funk", 1))), TestCorpus.Get(seed, new SongOverrides(Genre: With("funk", 0))));
            foreach (var (position, velocity, loudest) in Added(with, without))
            {
                count++;
                await Assert.That(Math.Abs(position / Ghosts.Length - Math.Round(position / Ghosts.Length))).IsLessThan(1e-6).Because($"seed {seed} at {position}");
                await Assert.That(Math.Abs(position - Math.Round(position))).IsGreaterThan(1e-6).Because($"seed {seed} at {position}");
                await Assert.That(Rmg.Core.Rendering.Render.ToMidiVelocity(velocity)).IsLessThanOrEqualTo(Math.Max(Rmg.Core.Rendering.Render.ToMidiVelocity(loudest) * Ghosts.Loudness, 0.2) + 1e-3)
                    .Because($"seed {seed} at {position}");
            }
        }

        await Assert.That(count).IsGreaterThan(100);
    }

    /// <summary>How many ghost notes a bar the funky presets play, of their bars with a backbeat.</summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var name in new[] { "funk", "soul", "rock", "blues", "hipHop", "disco" })
        {
            var pairs = TestCorpus.InParallel(Enumerable.Range(0, 32), seed => (TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(name))), TestCorpus.Get(seed, new SongOverrides(Genre: With(name, 0)))));
            var ghosting = pairs.Select(x => x.Item1.Trace.Where(t => t.Point == TracePoints.Ghosts).Average(t => (bool)t.Value! ? 1.0 : 0)).Average();
            var added = pairs.Sum(x => Added(x.Item1, x.Item2).Length);
            var bars = pairs.Sum(x => x.Item1.Map.Sections.Sum(s => s.Duration / s.Meter.BarDuration));
            // as rendered, in MIDI's velocities: the ghosts' against the snare's other notes
            var snares = new[] { DrumDefinitions.AcousticSnare, DrumDefinitions.ElectricSnare }.Select(DrumGroups.GetTrackNumber).ToHashSet();
            var (ghosts, others) = (new List<double>(), new List<double>());
            foreach (var (with, without) in pairs)
            {
                var before = without.Song.Notes!.Where(x => snares.Contains(x.Key)).SelectMany(x => x.Value).Select(x => Math.Round(x.Position, 6)).ToHashSet();
                foreach (var note in with.Rendered.Tracks.Where(x => x.IsPercussionInstrument).SelectMany(x => x.NoteTimeline)
                             .Where(x => with.Song.Notes!.Where(n => snares.Contains(n.Key)).Any(n => n.Value.Any(m => Math.Abs(m.Position - x.Position) < 1e-6 && m.Value.Pitches.Contains(x.Value.Offset)))))
                    (before.Contains(Math.Round(note.Position, 6)) ? others : ghosts).Add(note.Value.Velocity * 127);
            }

            Console.WriteLine($"{name}: sections ghosting {ghosting:P0}, ghost notes {added / bars:0.00} a bar of the songs', as loud as {ghosts.DefaultIfEmpty(0).Average():0} " +
                              $"against the snare's other notes' {others.DefaultIfEmpty(0).Average():0}");
        }

        await Task.CompletedTask;
    }
}
