using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Arrangements;

/// <summary>A drum solo played as a pitched solo is (<see cref="DrumSolos" />), the kit its scale, gesture by gesture by the drummer's vocabulary.</summary>
public sealed class DrumSoloTest
{
    /// <summary>Songs of wild solos, their form, melody and fills, where the drums solo as often as any part.</summary>
    private static SongOverrides Wild => new(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Form, 1).Add(Facet.Melody, 1).Add(Facet.Fills, 1));

    /// <summary>Every drum solo of a song, as the trace records it, with the section it plays in and whether the feet keep time.</summary>
    private static (SectionSpan Span, SoloLines.Segment[] Plan, bool Feet)[] DrumSolos(CorpusSong song) =>
    [
        ..song.Trace.Where(x => x.Point == TracePoints.DrumSolo).Select(x => ((int Index, SoloLines.Segment[] Plan, bool Feet))x.Value!)
            .Select(x => (song.Map.Sections[x.Index], x.Plan, x.Feet))
    ];

    private static readonly CorpusSong[] Songs = TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, Wild));

    [Test]
    public async Task ADrumSolosFeet_KeepTimeInMost_TheSectionsKickSofter_ElseTheKickStackedUnderItsAccentsNowAndThen()
    {
        var kick = DrumGroups.GetTrackNumber(DrumDefinitions.Kick);
        var (solos, feet, kept, stacking, others) = (0, 0, 0, 0, 0);
        var (feetLoudness, handsLoudness) = (new List<double>(), new List<double>());
        foreach (var song in Songs)
        foreach (var (span, plan, keepsTime) in DrumSolos(song))
        {
            var drums = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role == TrackRole.Drum).ToArray();
            var kicks = drums.Where(x => x.Key == kick).SelectMany(x => x.Value).ToArray();
            solos++;
            if (keepsTime)
            {
                // the section's kick under a pause, the hands silent, softer than the hands' calmest
                feet++;
                var pauses = plan.Where(x => x.Gesture == SoloGesture.Pause).ToArray();
                var under = kicks.Where(x => pauses.Any(p => x.Position >= p.Start - 1e-9 && x.Position < p.End - 1e-9)).ToArray();
                if (under.Length > 0)
                    kept++;
                feetLoudness.AddRange(under.Select(x => (double)x.Value.Velocity));
                handsLoudness.AddRange(drums.Where(d => d.Key != kick).SelectMany(d => d.Value).Where(h => h.Position >= span.Start && h.Position < plan[^1].End).Select(h => (double)h.Value.Velocity));
                continue;
            }

            // the kick struck with another drum in its runs and licks, which a cell's single hits never are
            others++;
            var runs = plan.Where(x => x.Gesture is SoloGesture.Run or SoloGesture.Lick).ToArray();
            var inRuns = kicks.Where(x => runs.Any(r => x.Position >= r.Start - 1e-9 && x.Position < r.End - 1e-9)).Select(x => Math.Round(x.Position, 6)).ToHashSet();
            var rest = drums.Where(x => x.Key != kick).SelectMany(x => x.Value).Select(x => Math.Round(x.Position, 6)).ToHashSet();
            if (inRuns.Any(rest.Contains))
                stacking++;
        }

        await Assert.That(solos).IsGreaterThan(10);
        await Assert.That(feet / (double)solos).IsBetween(0.55, 0.85);
        await Assert.That(kept).IsGreaterThan(feet / 3);
        await Assert.That(feetLoudness.Average()).IsLessThan(handsLoudness.Average());
        await Assert.That(stacking / (double)others).IsBetween(0.2, 0.8);
    }

    [Test]
    public async Task ADrumSolosClimax_StartsItsRunsOnACrash_ItsCalmerOnesNot()
    {
        var crashes = new HashSet<int> { DrumGroups.GetTrackNumber(DrumDefinitions.Cymbal) };
        var (climax, crashed, calm, calmCrashed) = (0, 0, 0, 0);
        foreach (var song in Songs)
        foreach (var (_, plan, _) in DrumSolos(song))
        {
            var starts = song.Song.Notes!.Where(x => crashes.Contains(x.Key)).SelectMany(x => x.Value).Select(x => Math.Round(x.Position, 6)).ToHashSet();
            foreach (var gesture in plan.Where(x => x.Gesture is SoloGesture.Run or SoloGesture.Lick))
            {
                var on = starts.Contains(Math.Round(gesture.Start, 6));
                if (gesture.Intensity >= Rmg.Core.Composition.DrumSolos.ClimaxIntensity)
                    (climax, crashed) = (climax + 1, crashed + (on ? 1 : 0));
                else
                    (calm, calmCrashed) = (calm + 1, calmCrashed + (on ? 1 : 0));
            }
        }

        await Assert.That(climax).IsGreaterThan(10);
        await Assert.That(crashed / (double)climax).IsGreaterThan(0.8);
        await Assert.That(calmCrashed / (double)Math.Max(1, calm)).IsLessThan(0.2);
    }

    [Test]
    public async Task ADrumSolosRuns_GoRoundItsKit_InTheDrummersCells_TheirFirstHitsTheLouder_ItsPausesTheHandsSilent()
    {
        var (solos, first, rest) = (0, new List<double>(), new List<double>());
        foreach (var song in Songs)
        {
            if (DrumSolos(song).Length == 0)
                continue;

            var drummer = song.Trace.Where(x => x.Point == TracePoints.Soloist).Select(x => ((TrackRole Role, Soloist Soloist))x.Value!).Single(x => x.Role == TrackRole.Drum).Soloist;
            var drums = song.Song.Notes!.Where(x => x.Key >= DrumGroups.FirstTrackNumber).ToArray();
            foreach (var (span, plan, feet) in DrumSolos(song))
            {
                solos++;
                var kick = DrumGroups.GetTrackNumber(DrumDefinitions.Kick);
                // all but the last bar, which the fill into the next section plays
                await Assert.That(plan[^1].End).IsLessThanOrEqualTo(span.End - span.Meter.BarDuration + 1e-9);
                foreach (var segment in plan)
                {
                    var hits = drums.SelectMany(x => x.Value.Where(n => n.Position >= segment.Start - 1e-9 && n.Position < segment.End - 1e-9).Select(n => (Track: x.Key, Note: n))).ToArray();
                    // a pause the hands silent, and the feet too unless they keep time
                    if (segment.Gesture == SoloGesture.Pause)
                        await Assert.That(hits.Where(x => !(feet && x.Track == kick))).IsEmpty().Because($"seed {song.Seed} at {segment.Start}");
                    if (segment.Gesture is not (SoloGesture.Run or SoloGesture.Lick))
                        continue;

                    await Assert.That(drummer.Shapes).Contains(segment.Shape);
                    await Assert.That(segment.Sizes.All(drummer.CellSizes.Contains)).IsTrue();
                    // and a climax's gesture its crash on its first hit
                    foreach (var (track, _) in hits.Where(x => !(segment.Intensity >= Rmg.Core.Composition.DrumSolos.ClimaxIntensity && Math.Abs(x.Note.Position - segment.Start) < 1e-9 && x.Track == DrumGroups.GetTrackNumber(DrumDefinitions.Cymbal))))
                        await Assert.That(DrumGroups.GroupOf(DrumGroups.GetDrum(track)) is var group && (group == DrumGroups.Snare || group == DrumGroups.Toms || group == DrumGroups.Kick)).IsTrue()
                            .Because($"seed {song.Seed}, {DrumGroups.GetDrum(track).Name} at {segment.Start}");
                    // a lick's cell on every note's place: the first of every cell the louder
                    if (segment.Gesture == SoloGesture.Lick && !segment.GoesOn)
                    {
                        var size = segment.Sizes[0];
                        foreach (var (hit, i) in hits.Where(x => !(feet && x.Track == kick)).OrderBy(x => x.Note.Position).Select((x, i) => (x, i)))
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
        var solos = Songs.SelectMany(song => DrumSolos(song).Select(x => (Song: song, x.Span, x.Plan, x.Feet))).ToArray();
        var segments = solos.SelectMany(x => x.Plan).ToArray();
        Console.WriteLine($"  {solos.Length} drum solos of {Songs.Length} songs, {segments.Length} segments: " +
                          string.Join(", ", segments.GroupBy(x => x.Gesture).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count() / (double)segments.Length:P0}")));
        var runs = segments.Where(x => x.Gesture is SoloGesture.Run or SoloGesture.Lick).ToArray();
        Console.WriteLine($"  runs and licks: shapes {string.Join(", ", runs.GroupBy(x => x.Shape).Select(x => $"{x.Key} {x.Count() / (double)runs.Length:P0}"))}; " +
                          $"cells {string.Join(", ", runs.SelectMany(x => x.Sizes).GroupBy(x => x).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count()}"))}; speeds {string.Join(", ", runs.GroupBy(x => x.Speed).OrderBy(x => x.Key).Select(x => $"{x.Key} a beat {x.Count()}"))}");
        var hitsABeat = solos.Select(x => x.Song.Song.Notes!.Where(n => n.Key >= DrumGroups.FirstTrackNumber).Sum(n => n.Value.Count(h => h.Position >= x.Span.Start && h.Position < x.Plan[^1].End)) / (x.Plan[^1].End - x.Span.Start)).ToArray();
        Console.WriteLine($"  hits a beat {hitsABeat.Average():F2}, from {hitsABeat.Min():F2} to {hitsABeat.Max():F2}; intensity swings {solos.SelectMany(x => x.Plan.Zip(x.Plan.Skip(1), (a, b) => Math.Abs(b.Intensity - a.Intensity))).Average():F2} a segment");
        // the kick under the hands' line: its hits a bar and how loud, and the hands' hits a beat beside it
        var kick = DrumGroups.GetTrackNumber(DrumDefinitions.Kick);
        var kicks = solos.Select(x => x.Song.Song.Notes!.GetValueOrDefault(kick)?.Where(h => h.Position >= x.Span.Start && h.Position < x.Plan[^1].End).ToArray() ?? []).ToArray();
        var bars = solos.Select(x => (x.Plan[^1].End - x.Span.Start) / x.Span.Meter.BarDuration).ToArray();
        var handsABeat = solos.Select((x, i) => x.Song.Song.Notes!.Where(n => n.Key >= DrumGroups.FirstTrackNumber && n.Key != kick).Sum(n => n.Value.Count(h => h.Position >= x.Span.Start && h.Position < x.Plan[^1].End)) / (x.Plan[^1].End - x.Span.Start)).ToArray();
        var pausing = solos.Select((x, i) => x.Plan.Any(p => p.Gesture == SoloGesture.Pause && kicks[i].Any(h => h.Position >= p.Start - 1e-9 && h.Position < p.End - 1e-9))).ToArray();
        Console.WriteLine($"  the kick {kicks.Select((x, i) => x.Length / bars[i]).Average():F2} hits a bar, velocity {kicks.SelectMany(x => x).Select(x => (double)x.Value.Velocity).DefaultIfEmpty().Average():F2}; " +
                          $"under a pause in {pausing.Count(x => x) / (double)solos.Length:P0} of the solos; the hands {handsABeat.Average():F2} hits a beat");
        await Task.CompletedTask;
    }
}
