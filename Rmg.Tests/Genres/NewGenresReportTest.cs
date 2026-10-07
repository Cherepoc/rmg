using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>Black metal and IDM against their sound, by how plain or wild the songs are.</summary>
public sealed class NewGenresReportTest
{
    private const int Count = 64;

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var name in new[] { "blackMetal", "idm" })
        foreach (var (end, given) in new (string, double?)[] { ("plainest fifth", 0.1), ("middling", null), ("wildest fifth", 0.9) })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, Count), seed => TestCorpus.Get(seed, new SongOverrides(Base: given, Genre: GenrePresets.Genre(name))));
            var figures = songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.Figures).Select(t => (ImmutableDictionary<FigurePlayer, string>)t.Value!)).ToArray();
            string Figure(FigurePlayer player, string figure) => $"{figure} {figures.Count(x => x.GetValueOrDefault(player) == figure) / (double)figures.Length:P0}";
            var tempos = songs.Select(x => x.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(x.Map.Sections[0].Start).GetStateValue(Rmg.Core.Events.StateKinds.Tempo)).ToArray();
            var meters = songs.Select(x => (Meter)x.Trace.Single(t => t.Point == TracePoints.Meter).Value!).ToArray();
            var chords = songs.SelectMany(x => x.Song.Notes!.GetValueOrDefault(SongTracks.ChordsTrack)?.Where(n => n.Value.Pitches.Length >= 3) ?? []).ToArray();
            var effects = songs.Select(x => ((SongEffects)x.Trace.Single(t => t.Point == TracePoints.SongEffects).Value!).Strong).ToArray();
            // the guitars' notes in pinch harmonics, General MIDI's 31, those shorter than an 8th apart
            var guitarNotes = songs.SelectMany(x => x.Song.Notes!.Where(n => x.Song.TrackDefinitions[n.Key] is PitchInstrumentTrack { InstrumentCode: 29 or 30 }).SelectMany(n => n.Value)).ToArray();
            var harmonics = guitarNotes.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Program) == 32).ToArray();
            Console.WriteLine($"{name}, {end}: of {guitarNotes.Length} driven guitar notes {harmonics.Length} in harmonics, {harmonics.Count(x => x.Value.Duration < 0.5 - 1e-9)} of them shorter than an 8th");
            Console.WriteLine($"{name}, {end}: tempo {tempos.Average():0}, in 4/4 {meters.Count(m => m.Sixteenths == 16) / (double)meters.Length:P0}, swung {songs.Average(x => x.Swing.Delay > 0 ? 1.0 : 0):P0}; " +
                              $"sections {Figure(FigurePlayer.Riff, "tremolo")}, {Figure(FigurePlayer.Ground, "blast")}, {Figure(FigurePlayer.Ground, "doubleKick")}, {Figure(FigurePlayer.Ground, "breakbeat")}; " +
                              $"chords of four notes or more {chords.Count(c => c.Value.Pitches.Select(p => p % 12).Distinct().Count() >= 4) / (double)Math.Max(1, chords.Length):P0}; " +
                              $"strong effects {string.Join(", ", effects.GroupBy(x => x).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count()}"))}");
        }

        await Task.CompletedTask;
    }
}
