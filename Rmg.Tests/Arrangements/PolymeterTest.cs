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
                var frame = (plan.Patterns == Polymeter.Plan.Section ? span.Duration : span.Meter.PatternDuration * plan.Patterns);
                for (var start = span.Start; start < span.End - 1e-9; start += frame)
                {
                    var length = Math.Min(frame, span.End - start);
                    var onsets = song.Song.Notes![SongTracks.RiffTrack].Select(x => Math.Round(x.Position - start, 6)).Where(x => x >= -1e-6 && x < length - 1e-6).ToArray();
                    // the cells in turn from the frame's start, and the motif as the longest plays it whole where it first does
                    var cells = new List<(double At, double Length)>();
                    for (var (cell, at) = (0, 0.0); at < length - 1e-9; at += cells[^1].Length)
                        cells.Add((at, plan.CellAt(cell++) * 0.25));
                    var longest = cells.FirstOrDefault(x => x.Length.Equals(plan.Longest * 0.25) && x.At + x.Length <= length + 1e-9);
                    if (longest == default && cells[0].Length < plan.Longest * 0.25)
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
    public async Task ThePolymetersChords_JoinItsCycle_ByTheirRhythm_TheLongerItsCellsTheOftener()
    {
        var (joined, plans, longJoined, longPlans) = (0, 0, 0, 0);
        foreach (var song in TestCorpus.Range(512).Where(x => !Plans(x).IsEmpty))
        foreach (var span in song.Map.Sections)
        {
            if (!Plans(song).TryGetValue(span.SectionId, out var plan) || song.IsSolo(span))
                continue;

            plans++;
            var isLong = plan.Cells.Average() >= 6;
            longPlans += isLong ? 1 : 0;
            if (!plan.Parts.Contains(TrackRole.Chords))
                continue;

            joined++;
            longJoined += isLong ? 1 : 0;
            // the chords' events, as decided, every one in a frame the first cell's again from where its cell starts
            var events = song.Song.TrackEventStateTimelineMap.TrackTimelineMap.GetValueOrDefault(SongTracks.ChordsTrack)?.EventTimeline.Select(x => x.Position).ToArray() ?? [];
            var frame = (plan.Patterns == Polymeter.Plan.Section ? span.Duration : span.Meter.PatternDuration * plan.Patterns);
            for (var start = span.Start; start < span.End - 1e-9; start += frame)
            {
                var end = Math.Min(start + frame, span.End);
                var cells = new List<(double At, double Length)>();
                for (var (cell, at) = (0, start); at < end - 1e-9; at += cells[^1].Length)
                    cells.Add((at, plan.CellAt(cell++) * 0.25));
                // the motif as the longest cell plays it whole where it first does
                var longest = cells.FirstOrDefault(x => x.Length.Equals(plan.Longest * 0.25) && x.At + x.Length <= end + 1e-9);
                if (longest == default)
                    continue;
                var first = events.Where(x => x >= longest.At - 1e-9 && x < longest.At + longest.Length - 1e-9).Select(x => Math.Round(x - longest.At, 6)).ToHashSet();
                foreach (var onset in events.Where(x => x >= start - 1e-9 && x < Math.Min(start + frame, span.End) - 1e-9))
                {
                    var from = cells.Last(x => x.At <= onset + 1e-9).At;
                    await Assert.That(first.Contains(Math.Round(onset - from, 6))).IsTrue().Because($"seed {song.Seed} at {onset}");
                }
            }
        }

        await Assert.That(joined).IsGreaterThan(5);
        await Assert.That(longJoined / (double)Math.Max(1, longPlans)).IsGreaterThan((joined - longJoined) / (double)Math.Max(1, plans - longPlans));
    }

    [Test]
    public async Task ASnareOnTheCycle_StrikesOnlyItsCellsStarts()
    {
        var checkedSections = 0;
        var snares = DrumGroups.Snare.Drums.Select(DrumGroups.GetTrackNumber).ToHashSet();
        foreach (var song in TestCorpus.Range(512).Where(x => !Plans(x).IsEmpty))
        foreach (var span in song.Map.Sections)
        {
            if (!Plans(song).TryGetValue(span.SectionId, out var plan) || !plan.Snare || song.IsSolo(span))
                continue;

            var frame = (plan.Patterns == Polymeter.Plan.Section ? span.Duration : span.Meter.PatternDuration * plan.Patterns);
            var starts = new HashSet<double>();
            for (var start = span.Start; start < span.End - 1e-9; start += frame)
            for (var (cell, at) = (0, start); at < Math.Min(start + frame, span.End) - 1e-9; at += plan.CellAt(cell++) * 0.25)
                starts.Add(Math.Round(at, 6));
            // but in the bar before a line, a phrase's end, which the fill takes
            var notes = song.Song.Notes!.Where(x => snares.Contains(x.Key)).SelectMany(x => x.Value)
                .Where(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - span.Meter.BarDuration - 1e-9 && (x.Position - span.Start) % (Meter.PhraseBarCount * span.Meter.BarDuration) < (Meter.PhraseBarCount - 1) * span.Meter.BarDuration).ToArray();
            await Assert.That(notes.All(x => starts.Contains(Math.Round(x.Position, 6)))).IsTrue().Because($"seed {song.Seed} at {span.Start}");
            checkedSections++;
        }

        await Assert.That(checkedSections).IsGreaterThan(3);
    }

    [Test]
    public async Task AGenrePlayingAPolymeter_PlaysItInItsPlainestSongs_ThePlainestCells_TheKickAndTheBassWithIt()
    {
        var genre = new Genre(ImmutableDictionary<string, ImmutableDictionary<string, Rmg.Core.Probabilities.ByConvention>>.Empty
            .Add("polymeter", ImmutableDictionary<string, Rmg.Core.Probabilities.ByConvention>.Empty.Add("sections", new(1, 1, 1))));
        var plans = TestCorpus.InParallel(Enumerable.Range(0, 16), seed => Plans(TestCorpus.Get(seed, new SongOverrides(Base: 0, Genre: genre))))
            .SelectMany(x => x.Values).ToArray();
        await Assert.That(plans.Length).IsGreaterThan(0);
        foreach (var plan in plans)
        {
            await Assert.That(plan.Cells.All(x => x is 3 or 6)).IsTrue().Because(string.Join("+", plan.Cells));
            await Assert.That(plan.Kick || !plan.Parts.Contains(TrackRole.Riff)).IsTrue();
            await Assert.That(plan.Grows || plan.Snare).IsFalse();
        }
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
        Console.WriteLine($"  the chords with them {plans.Count(x => x.Parts.Contains(TrackRole.Chords)) / (double)plans.Length:P0}, of cells of 6 or more {plans.Count(x => x.Cells.Average() >= 6 && x.Parts.Contains(TrackRole.Chords)) / (double)Math.Max(1, plans.Count(x => x.Cells.Average() >= 6)):P0}, shorter {plans.Count(x => x.Cells.Average() < 6 && x.Parts.Contains(TrackRole.Chords)) / (double)Math.Max(1, plans.Count(x => x.Cells.Average() < 6)):P0}");
        Console.WriteLine($"  {plans.Length} polymeters: the bass with the riff {plans.Count(x => x.Parts.Contains(TrackRole.Riff) && x.Parts.Contains(TrackRole.Bass)) / (double)Math.Max(1, plans.Count(x => x.Parts.Contains(TrackRole.Riff))):P0}, the snare {plans.Count(x => x.Snare) / (double)plans.Length:P0}, the kick with them {plans.Count(x => x.Kick) / (double)plans.Length:P0}, over 8 bars {plans.Count(x => x.Patterns == 2) / (double)plans.Length:P0}, " +
                          $"running on {plans.Count(x => x.Patterns == Polymeter.Plan.Section) / (double)plans.Length:P0}, growing {plans.Count(x => x.Grows) / (double)plans.Length:P0}, composite {plans.Count(x => x.Cells.Length > 1) / (double)plans.Length:P0}: {string.Join(", ", plans.Where(x => x.Cells.Length > 1).Select(x => string.Join("+", x.Cells)).GroupBy(x => x).OrderByDescending(x => x.Count()).Take(12).Select(x => $"{x.Key} {x.Count()}"))}");
        await Task.CompletedTask;
    }
}
