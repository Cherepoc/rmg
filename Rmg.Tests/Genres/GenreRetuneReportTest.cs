using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>
///     What a genre's retune moves, measured on 64 of its songs from the trace: how long and fast its songs are, which
///     parts play, and how often its sections take each device a retune sets.
/// </summary>
public sealed class GenreRetuneReportTest
{
    [Test]
    [Explicit]
    [MethodDataSource(nameof(Genres))]
    public async Task Report(string genre)
    {
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre))));
        var seconds = songs.Select(x => x.Rendered.Seconds).Order().ToArray();
        var tempos = songs.Select(x => x.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.Tempo).GetEffectiveValueAt(0) * Meter.BaseTempo).Order().ToArray();
        double Pct(double part, double whole) => whole == 0 ? 0 : 100 * part / whole;
        var sections = songs.SelectMany(s => s.Map.Sections.Select(x => (Song: s, Span: x))).ToArray();
        int SectionsWith(string point, Func<object?, bool> holds) => songs.Sum(s => s.Map.Sections.Count(x => s.Trace.Any(t => t.Point == point && t.Section == x.SectionId && holds(t.Value))));
        int SongsWith(string point, Func<object?, bool> holds) => songs.Count(s => s.Trace.Any(t => t.Point == point && holds(t.Value)));
        string Figure(FigurePlayer player, string name) => $"{Pct(SectionsWith(TracePoints.Figures, v => ((ImmutableDictionary<FigurePlayer, string>)v!).GetValueOrDefault(player) == name), sections.Length):0}%";
        // the four commonest scales, as shares of every section's scale
        var allScales = songs.SelectMany(s => s.Trace.Where(t => t.Point == TracePoints.SectionScale).Select(t => ((Scale)t.Value!).Name)).ToArray();
        var scales = allScales.GroupBy(x => x).OrderByDescending(x => x.Count()).Take(4).ToArray();
        var scaleTotal = Math.Max(1, allScales.Length);
        Console.WriteLine($"{genre}: length {seconds[6]:0}-{seconds[32]:0}-{seconds[57]:0} s, tempo {tempos[6]:0}-{tempos[32]:0}-{tempos[57]:0}; " +
                          $"parts: riff twin {Pct(songs.Count(x => !x.Absent.Contains(TrackRole.RiffTwin)), 64):0}%, counter-melody {Pct(songs.Count(x => !x.Absent.Contains(TrackRole.CounterMelody)), 64):0}%, solo part {Pct(songs.Count(x => !x.Absent.Contains(TrackRole.Solo)), 64):0}%, drums {Pct(songs.Count(x => !x.Absent.Contains(TrackRole.Drum)), 64):0}%; " +
                          $"sections: solos {Pct(sections.Count(x => x.Song.IsSolo(x.Span)), sections.Length):0}%, double time {Pct(SectionsWith(TracePoints.TimeFeel, v => (int)v! == -1), sections.Length):0}%, half time {Pct(SectionsWith(TracePoints.TimeFeel, v => (int)v! == 1), sections.Length):0}%, " +
                          $"calm {Pct(SectionsWith(TracePoints.SectionForm, v => (SectionFormKind)v! == SectionFormKind.Calm), sections.Length):0}%, heavy {Pct(SectionsWith(TracePoints.SectionForm, v => (SectionFormKind)v! == SectionFormKind.Heavy), sections.Length):0}%, ghosts {Pct(SectionsWith(TracePoints.Ghosts, v => (bool)v!), sections.Length):0}%, " +
                          $"gallop {Figure(FigurePlayer.Riff, "gallop")}, riff tremolo-picked {Pct(songs.Sum(s => s.Trace.Count(t => t.Point == TracePoints.Tremolo && s.Song.TrackDefinitions[t.Track].Role == TrackRole.Riff)), sections.Length):0}%, double kick {Figure(FigurePlayer.Ground, "doubleKick")}, home off the tonic {Pct(SectionsWith(TracePoints.SectionHome, v => (int)v! != 0), sections.Length):0}%; " +
                          $"songs: a section's own tempo {Pct(SongsWith(TracePoints.SectionTempo, v => ((ImmutableArray<double>)v!).Any(x => Math.Abs(x - 1) > 1e-6)), 64):0}%, key changes {Pct(SongsWith(TracePoints.KeyChange, v => !((ImmutableArray<KeyChange>)v!).IsEmpty), 64):0}%, last repeat {Pct(SongsWith(TracePoints.LastRepeat, v => (bool)v!), 64):0}%; " +
                          $"scales {string.Join(" ", scales.Select(x => $"{x.Key} {Pct(x.Count(), scaleTotal):0}%"))}");
        await Task.CompletedTask;
    }

    public static IEnumerable<string> Genres() => (Environment.GetEnvironmentVariable("RMG_GENRES") ?? "metal").Split(',');
}
