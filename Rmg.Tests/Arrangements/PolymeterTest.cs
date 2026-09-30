using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.Arrangements;

public sealed class PolymeterTest
{
    private static ImmutableDictionary<int, Polymeter.Plan> Plans(CorpusSong song) =>
        (ImmutableDictionary<int, Polymeter.Plan>)song.Trace.Single(x => x.Point == TracePoints.Polymeter).Value!;

    [Test]
    public async Task APolymetricPart_RepeatsItsFigureAcrossTheBars_FromEveryPatternsStart()
    {
        var checkedSections = 0;
        foreach (var song in TestCorpus.Range(256).Where(x => !Plans(x).IsEmpty))
        {
            var plans = Plans(song);
            foreach (var (span, index) in song.Map.Sections.Select((x, i) => (x, i)))
            {
                if (!plans.TryGetValue(span.SectionId, out var plan) || !plan.Parts.Contains(TrackRole.Riff) || song.IsSolo(span))
                    continue;

                await Assert.That(span.Meter.Groups.Sum() % plan.Sixteenths).IsNotEqualTo(0);
                var figure = plan.Sixteenths * 0.25;
                for (var start = span.Start; start < span.End - 1e-9; start += span.Meter.PatternDuration)
                {
                    var onsets = song.Song.Notes![SongTracks.RiffTrack].Select(x => x.Position - start).Where(x => x >= -1e-9 && x < span.Meter.PatternDuration - 1e-9).ToArray();
                    var first = onsets.Where(x => x < figure - 1e-9).Select(x => Math.Round(x, 6)).ToHashSet();
                    await Assert.That(onsets.All(x => first.Contains(Math.Round(x % figure, 6)))).IsTrue().Because($"seed {song.Seed} at {start}");
                }

                checkedSections++;
            }
        }

        await Assert.That(checkedSections).IsGreaterThan(3);
    }

    [Test]
    public async Task ThePlainestSongs_PlayNoPolymeter()
    {
        foreach (var seed in Enumerable.Range(0, 8))
            await Assert.That(Plans(TestCorpus.Get(seed, new SongOverrides(Base: 0)))).IsEmpty();
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var sections = TestCorpus.Measure(1024, song =>
        {
            var plans = Plans(song);
            return song.Trace.Where(x => x.Point == TracePoints.SectionUnconventionality).DistinctBy(x => x.Section)
                .Select(x => (Band: Math.Clamp((int)(((Unconventionality)x.Value!)[Facet.Feel] * 5), 0, 4), Plan: plans.GetValueOrDefault(x.Section)))
                .ToArray();
        }).SelectMany(x => x).ToArray();

        foreach (var band in sections.GroupBy(x => x.Band).OrderBy(x => x.Key))
            Console.WriteLine($"  feel {band.Key}/5, {band.Count()} sections: polymeter {band.Count(x => x.Plan is not null) / (double)band.Count():P1}");
        var plans = sections.Select(x => x.Plan).OfType<Polymeter.Plan>().ToArray();
        Console.WriteLine($"  lengths {string.Join(", ", plans.GroupBy(x => x.Sixteenths).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count()}"))}; " +
                          $"parts {string.Join(", ", plans.GroupBy(x => string.Join("+", x.Parts) + (x.Kick ? "+kick" : "")).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count()}"))}");
        await Task.CompletedTask;
    }
}
