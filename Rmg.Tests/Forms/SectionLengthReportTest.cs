using Rmg.Core.Composition;

namespace Rmg.Tests.Forms;

/// <summary>How many times the sections play their patterns, by how conventional their rhythm is, and how long the songs are.</summary>
public sealed class SectionLengthReportTest
{
    [Test]
    public async Task EverySection_PlaysItsPatternOnceTwiceOrFourTimes_AsItsSpanIsLong()
    {
        foreach (var song in TestCorpus.Range(20))
        {
            var plays = song.Trace.Where(x => x.Point == TracePoints.SectionLength).ToDictionary(x => x.Section, x => (int)x.Value!);
            foreach (var span in song.Map.Sections)
                await Assert.That(span.Duration).IsEqualTo(plays[span.SectionId] * Meter.FourFour.PatternDuration).Within(1e-9);
        }
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Range(200).ToArray();
        var sections = songs.SelectMany(song =>
        {
            var rhythm = ((RhythmicUnconventionality)song.Trace.Single(x => x.Point == TracePoints.SongRhythm).Value!).Value;
            return song.Trace.Where(x => x.Point == TracePoints.SectionLength).Select(x => (Plays: (int)x.Value!, Rhythm: rhythm));
        }).ToArray();
        foreach (var band in sections.GroupBy(x => Math.Min(2, (int)(x.Rhythm * 3))).OrderBy(x => x.Key))
            Console.WriteLine($"rhythm {band.Key}/3: once {band.Count(x => x.Plays == 1) / (double)band.Count():P0}, twice {band.Count(x => x.Plays == 2) / (double)band.Count():P0}, " +
                              $"four times {band.Count(x => x.Plays == 4) / (double)band.Count():P0} of {band.Count()}");
        var bars = songs.Select(x => x.Map.Sections.Sum(s => s.Duration) / Meter.FourFour.BarDuration).Order().ToArray();
        Console.WriteLine($"the sections' bars a song: median {bars[bars.Length / 2]}, 10% {bars[bars.Length / 10]}, 90% {bars[bars.Length * 9 / 10]}");
        await Task.CompletedTask;
    }
}
