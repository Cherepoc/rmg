using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>A song's intensity moving its sections' energy, where its energy follows it (<see cref="SectionEnergy.GenerateMood" />).</summary>
public sealed class MoodEnergyTest
{
    /// <summary>A genre whose songs' energy never follows their intensity.</summary>
    private static Genre Not(Genre genre) =>
        genre with { Choices = genre.Choices.SetItem(GenreChoices.MoodEnergy.Name, ImmutableDictionary<string, ByConvention>.Empty.Add("energy", new ByConvention(0, 0, 0))) };

    private static CorpusSong[] Songs(Genre genre, double intensity, int count) =>
        [..TestCorpus.InParallel(Enumerable.Range(0, count), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre, Intensity: intensity)))];

    /// <summary>
    ///     A rule: a song whose energy follows its intensity has every section's energy moved by it, the intensity times the
    ///     step, the same seed's sections otherwise as they are where it does not, which moves none; every Trance song's
    ///     follows it, and a song of no genre's either follows it or moves none.
    /// </summary>
    [Test]
    [Arguments("trance", 1.0)]
    [Arguments("trance", -1.0)]
    [Arguments(null, 1.0)]
    [Arguments(null, -1.0)]
    public async Task ASongsIntensity_MovesEverySectionsEnergy_WhereItsEnergyFollowsIt(string? id, double intensity)
    {
        var genre = id is null ? Genre.None : GenrePresets.Genre(id);
        var not = Songs(Not(genre), intensity, 32).ToDictionary(x => x.Seed);
        foreach (var song in Songs(genre, intensity, 32))
        {
            var because = $"seed {song.Seed}";
            var step = (double)song.Trace.Single(x => x.Point == TracePoints.MoodEnergy).Value!;
            if (id is null)
                await Assert.That(step == 0 || step == intensity * SectionEnergy.MoodStep).IsTrue().Because(because);
            else
                await Assert.That(step).IsEqualTo(intensity * SectionEnergy.MoodStep).Because(because);
            await Assert.That((double)not[song.Seed].Trace.Single(x => x.Point == TracePoints.MoodEnergy).Value!).IsEqualTo(0).Because(because);
            var own = not[song.Seed].Trace.Where(x => x.Point == TracePoints.SectionEnergy).ToDictionary(x => x.Section, x => ((SectionEnergyTrace)x.Value!).Energy);
            foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.SectionEnergy))
                await Assert.That(((SectionEnergyTrace)entry.Value!).Energy - own[entry.Section]).IsEqualTo(step).Within(1e-9).Because($"{because}, section {entry.Section}");
        }
    }

    /// <summary>
    ///     A genre's songs at intensity -1, 0 and 1, their energy following it and not: the tempo, the pitched parts' notes a
    ///     second, every note's velocity and the melody's, the drums' notes a bar, and the share of the sections played calm and heavy, so that
    ///     what the intensity moves through the energy shows beside what it moved before; the songs of no genre where none.
    /// </summary>
    [Test]
    [Explicit]
    [Arguments(null)]
    [Arguments("trance")]
    [Arguments("ambient")]
    [Arguments("metal")]
    [Arguments("jazz")]
    [Arguments("pop")]
    public async Task Report(string? id)
    {
        var genre = id is null ? Genre.None : GenrePresets.Genre(id);
        const int count = 64;
        static double Mean(IEnumerable<double> values) => values.Where(double.IsFinite).DefaultIfEmpty(double.NaN).Average();
        Console.WriteLine($"{"",-14}{"intensity",10}{"energy",8}{"tempo",8}{"notes/s",9}{"velocity",10}{"melody",8}{"drums/bar",11}{"calm",7}{"heavy",7}");
        foreach (var (name, each) in new[] { ($"{id ?? "songs"}", genre), ("not", Not(genre)) })
            foreach (var intensity in new[] { -1.0, 0, 1 })
            {
                var songs = Songs(each, intensity, count);
                var forms = songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.SectionForm).Select(t => (SectionFormKind)t.Value!)).ToArray();
                Console.WriteLine($"{name,-14}{intensity,10:0}" +
                                  $"{Mean(songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.SectionEnergy).Select(t => ((SectionEnergyTrace)t.Value!).Energy))),8:0.00}" +
                                  $"{Mean(songs.Select(x => x.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(Rmg.Core.Events.StateKinds.Tempo).GetEffectiveValueAt(0) * Meter.BaseTempo)),8:0}" +
                                  $"{Mean(songs.Select(x => x.Song.Notes!.Where(n => x.Song.TrackDefinitions[n.Key] is PitchInstrumentTrack).Sum(n => n.Value.Count) / x.Rendered.Seconds)),9:0.0}" +
                                  $"{Mean(songs.SelectMany(x => x.Rendered.Tracks.SelectMany(t => t.NoteTimeline).Select(n => n.Value.Velocity))),10:0.000}" +
                                  $"{Mean(songs.SelectMany(x => x.Song.Notes!.GetValueOrDefault(x.Track(TrackRole.Melody))?.Select(n => n.Value.Velocity) ?? [])),8:0.000}" +
                                  $"{Mean(songs.Select(x => x.Drums.Length / (x.Rendered.Duration / x.Map.Meter.BarDuration))),11:0.0}" +
                                  $"{forms.Count(x => x == SectionFormKind.Calm) / (double)forms.Length,7:P0}{forms.Count(x => x == SectionFormKind.Heavy) / (double)forms.Length,7:P0}");
            }

        await Task.CompletedTask;
    }
}
