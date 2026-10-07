using Rmg.Core.Composition;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.Rendering;

/// <summary>A part's darker tone (<see cref="Expression.ToneChance" />): its brightness below its sound's own, the further the quieter its section.</summary>
public sealed class ToneTest
{
    private static IEnumerable<(int Track, TrackRole Role, double Position, RealizedNote Note)> Toned(CorpusSong song) =>
        song.Song.Notes!.SelectMany(x => x.Value.Where(n => n.Value.State.GetStateValue(CompositionStateKinds.Tone) != 0)
            .Select(n => (x.Key, song.Song.TrackDefinitions[x.Key].Role, n.Position, n.Value)));

    [Test]
    public async Task ATone_IsNeverADrums_AndWithinItsReach()
    {
        var toned = 0;
        foreach (var song in TestCorpus.Range(64))
        foreach (var (_, role, _, note) in Toned(song))
        {
            toned++;
            await Assert.That(role).IsNotEqualTo(TrackRole.Drum);
            await Assert.That(note.State.GetStateValue(CompositionStateKinds.Tone)).IsBetween(1, 16);
        }

        await Assert.That(toned).IsGreaterThan(100);
    }

    [Test]
    public async Task APartsBrightness_NeverFallsBelowTheFiltersReach_AndADrumsNeverMoves()
    {
        foreach (var song in TestCorpus.Range(64))
        foreach (var track in song.Rendered.Tracks)
        {
            var values = track.Controllers.Where(c => c.Controller == ExpressionRender.BrightnessController).Select(c => c.Value).ToArray();
            if (track.IsPercussionInstrument)
                await Assert.That(values).IsEmpty();
            await Assert.That(values.All(x => x is >= 40 and <= 64)).IsTrue().Because($"seed {song.Seed}");
        }
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var (end, given) in new (string, double?)[] { ("plainest fifth", 0.1), ("middling", null), ("wildest fifth", 0.9) })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Base: given)));
            var parts = songs.SelectMany(x => x.Song.Notes!.Where(n => x.Song.TrackDefinitions[n.Key].Role != TrackRole.Drum && n.Value.Count > 0)
                .Select(n => (Song: x, Role: x.Song.TrackDefinitions[n.Key].Role, Tones: n.Value.Select(v => v.Value.State.GetStateValue(CompositionStateKinds.Tone)).ToArray()))).ToArray();
            var toned = parts.Where(x => x.Tones.Any(t => t > 0)).ToArray();
            // a toned part's tone by its section's energy, its quieter half against its louder
            var bySection = songs.SelectMany(song =>
            {
                var energy = song.Trace.Where(x => x.Point == TracePoints.SectionEnergy).GroupBy(x => x.Section).ToDictionary(x => x.Key, x => ((SectionEnergyTrace)x.First().Value!).Energy);
                return song.Map.Sections.Where(x => energy.ContainsKey(x.SectionId)).SelectMany(span => Toned(song)
                    .Where(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9)
                    .Select(x => (Energy: energy[span.SectionId], Tone: x.Note.State.GetStateValue(CompositionStateKinds.Tone))));
            }).ToArray();
            var middle = bySection.Select(x => x.Energy).DefaultIfEmpty().Order().ElementAtOrDefault(bySection.Length / 2);
            Console.WriteLine($"{end}: songs toned {songs.Count(x => Toned(x).Any()) / (double)songs.Length:P0}, parts {toned.Length / (double)parts.Length:P0} " +
                              $"({string.Join(", ", toned.GroupBy(x => x.Role).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count()}"))}); " +
                              $"tone {toned.SelectMany(x => x.Tones.Where(t => t > 0)).DefaultIfEmpty().Average():F1} steps, quieter sections {bySection.Where(x => x.Energy < middle).Select(x => x.Tone).DefaultIfEmpty().Average():F1}, louder {bySection.Where(x => x.Energy >= middle).Select(x => x.Tone).DefaultIfEmpty().Average():F1}");
        }

        await Task.CompletedTask;
    }
}
