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
    public async Task APolymetricPart_PlaysItsCycleCellByCellAcrossTheBars_FromEveryFramesStart_EachCellItsFirstsMotifCut()
    {
        var (checkedSections, composite) = (0, 0);
        foreach (var song in TestCorpus.Range(256).Where(x => !Plans(x).IsEmpty))
        {
            var plans = Plans(song);
            foreach (var span in song.Map.Sections)
            {
                if (!plans.TryGetValue(span.SectionId, out var plan) || !plan.Parts.Contains(TrackRole.Riff) || song.IsSolo(span))
                    continue;

                // a cycle that drifts against the bar, neither dividing it nor a whole number of them
                var bar = span.Meter.Groups.Sum();
                await Assert.That(bar % plan.Sixteenths != 0 && plan.Sixteenths % bar != 0).IsTrue().Because($"seed {song.Seed}, {string.Join("+", plan.Cells)} in {bar}");
                var frame = span.Meter.PatternDuration * plan.Patterns;
                for (var start = span.Start; start < span.End - 1e-9; start += frame)
                {
                    var length = Math.Min(frame, span.End - start);
                    var onsets = song.Song.Notes![SongTracks.RiffTrack].Select(x => Math.Round(x.Position - start, 6)).Where(x => x >= -1e-6 && x < length - 1e-6).ToArray();
                    // the cells in turn from the frame's start, and the motif as the longest plays it whole where it first does
                    var cells = new List<(double At, double Length)>();
                    for (var (cell, at) = (0, 0.0); at < length - 1e-9; at += cells[^1].Length)
                        cells.Add((at, plan.Cells[cell++ % plan.Cells.Length] * 0.25));
                    var longest = cells.FirstOrDefault(x => x.Length.Equals(plan.Cells.Max() * 0.25) && x.At + x.Length <= length + 1e-9);
                    if (longest == default && cells[0].Length < plan.Cells.Max() * 0.25)
                        continue;

                    var motif = onsets.Where(x => x >= longest.At - 1e-6 && x < longest.At + longest.Length - 1e-6).Select(x => Math.Round(x - longest.At, 6)).ToHashSet();
                    // every cell's notes the motif's, cut to its length
                    foreach (var (at, cellLength) in cells)
                        await Assert.That(onsets.Where(x => x >= at - 1e-6 && x < at + cellLength - 1e-6).All(x => motif.Contains(Math.Round(x - at, 6))))
                            .IsTrue().Because($"seed {song.Seed} at {start + at}");
                }

                checkedSections++;
                if (plan.Cells.Length > 1)
                    composite++;
            }
        }

        await Assert.That(checkedSections).IsGreaterThan(3);
        await Assert.That(composite).IsGreaterThan(0);
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
        Console.WriteLine($"  {plans.Length} polymeters: the kick with them {plans.Count(x => x.Kick) / (double)plans.Length:P0}, over 8 bars {plans.Count(x => x.Patterns == 2) / (double)plans.Length:P0}, " +
                          $"composite {plans.Count(x => x.Cells.Length > 1) / (double)plans.Length:P0}: {string.Join(", ", plans.Where(x => x.Cells.Length > 1).Select(x => string.Join("+", x.Cells)).GroupBy(x => x).OrderByDescending(x => x.Count()).Take(12).Select(x => $"{x.Key} {x.Count()}"))}");
        await Task.CompletedTask;
    }
}
