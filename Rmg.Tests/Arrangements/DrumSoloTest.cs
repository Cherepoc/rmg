using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Arrangements;

/// <summary>A drum solo played as a pitched solo is (<see cref="DrumSolos" />), the kit its scale, gesture by gesture by the drummer's vocabulary.</summary>
public sealed class DrumSoloTest
{
    /// <summary>Songs of wild solos, their form, melody and fills, where the drums solo as often as any part.</summary>
    private static SongOverrides Wild => new(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Form, 1).Add(Facet.Melody, 1).Add(Facet.Fills, 1));

    /// <summary>Every drum solo of a song, as the trace records it, with the section it plays in.</summary>
    private static (SectionSpan Span, SoloLines.Segment[] Plan)[] DrumSolos(CorpusSong song) =>
    [
        ..song.Trace.Where(x => x.Point == TracePoints.DrumSolo).Select(x => ((int Index, SoloLines.Segment[] Plan))x.Value!)
            .Select(x => (song.Map.Sections[x.Index], x.Plan))
    ];

    private static readonly CorpusSong[] Songs = TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, Wild));

    [Test]
    public async Task ADrumSolosRuns_GoRoundItsKit_InTheDrummersCells_TheirFirstHitsTheLouder_ItsPausesSilent()
    {
        var (solos, first, rest) = (0, new List<double>(), new List<double>());
        foreach (var song in Songs)
        {
            if (DrumSolos(song).Length == 0)
                continue;

            var drummer = song.Trace.Where(x => x.Point == TracePoints.Soloist).Select(x => ((TrackRole Role, Soloist Soloist))x.Value!).Single(x => x.Role == TrackRole.Drum).Soloist;
            var drums = song.Song.Notes!.Where(x => x.Key >= DrumGroups.FirstTrackNumber).ToArray();
            foreach (var (span, plan) in DrumSolos(song))
            {
                solos++;
                // all but the last bar, which the fill into the next section plays
                await Assert.That(plan[^1].End).IsLessThanOrEqualTo(span.End - span.Meter.BarDuration + 1e-9);
                foreach (var segment in plan)
                {
                    var hits = drums.SelectMany(x => x.Value.Where(n => n.Position >= segment.Start - 1e-9 && n.Position < segment.End - 1e-9).Select(n => (Track: x.Key, Note: n))).ToArray();
                    if (segment.Gesture == SoloGesture.Pause)
                        await Assert.That(hits).IsEmpty().Because($"seed {song.Seed} at {segment.Start}");
                    if (segment.Gesture is not (SoloGesture.Run or SoloGesture.Lick))
                        continue;

                    await Assert.That(drummer.Shapes).Contains(segment.Shape);
                    await Assert.That(segment.Sizes.All(drummer.CellSizes.Contains)).IsTrue();
                    foreach (var (track, _) in hits)
                        await Assert.That(DrumGroups.GroupOf(DrumGroups.GetDrum(track)) is var group && (group == DrumGroups.Snare || group == DrumGroups.Toms || group == DrumGroups.Kick)).IsTrue()
                            .Because($"seed {song.Seed}, {DrumGroups.GetDrum(track).Name} at {segment.Start}");
                    // a lick's cell on every note's place: the first of every cell the louder
                    if (segment.Gesture == SoloGesture.Lick && !segment.GoesOn)
                    {
                        var size = segment.Sizes[0];
                        foreach (var (hit, i) in hits.OrderBy(x => x.Note.Position).Select((x, i) => (x, i)))
                            (i % size == 0 ? first : rest).Add(hit.Note.Value.Velocity);
                    }
                }
            }
        }

        await Assert.That(solos).IsGreaterThan(5);
        await Assert.That(first.Average()).IsGreaterThan(rest.Average());
    }

    /// <summary>The drum solos' gestures, cells and hits a beat, and how far their intensity swings, of songs of wild solos.</summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        var solos = Songs.SelectMany(song => DrumSolos(song).Select(x => (Song: song, x.Span, x.Plan))).ToArray();
        var segments = solos.SelectMany(x => x.Plan).ToArray();
        Console.WriteLine($"  {solos.Length} drum solos of {Songs.Length} songs, {segments.Length} segments: " +
                          string.Join(", ", segments.GroupBy(x => x.Gesture).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count() / (double)segments.Length:P0}")));
        var runs = segments.Where(x => x.Gesture is SoloGesture.Run or SoloGesture.Lick).ToArray();
        Console.WriteLine($"  runs and licks: shapes {string.Join(", ", runs.GroupBy(x => x.Shape).Select(x => $"{x.Key} {x.Count() / (double)runs.Length:P0}"))}; " +
                          $"cells {string.Join(", ", runs.SelectMany(x => x.Sizes).GroupBy(x => x).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count()}"))}; speeds {string.Join(", ", runs.GroupBy(x => x.Speed).OrderBy(x => x.Key).Select(x => $"{x.Key} a beat {x.Count()}"))}");
        var hitsABeat = solos.Select(x => x.Song.Song.Notes!.Where(n => n.Key >= DrumGroups.FirstTrackNumber).Sum(n => n.Value.Count(h => h.Position >= x.Span.Start && h.Position < x.Plan[^1].End)) / (x.Plan[^1].End - x.Span.Start)).ToArray();
        Console.WriteLine($"  hits a beat {hitsABeat.Average():F2}, from {hitsABeat.Min():F2} to {hitsABeat.Max():F2}; intensity swings {solos.SelectMany(x => x.Plan.Zip(x.Plan.Skip(1), (a, b) => Math.Abs(b.Intensity - a.Intensity))).Average():F2} a segment");
        await Task.CompletedTask;
    }
}
