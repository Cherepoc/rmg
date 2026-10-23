using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>How Surf rock's songs play: their tempo, their scales, their riffs tremolo-picked, the tremolo, the guitars' reverb and the dip.</summary>
public sealed class SurfReportTest
{
    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("surf"))));
        var tempos = songs.Select(x => Meter.BeatsPerSecond(x.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.Tempo).GetEffectiveValueAt(x.Map.Sections[0].Start)) * 60).ToArray();
        var scales = songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.SectionScale).Select(t => ((Scale)t.Value!).Name)).GroupBy(x => x).OrderByDescending(x => x.Count()).ToArray();
        var figures = songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.Figures).Select(t => (ImmutableDictionary<FigurePlayer, string>)t.Value!)).ToArray();
        var tremoloSongs = songs.Count(x => ((SongEffects)x.Trace.Single(t => t.Point == TracePoints.SongEffects).Value!).Strong == StrongEffect.Tremolo);
        var guitars = songs.SelectMany(x => x.Song.Notes!.Where(n => x.Song.TrackDefinitions[n.Key].Role is TrackRole.Melody or TrackRole.Riff).SelectMany(n => n.Value)).ToArray();
        var dips = songs.Count(x => x.Song.Notes!.Values.SelectMany(n => n).Any(n => n.Value.State.GetStateValue(CompositionStateKinds.WideVibrato) < 0));
        Console.WriteLine($"  tempo {tempos.Average():F0} a minute, from {tempos.Min():F0} to {tempos.Max():F0}; scales {string.Join(", ", scales.Select(x => $"{x.Key} {x.Count() / (double)scales.Sum(g => g.Count()):P0}"))}");
        Console.WriteLine($"  sections whose riff is tremolo-picked {figures.Count(x => x.GetValueOrDefault(FigurePlayer.Riff) == "tremolo") / (double)Math.Max(1, figures.Length):P0}; " +
                          $"songs of the tremolo {tremoloSongs / 64.0:P0}, of the whammy's dip {dips / 64.0:P0}; the guitars' reverb send {guitars.Average(x => x.Value.State.GetStateValue(CompositionStateKinds.Reverb) - 1):F0}");
        await Task.CompletedTask;
    }
}
