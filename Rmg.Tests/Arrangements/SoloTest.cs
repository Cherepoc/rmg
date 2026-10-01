using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Events;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.Arrangements;

public sealed class SoloTest
{
    private static readonly TrackRole[] Band = [TrackRole.Melody, TrackRole.Chords, TrackRole.Bass, TrackRole.Riff, TrackRole.Drum];

    [Test]
    public async Task ThePlainestSongs_HaveNoSolo_AndTheWildestOneAsLikelyAsNot()
    {
        var context = new GenerationContext(1);
        var plain = Unconventionality.Generate(0, _ => context);
        var wild = Unconventionality.Generate(1, _ => context);

        await Assert.That(Enumerable.Range(0, 1000).All(_ => Solos.Draw(context, 1, plain, Band) is null)).IsTrue();
        var solos = Enumerable.Range(0, 4000).Count(_ => Solos.Draw(context, 1, wild, Band) is not null) / 4000.0;
        await Assert.That(solos).IsEqualTo(0.5).Within(0.03);
        // and never the song's first section
        await Assert.That(Enumerable.Range(0, 1000).All(_ => Solos.Draw(context, 0, wild, Band) is null)).IsTrue();
    }

    [Test]
    public async Task ASolo_IsPlayedByItsSoloists_TheMelodyResting_AndADrumSolo_ByTheDrumsAlone()
    {
        var checkedSolos = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Form, 1)))))
        foreach (var (index, solo) in song.Solos)
        {
            // a melody with no notes there leaves nothing to solo on
            if (song.Trace.Any(x => x.Point == TracePoints.SoloLine && ((int Index, int Notes))x.Value! is { } line && line.Index == index && line.Notes == 0))
                continue;

            var span = song.Map.Sections[index];
            int Notes(TrackRole role) => song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role == role)
                .Sum(x => x.Value.Count(n => n.Position >= span.Start + 1e-9 && n.Position < span.End - song.Map.Meter.BarDuration));
            if (solo.IsDrumSolo)
            {
                foreach (var part in Enum.GetValues<TrackRole>().Where(x => x != TrackRole.Drum))
                    await Assert.That(Notes(part)).IsEqualTo(0).Because($"seed {song.Seed}, {part} in a drum solo");
            }
            else
            {
                if (!solo.Soloists.Contains(TrackRole.Melody))
                    await Assert.That(Notes(TrackRole.Melody)).IsEqualTo(0).Because($"seed {song.Seed}, the melody in a solo");
                await Assert.That(solo.Soloists.Sum(Notes)).IsGreaterThan(0).Because($"seed {song.Seed}, the soloists");
            }

            checkedSolos++;
        }

        await Assert.That(checkedSolos).IsGreaterThan(10);
    }

    private static SongOverrides Facets(double form, double melody) =>
        new(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Form, form).Add(Facet.Melody, melody));

    /// <summary>Every pitched solo's segments, as the trace records them: whether it is its own, and each segment's start, end, intensity and gesture.</summary>
    private static (int Index, bool IsOwn, (double Start, double End, double Intensity, SoloGesture Gesture)[] Segments)[] Gestures(CorpusSong song) =>
    [
        ..song.Trace.Where(x => x.Point == TracePoints.SoloGestures).Select(x => ((int Index, bool IsOwn, (double Start, double Intensity, SoloGesture Gesture)[] Segments))x.Value!)
            .Select(x => (x.Index, x.IsOwn, x.Segments.Select((g, i) => (g.Start, i + 1 < x.Segments.Length ? x.Segments[i + 1].Start : song.Map.Sections[x.Index].End, g.Intensity, g.Gesture)).ToArray()))
    ];

    [Test]
    public async Task APlainSolo_IsOnTheMelody_AndAWildOne_ItsOwn()
    {
        var plain = TestCorpus.InParallel(Enumerable.Range(0, 32), seed => Gestures(TestCorpus.Get(seed, Facets(1, 0)))).SelectMany(x => x).ToArray();
        var wild = TestCorpus.InParallel(Enumerable.Range(0, 32), seed => Gestures(TestCorpus.Get(seed, Facets(1, 1)))).SelectMany(x => x).ToArray();

        await Assert.That(plain.Length).IsGreaterThan(0);
        await Assert.That(plain.All(x => !x.IsOwn)).IsTrue();
        await Assert.That(wild.All(x => x.IsOwn)).IsTrue();
    }

    [Test]
    public async Task ASolo_RunsWhereItIsIntense_AndAWildSoloistSwingsMore()
    {
        var plain = TestCorpus.InParallel(Enumerable.Range(0, 32), seed => Gestures(TestCorpus.Get(seed, Facets(1, 0)))).SelectMany(x => x).ToArray();
        var wild = TestCorpus.InParallel(Enumerable.Range(0, 32), seed => Gestures(TestCorpus.Get(seed, Facets(1, 1)))).SelectMany(x => x).ToArray();
        var segments = plain.Concat(wild).SelectMany(x => x.Segments).ToArray();
        double Runs(IEnumerable<(double, double, double, SoloGesture Gesture)> some) => some.Count(x => x.Gesture == SoloGesture.Run) / (double)Math.Max(1, some.Count());

        await Assert.That(Runs(segments.Where(x => x.Intensity > 0.66))).IsGreaterThan(Runs(segments.Where(x => x.Intensity < 0.33)) * 2);

        static double Swing((int, bool, (double, double, double Intensity, SoloGesture)[] Segments)[] solos) =>
            solos.SelectMany(x => x.Segments.Zip(x.Segments.Skip(1), (a, b) => Math.Abs(b.Intensity - a.Intensity))).Average();
        await Assert.That(Swing(wild)).IsGreaterThan(Swing(plain) * 1.5);
    }

    [Test]
    public async Task ASolosScaleRun_StepsByTheScale_AndItsHeldNotes_AreTheChords()
    {
        int held = 0, onChord = 0, scaleRuns = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, Facets(1, 1))))
        foreach (var (index, _, segments) in Gestures(song))
        {
            // a solo of one soloist, all of whose line is its
            var solo = song.Solos[index];
            if (solo.Soloists.Length > 1)
                continue;

            var notes = song.Song.Notes!.Single(x => song.Song.TrackDefinitions[x.Key].Role == solo.Soloists[0]).Value;
            var shapes = song.Trace.Where(x => x.Point == TracePoints.SoloCells).Select(x => ((double Start, SoloShape Shape, int[]))x.Value!).ToDictionary(x => Math.Round(x.Start, 6), x => x.Shape);
            foreach (var (start, end, _, gesture) in segments)
            {
                var played = notes.Where(x => x.Position >= start - 1e-9 && x.Position < end - 1e-9).ToArray();
                // a run up or down the scale, the others leaping by their figures
                if (gesture == SoloGesture.Run && shapes.GetValueOrDefault(Math.Round(start, 6)) == SoloShape.Scale && scaleRuns++ >= 0)
                    foreach (var (a, b) in played.Zip(played.Skip(1)))
                        await Assert.That(Math.Abs(b.Value.Pitches[0] - a.Value.Pitches[0])).IsLessThanOrEqualTo(4).Because($"seed {song.Seed} at {b.Position}");
                if (gesture == SoloGesture.Held && played.Length > 0)
                {
                    held++;
                    var (offsets, _, chord) = Realizer.GetChord(played[0].Value.State);
                    var tones = new[] { 0, 2, 4 }.Select(x => chord.GetPitch(x).Mod(12)).ToHashSet();
                    if (tones.Contains(played[0].Value.Pitches[0].Mod(12)))
                        onChord++;
                }
            }
        }

        await Assert.That(held).IsGreaterThan(0);
        await Assert.That(scaleRuns).IsGreaterThan(0);
        await Assert.That(onChord / (double)held).IsGreaterThan(0.9);
    }

    /// <summary>
    ///     Every run and lick of a song's solos, with its soloist's vocabulary: its start and end, its shape, the sizes of its
    ///     cells and the notes its first soloist plays in it.
    /// </summary>
    private static (Soloist Soloist, double Start, double End, SoloShape Shape, int[] Cells, TimelineItem<RealizedNote>[] Notes)[] Runs(CorpusSong song)
    {
        var soloists = song.Trace.Where(x => x.Point == TracePoints.Soloist).Select(x => ((TrackRole Role, Soloist Soloist))x.Value!).ToDictionary(x => x.Role, x => x.Soloist);
        var cells = song.Trace.Where(x => x.Point == TracePoints.SoloCells).Select(x => ((double Start, SoloShape Shape, int[] Cells))x.Value!).ToDictionary(x => Math.Round(x.Start, 6));
        return
        [
            ..Gestures(song).Where(x => song.Solos[x.Index].Soloists.Length == 1).SelectMany(solo =>
            {
                var role = song.Solos[solo.Index].Soloists[0];
                var notes = song.Song.Notes!.Single(x => song.Song.TrackDefinitions[x.Key].Role == role).Value;
                return solo.Segments.Where(x => cells.ContainsKey(Math.Round(x.Start, 6))).Select(x =>
                {
                    var run = cells[Math.Round(x.Start, 6)];
                    return (soloists[role], x.Start, x.End, run.Shape, run.Cells, notes.Where(n => n.Position >= x.Start - 1e-9 && n.Position < x.End - 1e-9).ToArray());
                });
            })
        ];
    }

    [Test]
    public async Task ARun_IsMadeOfItsSoloistsCells_AccentedAsTheyStart_NoFasterThanItPlays()
    {
        var plain = TestCorpus.InParallel(Enumerable.Range(0, 32), seed => Runs(TestCorpus.Get(seed, Facets(1, 0)))).SelectMany(x => x).ToArray();
        var wild = TestCorpus.InParallel(Enumerable.Range(0, 32), seed => (Song: TestCorpus.Get(seed, Facets(1, 1)), Seed: seed)).Select(x => (x.Song, Runs: Runs(x.Song))).ToArray();
        var runs = plain.Concat(wild.SelectMany(x => x.Runs)).ToArray();

        foreach (var run in runs)
        {
            await Assert.That(run.Soloist.Shapes).Contains(run.Shape);
            // every cell of the soloist's sizes, as it was started, however the run's end cut it short
            await Assert.That(run.Cells.All(run.Soloist.CellSizes.Contains)).IsTrue();
        }

        await Assert.That(plain.SelectMany(x => x.Cells).All(x => x is 3 or 4)).IsTrue();
        await Assert.That(runs.Where(x => x.Soloist.Groups && x.Cells.Length > 1).All(x => x.Cells.Distinct().Count() > 1)).IsTrue();

        // the first note of a cell the louder
        var (first, rest) = (new List<double>(), new List<double>());
        foreach (var run in runs.Where(x => x.Notes.Length == x.Cells.Sum()))
        {
            var starts = run.Cells.Select((_, i) => run.Cells.Take(i).Sum()).ToHashSet();
            foreach (var (note, i) in run.Notes.Select((x, i) => (x, i)))
                (starts.Contains(i) ? first : rest).Add(note.Value.Velocity);
        }

        await Assert.That(first.Average()).IsGreaterThan(rest.Average());

        // and no faster than the soloist plays at the song's tempo there
        foreach (var (song, songRuns) in wild)
        foreach (var run in songRuns.Where(x => x.Notes.Length > 1))
        {
            var beatsASecond = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(run.Start).GetStateValue(StateKinds.Tempo) * Meter.BaseTempo / 60;
            var shortest = run.Notes.Zip(run.Notes.Skip(1), (a, b) => b.Position - a.Position).Min();
            await Assert.That(beatsASecond / shortest).IsLessThanOrEqualTo(Math.Max(run.Soloist.MaxNotesASecond, run.Soloist.Speeds[0] * beatsASecond) + 1e-6);
        }
    }

    /// <summary>The solos' runs, by fifth of the melody facet: the sizes of their cells, their shapes, and the share grouping cells of several sizes.</summary>
    [Test]
    [Explicit]
    public async Task CellReport()
    {
        static double MelodyOf(CorpusSong song) => ((Unconventionality)song.Trace.Single(t => t.Point == TracePoints.SongUnconventionality).Value!)[Facet.Melody];
        var runs = TestCorpus.Measure(512, song => Runs(song).Select(x => (Fifth: Math.Clamp((int)(MelodyOf(song) * 5), 0, 4), Run: x)).ToArray()).SelectMany(x => x).ToArray();
        foreach (var band in runs.GroupBy(x => x.Fifth).OrderBy(x => x.Key))
        {
            var cells = band.SelectMany(x => x.Run.Cells).ToArray();
            string Shares<T>(IEnumerable<T> values, int total) => string.Join(" ", values.GroupBy(x => x).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count() / (double)total:P0}"));
            Console.WriteLine($"  melody {band.Key}/5, {band.Count()} runs and licks: cells {Shares(cells, cells.Length)}; shapes {Shares(band.Select(x => x.Run.Shape), band.Count())}; " +
                              $"cells of several sizes in {band.Count(x => x.Run.Cells.Distinct().Count() > 1) / (double)band.Count():P0}, by {band.Select(x => x.Run.Soloist).Distinct().Count(x => x.Groups)} grouping soloists");
        }

        await Task.CompletedTask;
    }

    /// <summary>The solos' gestures, by fifth of the melody facet: each gesture's share, the notes a beat, how far the intensity swings, and the share of solos of their own.</summary>
    [Test]
    [Explicit]
    public async Task GestureReport()
    {
        static double MelodyOf(CorpusSong song) => ((Unconventionality)song.Trace.Single(t => t.Point == TracePoints.SongUnconventionality).Value!)[Facet.Melody];
        var solos = TestCorpus.Measure(512, song => Gestures(song).Select(x => (Fifth: Math.Clamp((int)(MelodyOf(song) * 5), 0, 4), Solo: x)).ToArray()).SelectMany(x => x).ToArray();
        foreach (var band in solos.GroupBy(x => x.Fifth).OrderBy(x => x.Key))
        {
            var segments = band.SelectMany(x => x.Solo.Segments).ToArray();
            var shares = segments.GroupBy(x => x.Gesture).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count() / (double)segments.Length:P0}");
            var swing = band.SelectMany(x => x.Solo.Segments.Zip(x.Solo.Segments.Skip(1), (a, b) => Math.Abs(b.Intensity - a.Intensity))).DefaultIfEmpty().Average();
            Console.WriteLine($"  melody {band.Key}/5, {band.Count()} solos, own {band.Count(x => x.Solo.IsOwn) / (double)band.Count():P0}: {string.Join(", ", shares)}; intensity swings {swing:F2} a segment");
        }

        await Task.CompletedTask;
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Range(512);
        foreach (var band in songs.GroupBy(x => Math.Clamp((int)(((Unconventionality)x.Trace.Single(t => t.Point == TracePoints.SongUnconventionality).Value!)[Facet.Form] * 5), 0, 4)).OrderBy(x => x.Key))
            Console.WriteLine($"  form {band.Key}/5, {band.Count()} songs: with a solo {band.Count(x => !x.Solos.IsEmpty) / (double)band.Count():P0}, solos a song {band.Average(x => x.Solos.Count):F2}");
        foreach (var band in songs.GroupBy(x => Math.Clamp((int)(((Unconventionality)x.Trace.Single(t => t.Point == TracePoints.SongUnconventionality).Value!)[Facet.Melody] * 5), 0, 4)).OrderBy(x => x.Key))
        {
            var soloists = band.SelectMany(x => x.Solos.Values).SelectMany(x => x.Soloists).ToArray();
            Console.WriteLine($"  melody {band.Key}/5, {soloists.Length} soloists: {string.Join(", ", soloists.GroupBy(x => x).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count() / (double)soloists.Length:P0}"))}");
        }

        var solos = songs.SelectMany(x => x.Solos.Values).ToArray();
        Console.WriteLine($"soloists: {string.Join(", ", solos.SelectMany(x => x.Soloists).GroupBy(x => x).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count()}"))}; " +
                          $"several {solos.Count(x => x.Soloists.Length > 1)}, together {solos.Count(x => x.Soloists.Length > 1 && x.Mode == SoloMode.Together)}");
        await Task.CompletedTask;
    }
}
