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
        ..Plans(song).Select(x => (x.Index, x.IsOwn, x.Plan.Select(g => (g.Start, g.End, g.Intensity, g.Gesture)).ToArray()))
    ];

    /// <summary>Every pitched solo's plan, as the trace records it, and whether it is its own.</summary>
    private static (int Index, bool IsOwn, SoloLines.Segment[] Plan)[] Plans(CorpusSong song) =>
        [..song.Trace.Where(x => x.Point == TracePoints.SoloGestures).Select(x => ((int, bool, SoloLines.Segment[]))x.Value!)];

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

    /// <summary>A song of solos played as wild as can be, its form, its melody and its sound.</summary>
    private static SongOverrides Wild => new(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Form, 1).Add(Facet.Melody, 1).Add(Facet.Sound, 1));

    /// <summary>Every note of a song's solos, by the intensity there, from 0 to 1.</summary>
    private static (double Intensity, RealizedNote Note)[] SoloNotes(CorpusSong song) =>
    [
        ..song.Song.Notes!.Values.SelectMany(x => x)
            .Where(x => x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 1)
            .Select(x => (x.Value.State.GetStateValue(CompositionStateKinds.SoloIntensity) / 100.0, x.Value))
    ];

    [Test]
    public async Task AnIntenseMomentOfASolo_BendsOftener_AndFurther()
    {
        // of the notes long enough to bend, a run's going by straight, by thirds of the intensity
        static ((double, RealizedNote Note)[] Calm, (double, RealizedNote Note)[] Intense) Thirds(SongOverrides overrides)
        {
            var notes = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => SoloNotes(TestCorpus.Get(seed, overrides)))
                .SelectMany(x => x).Where(x => x.Note.Duration >= Expression.ShortestShaped).OrderBy(x => x.Intensity).ToArray();
            return ([..notes.Take(notes.Length / 3)], [..notes.TakeLast(notes.Length / 3)]);
        }

        static bool Bent(RealizedNote note) => (BendKind)note.State.GetStateValue(CompositionStateKinds.Bend) is not (BendKind.None or BendKind.Slide);
        static double Depth(IEnumerable<(double, RealizedNote Note)> some) => some.Where(x => Bent(x.Note)).Select(x => Math.Abs(x.Note.State.GetStateValue(CompositionStateKinds.BendDepth))).DefaultIfEmpty().Average();

        // oftener where the song's sound leaves its notes room to bend more, as the wildest bend every one they can
        var middling = Thirds(Wild with { Facets = Wild.Facets!.SetItem(Facet.Sound, 0.5) });
        await Assert.That(middling.Intense.Count(x => Bent(x.Note))).IsGreaterThan(middling.Calm.Count(x => Bent(x.Note)));

        var wild = Thirds(Wild);
        await Assert.That(Depth(wild.Intense)).IsGreaterThan(Depth(wild.Calm));
    }

    [Test]
    public async Task ASolo_PlaysOverItsBand_WhateverPartSolos()
    {
        var (above, solos) = (0, 0);
        foreach (var song in TestCorpus.Range(256))
        {
            var band = song.Rendered.Tracks.Where(t => t.Role is TrackRole.Chords or TrackRole.Drum or TrackRole.Bass).ToArray();
            foreach (var (index, solo) in song.Solos.Where(x => !x.Value.IsDrumSolo))
            {
                var span = song.Map.Sections[index];
                bool InSolo(double p) => p >= span.Start - 1e-9 && p < span.End - 1e-9;
                var soloing = song.Rendered.Tracks.Where(t => solo.Soloists.Contains(t.Role)).SelectMany(t => t.NoteTimeline.Where(x => InSolo(x.Position))).ToArray();
                var backing = band.Where(t => !solo.Soloists.Contains(t.Role)).SelectMany(t => t.NoteTimeline.Where(x => InSolo(x.Position))).ToArray();
                if (soloing.Length == 0 || backing.Length == 0)
                    continue;

                solos++;
                if (soloing.Average(x => x.Value.Velocity) > backing.Average(x => x.Value.Velocity))
                    above++;
            }
        }

        await Assert.That(solos).IsGreaterThan(50);
        await Assert.That(above / (double)solos).IsGreaterThan(0.85);
    }

    [Test]
    public async Task TheCounterMelodyAndThePad_MakeRoomForASolo()
    {
        var checkedSolos = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, Facets(1, 0.5))))
        foreach (var (index, solo) in song.Solos.Where(x => !x.Value.IsDrumSolo))
        {
            var span = song.Map.Sections[index];
            foreach (var role in new[] { TrackRole.CounterMelody, TrackRole.Pad }.Where(x => !solo.Soloists.Contains(x)))
                await Assert.That(song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role == role)
                    .Sum(x => x.Value.Count(n => n.Position >= span.Start + 1e-9 && n.Position < span.End - 1e-9))).IsEqualTo(0).Because($"seed {song.Seed}, {role} in a solo");
            checkedSolos++;
        }

        await Assert.That(checkedSolos).IsGreaterThan(10);
    }

    /// <summary>How a solo's notes are played by its intensity, in thirds: how many bend, how far, how many sing a wide vibrato, and how many slide.</summary>
    [Test]
    [Explicit]
    public async Task ExpressionReport()
    {
        var notes = TestCorpus.InParallel(Enumerable.Range(0, 128), seed => SoloNotes(TestCorpus.Get(seed, Wild)))
            .SelectMany(x => x).Where(x => x.Note.Duration >= Expression.ShortestShaped).OrderBy(x => x.Intensity).ToArray();
        foreach (var (name, third) in new[] { ("calmest", 0), ("middle", 1), ("most intense", 2) })
        {
            var some = notes.Skip(third * notes.Length / 3).Take(notes.Length / 3).Select(x => x.Note).ToArray();
            var bent = some.Where(x => (BendKind)x.State.GetStateValue(CompositionStateKinds.Bend) is not (BendKind.None or BendKind.Slide)).ToArray();
            Console.WriteLine($"  sound 1 given, the {name} third of the solos' notes of an 8th or longer: {bent.Length / (double)some.Length:P0} bend, by {bent.Select(x => Math.Abs(x.State.GetStateValue(CompositionStateKinds.BendDepth))).DefaultIfEmpty().Average():F1} semitones; " +
                              $"{some.Count(x => x.State.GetStateValue(CompositionStateKinds.WideVibrato) > 0) / (double)some.Length:P1} a wide vibrato; {some.Count(x => (BendKind)x.State.GetStateValue(CompositionStateKinds.Bend) == BendKind.Slide) / (double)some.Length:P1} slide");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    ///     How many pitched solos of 8 bars or more bend at all or sing a vibrato, by fifth of the sound facet and by the
    ///     family of the first soloist's instrument: guitars and synth leads, the other instruments that bend, and the rest.
    /// </summary>
    [Test]
    [Explicit]
    public async Task PitchReport()
    {
        static double SoundOf(CorpusSong song) => ((Unconventionality)song.Trace.Single(t => t.Point == TracePoints.SongUnconventionality).Value!)[Facet.Sound];
        static string Family(int program) => program is >= 24 and <= 31 or >= 80 and <= 87 ? "guitar or lead" : Expression.Bends(program) ? "other bending" : "not bending";
        var solos = TestCorpus.Measure(1024, song => Gestures(song).Select(g =>
        {
            var span = song.Map.Sections[g.Index];
            var track = song.Song.TrackDefinitions.First(t => t.Value.Role == song.Solos[g.Index].Soloists[0]);
            var notes = song.Song.Notes!.GetValueOrDefault(track.Key)?.Where(n => n.Position >= span.Start - 1e-9 && n.Position < span.End - 1e-9 && n.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 1).Select(n => n.Value.State).ToArray() ?? [];
            return (Fifth: Math.Clamp((int)(SoundOf(song) * 5), 0, 4), Family: Family(((PitchInstrumentTrack)track.Value).InstrumentCode), Bars: span.Duration / span.Meter.BarDuration,
                Bends: notes.Any(n => (BendKind)n.GetStateValue(CompositionStateKinds.Bend) is not (BendKind.None or BendKind.Slide)),
                Vibrato: notes.Any(n => n.GetStateValue(CompositionStateKinds.Vibrato) > 0 || n.GetStateValue(CompositionStateKinds.WideVibrato) > 0));
        }).Where(x => x.Bars >= 8).ToArray()).SelectMany(x => x).ToArray();
        foreach (var band in solos.GroupBy(x => x.Fifth).OrderBy(x => x.Key))
            Console.WriteLine($"  sound {band.Key}/5, {band.Count()} solos: bend {band.Count(x => x.Bends) / (double)band.Count():P0}, a vibrato {band.Count(x => x.Vibrato) / (double)band.Count():P0}");
        foreach (var family in solos.GroupBy(x => x.Family).OrderBy(x => x.Key))
            Console.WriteLine($"  {family.Key}, {family.Count()} solos: bend {family.Count(x => x.Bends) / (double)family.Count():P0}, a vibrato {family.Count(x => x.Vibrato) / (double)family.Count():P0}");
        await Task.CompletedTask;
    }

    [Test]
    public async Task ARun_KeepsToItsSoloistsStamina_AtEverySpeed_AndToItsCap()
    {
        var checkedRuns = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, Facets(1, 1))))
        {
            var soloists = song.Trace.Where(x => x.Point == TracePoints.Soloist).Select(x => ((TrackRole Role, Soloist Soloist))x.Value!).ToDictionary(x => x.Role, x => x.Soloist);
            var common = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap;
            foreach (var (index, _, plan) in Plans(song))
            {
                var soloist = soloists[song.Solos[index].Soloists[0]];
                var played = new Dictionary<int, double>();
                foreach (var segment in plan)
                {
                    if (!segment.GoesOn)
                        played.Clear();
                    if (segment.Gesture != SoloGesture.Run)
                        continue;

                    checkedRuns++;
                    played[segment.Speed] = played.GetValueOrDefault(segment.Speed) + segment.End - segment.Start;
                    var beatsASecond = common.GetEffectiveStateMapAt(segment.Start).GetStateValue(Rmg.Core.Events.StateKinds.Tempo) * Meter.BaseTempo / 60;
                    // where a soloist has nothing slower to play at, its slowest
                    if (segment.Speed == soloist.Speeds[0])
                        continue;
                    await Assert.That(played[segment.Speed]).IsLessThanOrEqualTo(soloist.BeatsAt(segment.Speed) + 1e-9).Because($"seed {song.Seed} at {segment.Start}");
                    await Assert.That(segment.Speed * beatsASecond).IsLessThanOrEqualTo(soloist.MaxNotesASecond + 1e-9);
                }
            }
        }

        await Assert.That(checkedRuns).IsGreaterThan(50);
    }

    [Test]
    public async Task EveryPhraseOfASolo_EndsOnARelease_AChordsNoteHeldFromAStrongBeat()
    {
        var (releases, onChord) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, Facets(1, 0.5))))
        foreach (var (index, _, plan) in Plans(song))
        {
            var span = song.Map.Sections[index];
            var strong = span.Meter.BarDuration >= 4 ? span.Meter.BarDuration / 2 : span.Meter.BarDuration;
            foreach (var phrase in plan.GroupBy(x => x.Phrase))
            {
                var release = phrase.Last(x => x.Gesture != SoloGesture.Pause);
                await Assert.That(release.IsRelease).IsTrue().Because($"seed {song.Seed} at {release.Start}");
                await Assert.That(Math.Abs(Math.Round((release.Start - span.Start) / strong) * strong - (release.Start - span.Start))).IsLessThan(1e-6);
                await Assert.That(release.End - release.Start).IsGreaterThanOrEqualTo(1 - 1e-9);
            }

            // its note the chord's, marked as a release, on the first soloist's part
            var soloist = song.Song.TrackDefinitions.First(x => x.Value.Role == song.Solos[index].Soloists[0]).Key;
            foreach (var note in song.Song.Notes![soloist].Where(x => x.Value.State.GetStateValue(CompositionStateKinds.SoloRelease) == 1 && x.Position >= span.Start && x.Position < span.End))
            {
                releases++;
                var (_, _, chord) = Realizer.GetChord(note.Value.State);
                if (new[] { 0, 2, 4 }.Select(x => chord.GetPitch(x).Mod(12)).Contains(note.Value.Pitches[0].Mod(12)))
                    onChord++;
            }
        }

        await Assert.That(releases).IsGreaterThan(20);
        await Assert.That(onChord / (double)releases).IsGreaterThan(0.9);
    }

    [Test]
    public async Task ARelease_SingsItsSoloistsVibrato_BendsOnlyIntoIt_TheMoreIntenseTheOftener_AndNeverSlides()
    {
        var releases = new List<(double Intensity, BendKind Bend, bool Vibrato)>();
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, Wild)))
        {
            var soloists = song.Trace.Where(x => x.Point == TracePoints.Soloist).Select(x => ((TrackRole Role, Soloist Soloist))x.Value!).ToDictionary(x => x.Role, x => x.Soloist);
            foreach (var (track, notes) in song.Song.Notes!)
            {
                var role = song.Song.TrackDefinitions[track].Role;
                // a soloist that sings one, on an instrument that bends, playing a line, but where it echoes
                var echoes = notes.Any(x => x.Value.State.GetStateValue(CompositionStateKinds.Echo) != 0);
                foreach (var note in notes.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.SoloRelease) == 1))
                {
                    var state = note.Value.State;
                    var program = state.GetStateValue(CompositionStateKinds.Program) is var stated and > 0 ? stated - 1 : ((PitchInstrumentTrack)song.Song.TrackDefinitions[track]).InstrumentCode;
                    var bend = (BendKind)state.GetStateValue(CompositionStateKinds.Bend);
                    await Assert.That(bend is BendKind.None or BendKind.Scoop or BendKind.PrebendRelease).IsTrue().Because($"seed {song.Seed} at {note.Position}");
                    if (!soloists.TryGetValue(role, out var soloist) || !soloist.Vibrates || echoes || !Expression.Bends(program) || note.Value.Duration < 1)
                        continue;
                    releases.Add((state.GetStateValue(CompositionStateKinds.SoloIntensity) / 100.0, bend,
                        state.GetStateValue(CompositionStateKinds.Vibrato) > 0 || state.GetStateValue(CompositionStateKinds.WideVibrato) > 0));
                }
            }
        }

        await Assert.That(releases.Count).IsGreaterThan(20);
        await Assert.That(releases.All(x => x.Vibrato)).IsTrue();
        var ordered = releases.OrderBy(x => x.Intensity).ToArray();
        int Bent(IEnumerable<(double, BendKind Bend, bool)> some) => some.Count(x => x.Bend != BendKind.None);
        await Assert.That(Bent(ordered.TakeLast(ordered.Length / 3))).IsGreaterThan(Bent(ordered.Take(ordered.Length / 3)));
    }

    [Test]
    public async Task APlainSolosPhrases_StartOnTheBar_AWildOnesNowAndThenOff_AndASoloBuildsToItsClimax()
    {
        static bool IsLate(SoloLines.Segment[] plan, int phrase) => plan.First(x => x.Phrase == phrase) is { Gesture: SoloGesture.Pause, IsRelease: false };
        var plain = TestCorpus.InParallel(Enumerable.Range(0, 32), seed => Plans(TestCorpus.Get(seed, Facets(1, 0)))).SelectMany(x => x).ToArray();
        var wild = TestCorpus.InParallel(Enumerable.Range(0, 32), seed => Plans(TestCorpus.Get(seed, Facets(1, 1)))).SelectMany(x => x).ToArray();

        await Assert.That(plain.All(x => x.Plan.Select(s => s.Phrase).Distinct().All(p => !IsLate(x.Plan, p)))).IsTrue();
        await Assert.That(wild.Count(x => x.Plan.Select(s => s.Phrase).Distinct().Any(p => IsLate(x.Plan, p)))).IsGreaterThan(0);

        // the intensity of a solo's first fifth against that about its climax, the fourth
        double Around(IEnumerable<(int, bool, SoloLines.Segment[] Plan)> solos, double from, double to) => solos
            .SelectMany(x => x.Plan.Where(s => s.Gesture != SoloGesture.Pause)
                .Select(s => (Place: (s.Start - x.Plan[0].Start) / (x.Plan[^1].End - x.Plan[0].Start), s.Intensity)))
            .Where(x => x.Place >= from && x.Place < to).Select(x => x.Intensity).DefaultIfEmpty().Average();
        var all = plain.Concat(wild).ToArray();
        await Assert.That(Around(all, 0.6, 0.8)).IsGreaterThan(Around(all, 0, 0.2) + 0.15);
    }

    /// <summary>
    ///     The solos' phrases, by fifth of the melody facet: how long, how many come in late, how many breathe after their
    ///     release, the share of runs that go on over a bar line, and the intensity by fifth of the way through a solo.
    /// </summary>
    [Test]
    [Explicit]
    public async Task PhraseReport()
    {
        static double MelodyOf(CorpusSong song) => ((Unconventionality)song.Trace.Single(t => t.Point == TracePoints.SongUnconventionality).Value!)[Facet.Melody];
        var solos = TestCorpus.Measure(512, song => Plans(song).Select(x => (Fifth: Math.Clamp((int)(MelodyOf(song) * 5), 0, 4), Bar: song.Map.Sections[x.Index].Meter.BarDuration, x.Plan)).ToArray()).SelectMany(x => x).ToArray();
        foreach (var band in solos.GroupBy(x => x.Fifth).OrderBy(x => x.Key))
        {
            var phrases = band.SelectMany(x => x.Plan.GroupBy(s => s.Phrase).Select(p => (x.Bar, Segments: p.ToArray()))).ToArray();
            var lengths = phrases.GroupBy(p => Math.Round((p.Segments[^1].End - p.Segments[0].Start) / p.Bar)).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count() / (double)phrases.Length:P0}");
            var late = phrases.Count(p => p.Segments[0] is { Gesture: SoloGesture.Pause, IsRelease: false });
            var breathe = phrases.Count(p => p.Segments[^1] is { Gesture: SoloGesture.Pause });
            // a run's chain of segments, going on over a bar line
            var chains = band.SelectMany(x => x.Plan.Select((s, i) => (s, i, x.Bar, x.Plan)).Where(c => c.s.Gesture == SoloGesture.Run && !c.s.GoesOn)
                .Select(c => (Start: c.s.Start, End: c.Plan.Skip(c.i).TakeWhile((s, k) => k == 0 || s.GoesOn).Last().End, Origin: x.Plan[0].Start, c.Bar))).ToArray();
            var crossing = chains.Count(c => Math.Floor((c.Start - c.Origin) / c.Bar + 1e-9) != Math.Floor((c.End - c.Origin) / c.Bar - 1e-9));
            var arc = Enumerable.Range(0, 5).Select(f => band.SelectMany(x => x.Plan.Where(s => s.Gesture != SoloGesture.Pause)
                    .Select(s => (Place: (s.Start - x.Plan[0].Start) / (x.Plan[^1].End - x.Plan[0].Start), s.Intensity)))
                .Where(x => x.Place >= f / 5.0 && x.Place < (f + 1) / 5.0).Select(x => x.Intensity).DefaultIfEmpty().Average());
            Console.WriteLine($"  melody {band.Key}/5, {phrases.Length} phrases: {string.Join(", ", lengths)} bars; late {late / (double)phrases.Length:P0}, breathe {breathe / (double)phrases.Length:P0}; " +
                              $"runs over a bar line {crossing / (double)Math.Max(1, chains.Length):P0} of {chains.Length}; intensity by fifth {string.Join(" ", arc.Select(x => $"{x:F2}"))}");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    ///     How fast the solos' runs go, by fifth of the melody facet: the share of their notes at each speed, and the longest
    ///     any run keeps up a speed of 32nds or faster, in beats.
    /// </summary>
    [Test]
    [Explicit]
    public async Task SpeedReport()
    {
        static double MelodyOf(CorpusSong song) => ((Unconventionality)song.Trace.Single(t => t.Point == TracePoints.SongUnconventionality).Value!)[Facet.Melody];
        var drawn = TestCorpus.Measure(512, song => Plans(song).Select(x => (Fifth: Math.Clamp((int)(MelodyOf(song) * 5), 0, 4), x.Plan)).ToArray()).SelectMany(x => x)
            .Concat(TestCorpus.InParallel(Enumerable.Range(0, 64), seed => Plans(TestCorpus.Get(seed, Facets(1, 1))).Select(x => (Fifth: 5, x.Plan)).ToArray()).SelectMany(x => x))
            .ToArray();
        foreach (var band in drawn.GroupBy(x => x.Fifth).OrderBy(x => x.Key))
        {
            var runs = band.SelectMany(x => x.Plan).Where(x => x.Gesture == SoloGesture.Run).ToArray();
            var notes = runs.Sum(x => (x.End - x.Start) * x.Speed);
            var shares = runs.GroupBy(x => x.Speed).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Sum(r => (r.End - r.Start) * r.Speed) / notes:P0}");
            // the longest stretch a run keeps to one fast speed, segment after segment
            var longest = band.Select(x => x.Plan).SelectMany(plan => plan.Select((s, i) => (s, i)).Where(x => x.s.Gesture == SoloGesture.Run && x.s.Speed >= 8)
                .Select(x => plan.Skip(x.i).TakeWhile((s, k) => s.Gesture == SoloGesture.Run && s.Speed == x.s.Speed && (k == 0 || s.GoesOn)).Sum(s => s.End - s.Start))).DefaultIfEmpty().Max();
            Console.WriteLine($"  {(band.Key == 5 ? "melody 1 given" : $"melody {band.Key}/5")}, {runs.Length} run segments: notes at {string.Join(", ", shares)} a beat; longest at 32nds or faster {longest:F1} beats");
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
