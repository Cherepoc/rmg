using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Events;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.Arrangements;

public sealed class SoloTest
{
    /// <summary>
    ///     The chord's own notes at a solo's note, as the chords part plays its shape there, a sus or power chord's without
    ///     the third; the triad's of the note's chord where the band carries no shape.
    /// </summary>
    private static HashSet<int> ChordTones(CorpusSong song, double position, Rmg.Core.Events.StateMap state)
    {
        var band = song.Song.Notes!.GetValueOrDefault(song.Track(TrackRole.Chords))?.LastOrDefault(x => x.Position <= position + 1e-9);
        if (band is { Value: { } chordNote } && !chordNote.State.GetStateValue(Rmg.Core.Events.StateKinds.ChordNotePitchOffsets).IsDefaultOrEmpty
            && chordNote.State.GetStateValue(Rmg.Core.Events.StateKinds.ChordChange) == state.GetStateValue(Rmg.Core.Events.StateKinds.ChordChange))
            return [..Realizer.GetChordNotes(chordNote.State).Classes];
        var (_, _, chord) = Realizer.GetChord(state);
        return [..new[] { 0, 2, 4 }.Select(x => chord.GetPitch(x).Mod(12))];
    }

    private static readonly TrackRole[] Band = [TrackRole.Melody, TrackRole.Chords, TrackRole.Bass, TrackRole.Riff, TrackRole.Drum];

    [Test]
    public async Task ThePlainestSongs_HaveNoSolo_AndTheWildestOneAsLikelyAsNot()
    {
        var context = new GenerationContext(1);
        var plain = Unconventionality.Generate(0, _ => context);
        var wild = Unconventionality.Generate(1, _ => context);

        await Assert.That(Enumerable.Range(0, 1000).All(_ => Solos.Draw(context, 1, SectionRole.Free, plain, Band, Genre.None) is null)).IsTrue();
        var solos = Enumerable.Range(0, 4000).Count(_ => Solos.Draw(context, 1, SectionRole.Free, wild, Band, Genre.None) is not null) / 4000.0;
        await Assert.That(solos).IsEqualTo(0.5).Within(0.03);
        // and never the song's first section
        await Assert.That(Enumerable.Range(0, 1000).All(_ => Solos.Draw(context, 0, SectionRole.Free, wild, Band, Genre.None) is null)).IsTrue();
    }

    /// <summary>A pitched solo never trading with the drums at the melody facet's plain end, as often as Trade has it at the wild; the songs' own never twinned.</summary>
    [Test]
    public async Task APitchedSolo_TradesOnlyAwayFromThePlainEnd_AndTheSongsNeverTwin()
    {
        var context = new GenerationContext(3);
        var solo = new SoloPlan([TrackRole.Solo], SoloMode.Trading, 0, true);
        TrackRole[] parts = [..Band, TrackRole.Solo, TrackRole.Riff];
        await Assert.That(Enumerable.Range(0, 1000).All(_ => Solos.WithTrade(context, solo, 0, parts, Genre.None) == solo)).IsTrue();
        await Assert.That(Enumerable.Range(0, 1000).All(_ => Solos.WithTwin(context, solo, 0, parts, Genre.None) == solo && Solos.WithTwin(context, solo, 1, parts, Genre.None) == solo)).IsTrue();
        var trades = Enumerable.Range(0, 4000).Count(_ => Solos.WithTrade(context, solo, 1, parts, Genre.None)!.TradesWithDrums) / 4000.0;
        await Assert.That(trades).IsEqualTo(Solos.Trade.Wild).Within(0.03);
    }

    [Test]
    public async Task ABridge_SolosTheOftenest_AndAPreChorus_TheLeast()
    {
        var context = new GenerationContext(2);
        var middling = Unconventionality.Generate(0.5, _ => context);
        double Rate(SectionRole role) => Enumerable.Range(0, 8000).Count(_ => Solos.Draw(context, 1, role, middling, Band, Genre.None) is not null) / 8000.0;

        var (bridge, verse, chorus, preChorus) = (Rate(SectionRole.Bridge), Rate(SectionRole.Verse), Rate(SectionRole.Chorus), Rate(SectionRole.PreChorus));
        await Assert.That(bridge).IsGreaterThan(verse);
        await Assert.That(verse).IsGreaterThan(chorus);
        await Assert.That(chorus).IsGreaterThan(preChorus);
    }

    [Test]
    public async Task ASolo_IsPlayedByItsSoloists_TheMelodyResting_AndADrumSolo_ByTheDrumsAlone()
    {
        var (checkedSolos, drumSolos) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Facets: TestCorpus.FacetsOf((Facet.Form, 1))))))
        foreach (var (index, solo) in song.Solos)
        {
            // a melody with no notes there leaves nothing to solo on
            if (song.Trace.Any(x => x.Point == TracePoints.SoloLine && ((int Index, int Notes))x.Value! is { } line && line.Index == index && line.Notes == 0))
                continue;

            var span = song.Map.Sections[index];
            int Notes(TrackRole role) => song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role == role)
                .Sum(x => x.Value.Count(n => n.Position >= span.Start - 1e-9 && n.Position < span.LastBar));
            if (solo.IsDrumSolo)
            {
                foreach (var part in Enum.GetValues<TrackRole>().Where(x => x != TrackRole.Drum))
                    await Assert.That(Notes(part)).IsEqualTo(0).Because($"seed {song.Seed}, {part} in a drum solo");
                drumSolos++;
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
        await Assert.That(drumSolos).IsGreaterThan(0);
    }

    private static SongOverrides Facets(double form, double melody) =>
        new(Facets: TestCorpus.FacetsOf((Facet.Form, form), (Facet.Melody, melody)));

    /// <summary>Every pitched solo's segments, as the trace records them: whether it is its own, and each segment's start, end, intensity and gesture.</summary>
    private static (int Index, bool IsOwn, (double Start, double End, double Intensity, SoloGesture Gesture)[] Segments)[] Gestures(CorpusSong song) =>
    [
        ..Plans(song).Select(x => (x.Index, x.IsOwn, x.Plan.Select(g => (g.Start, g.End, g.Intensity, g.Gesture)).ToArray()))
    ];

    /// <summary>
    ///     A ghosting soloist's ghosts, its muted notes, only in its licks, and Funk's soloists ghosting: measured 31 of 47 of
    ///     64 Funk songs' soloists ghosting, 1.8% of their notes on the average and 10% at the most.
    /// </summary>
    [Test]
    public async Task ASoloistsGhosts_AreInItsLicksAlone()
    {
        var ghosts = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Rmg.Tests.Genres.GenrePresets.Genre("funk")))))
        foreach (var (index, _, plan) in Plans(song))
        {
            var solo = song.Solos[index];
            if (solo.Soloists.Length > 1 || solo.Soloists[0] == TrackRole.Drum)
                continue;
            var span = song.Map.Sections[index];
            var notes = song.Song.Notes![song.Track(solo.Soloists[0])].Where(x => x.IsWithin(span.Start, span.End) && x.Value.State.GetStateValue(CompositionStateKinds.PalmMute) > 0
                                                                                  && (solo.Soloists[0] == TrackRole.Solo || x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) != 0));
            foreach (var note in notes)
            {
                ghosts++;
                await Assert.That(plan.Any(x => x.Gesture == SoloGesture.Lick && note.Position >= x.Start - 1e-9 && note.Position < x.End - 1e-9)).IsTrue().Because($"seed {song.Seed} at {note.Position}");
            }
        }

        Console.WriteLine($"{ghosts} ghosts");
        await Assert.That(ghosts).IsGreaterThan(0);
    }

    /// <summary>Every pitched solo's plan, as the trace records it, and whether it is its own.</summary>
    private static (int Index, bool IsOwn, SoloLines.Segment[] Plan)[] Plans(CorpusSong song) =>
        [..song.Trace.Where(x => x.Point == TracePoints.SoloGestures).Select(x => ((int, bool, SoloLines.Segment[]))x.Value!)];

    /// <summary>Soloists trading take whole phrases in turn: no phrase of the line runs over a turn's end.</summary>
    [Test]
    public async Task TradingSoloists_TakeWholePhrases_NoneRunningOverItsTurn()
    {
        var trading = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, Facets(1, 1))))
        foreach (var (index, _, plan) in Plans(song))
        {
            if (song.Solos[index] is not { Mode: SoloMode.Trading, Soloists.Length: > 1 })
                continue;
            trading++;
            var span = song.Map.Sections[index];
            var turn = SoloPlan.TurnOf(span);
            int TurnOf(double position) => (int)Math.Floor((position - span.Start) / turn + 1e-9);
            foreach (var phrase in plan.GroupBy(x => x.Phrase))
                await Assert.That(TurnOf(phrase.Max(x => x.End) - 1e-6)).IsEqualTo(TurnOf(phrase.Min(x => x.Start))).Because($"seed {song.Seed}, phrase {phrase.Key}");
        }

        Console.WriteLine($"{trading} solos trading");
        await Assert.That(trading).IsGreaterThan(0);
    }

    [Test]
    public async Task APlainSolo_IsOnTheMelody_AndAWildOne_ItsOwn()
    {
        var plain = TestCorpus.InParallel(Enumerable.Range(0, 32), seed => Gestures(TestCorpus.Get(seed, Facets(1, 0)))).SelectMany(x => x).ToArray();
        var wild = TestCorpus.InParallel(Enumerable.Range(0, 32), seed => Gestures(TestCorpus.Get(seed, Facets(1, 1)))).SelectMany(x => x).ToArray();

        await Assert.That(plain.Length).IsGreaterThan(0);
        await Assert.That(plain.All(x => !x.IsOwn)).IsTrue();
        await Assert.That(wild.Length).IsGreaterThan(0);
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
                // a run up or down the scale, a step of it at most three semitones, harmonic minor's augmented second, the
                // others leaping by their figures; but across a change of chord, whose scale the run takes up from there
                if (gesture == SoloGesture.Run && shapes.GetValueOrDefault(Math.Round(start, 6)) == SoloShape.Scale && scaleRuns++ >= 0)
                    foreach (var (a, b) in played.Zip(played.Skip(1)).Where(x => x.First.Value.State.GetStateValue(StateKinds.ChordChange) == x.Second.Value.State.GetStateValue(StateKinds.ChordChange)))
                        await Assert.That(Math.Abs(b.Value.Pitches[0] - a.Value.Pitches[0])).IsLessThanOrEqualTo(3).Because($"seed {song.Seed} at {b.Position}");
                if (gesture == SoloGesture.Held && played.Length > 0)
                {
                    held++;
                    var tones = ChordTones(song, played[0].Position, played[0].Value.State);
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
        var soloists = song.Soloists;
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
            var beatsASecond = Meter.BeatsPerSecond(song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(run.Start).GetStateValue(StateKinds.Tempo));
            var shortest = run.Notes.Zip(run.Notes.Skip(1), (a, b) => b.Position - a.Position).Min();
            await Assert.That(beatsASecond / shortest).IsLessThanOrEqualTo(Math.Max(run.Soloist.MaxNotesASecond, run.Soloist.Speeds[0] * beatsASecond) + 1e-6);
        }
    }

    /// <summary>The solos' runs, by fifth of the melody facet: the sizes of their cells, their shapes, and the share grouping cells of several sizes.</summary>
    [Test]
    [Explicit]
    public async Task CellReport()
    {
        static double MelodyOf(CorpusSong song) => song.Unconventionality[Facet.Melody];
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
    private static SongOverrides Wild => new(Facets: TestCorpus.FacetsOf((Facet.Form, 1), (Facet.Melody, 1), (Facet.Sound, 1)));

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

    /// <summary>
    ///     Every solo of one soloist in the songs: its soloist's average velocity there as rendered, and the loudest other
    ///     pitched part's, the melody resting; a note of several pitches as loud as one, as its pitches are rendered the
    ///     softer to sound so.
    /// </summary>
    private static (int Seed, TrackRole Soloist, double Solo, double Band)[] Loudness(IEnumerable<CorpusSong> songs) =>
    [
        ..songs.SelectMany(song => song.Solos.Where(x => !x.Value.IsDrumSolo && x.Value.Soloists.Length == 1).Select(x =>
        {
            var span = song.Map.Sections[x.Key];
            var parts = song.Song.Notes!.Where(n => song.Song.TrackDefinitions[n.Key] is PitchInstrumentTrack)
                .Select(n => (song.Song.TrackDefinitions[n.Key].Role, Loudness: n.Value.Where(v => v.Position >= span.Start - 1e-9 && v.Position < span.End - 1e-9)
                    .Select(v => Rmg.Core.Rendering.Render.ToMidiVelocity(v.Value.Velocity)).DefaultIfEmpty(double.NaN).Average()))
                .Where(n => !double.IsNaN(n.Loudness))
                .ToArray();
            var solo = parts.Where(n => n.Role == x.Value.Soloists[0]).Select(n => n.Loudness).DefaultIfEmpty(double.NaN).First();
            var band = parts.Where(n => n.Role != x.Value.Soloists[0]).Select(n => n.Loudness).DefaultIfEmpty(0).Max();
            return (song.Seed, x.Value.Soloists[0], solo, band);
        }).Where(x => !double.IsNaN(x.solo)))
    ];

    [Test]
    public async Task ALoneSoloist_PlaysAtLeastAsLoudAsTheLoudestOfItsBand()
    {
        var solos = Loudness(TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, Facets(1, 0.5))));

        await Assert.That(solos.Length).IsGreaterThan(20);
        foreach (var (seed, soloist, solo, band) in solos)
            await Assert.That(solo).IsGreaterThanOrEqualTo(band).Because($"seed {seed}, {soloist}");
    }

    /// <summary>How loud a lone soloist plays over the loudest part of its band, as rendered, over the corpus.</summary>
    [Test]
    [Explicit]
    public async Task LoudnessReport()
    {
        var solos = Loudness(TestCorpus.Range(1024));
        Console.WriteLine($"{solos.Length} solos of one soloist: the soloist {solos.Average(x => x.Solo):F3}, the band's loudest part {solos.Average(x => x.Band):F3}, " +
                          $"the soloist the quieter {solos.Count(x => x.Solo < x.Band) / (double)solos.Length:P0}, by 0.05 or more {solos.Count(x => x.Solo < x.Band - 0.05) / (double)solos.Length:P0}, " +
                          $"the most {solos.Max(x => x.Band - x.Solo):+0.000;-0.000}");
        foreach (var g in solos.GroupBy(x => x.Soloist).OrderByDescending(g => g.Count()))
            Console.WriteLine($"  {g.Key}: {g.Count()}, {g.Average(x => x.Solo - x.Band):+0.000;-0.000} over the band");
        await Task.CompletedTask;
    }

    [Test]
    public async Task TheCounterMelodyAndThePad_MakeRoomForASolo()
    {
        var checkedSolos = new Dictionary<TrackRole, int> { [TrackRole.CounterMelody] = 0, [TrackRole.Pad] = 0 };
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, Facets(1, 0.5))))
        foreach (var (index, solo) in song.Solos.Where(x => !x.Value.IsDrumSolo))
        {
            var span = song.Map.Sections[index];
            int NotesOf(TrackRole role, Func<double, bool> where) => song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role == role).Sum(x => x.Value.Count(n => where(n.Position)));
            bool In(double position) => position >= span.Start - 1e-9 && position < span.End - 1e-9;
            // a part the song plays elsewhere, which rests in the solo
            foreach (var role in checkedSolos.Keys.ToArray().Where(x => !solo.Soloists.Contains(x) && NotesOf(x, p => !In(p)) > 0))
            {
                await Assert.That(NotesOf(role, In)).IsEqualTo(0).Because($"seed {song.Seed}, {role} in a solo");
                checkedSolos[role]++;
            }
        }

        Console.WriteLine($"solos checked: {string.Join(", ", checkedSolos.Select(x => $"{x.Key} {x.Value}"))}");
        await Assert.That(checkedSolos.Values.All(x => x > 5)).IsTrue();
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
        static double SoundOf(CorpusSong song) => song.Unconventionality[Facet.Sound];
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
            var soloists = song.Soloists;
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
                    var beatsASecond = Meter.BeatsPerSecond(common.GetEffectiveStateMapAt(segment.Start).GetStateValue(Rmg.Core.Events.StateKinds.Tempo));
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
            // a bar's strong beats, its groups' starts at every length of bar
            double[] strong = [..span.Meter.Levels[1].Select(x => x.Start / 4.0)];
            foreach (var phrase in plan.GroupBy(x => x.Phrase))
            {
                var release = phrase.Last(x => x.Gesture != SoloGesture.Pause);
                await Assert.That(release.IsRelease).IsTrue().Because($"seed {song.Seed} at {release.Start}");
                var beat = (release.Start - span.Start).Mod(span.Meter.BarDuration);
                await Assert.That(strong.Any(x => Math.Abs(x - beat) < 1e-6 || Math.Abs(x + span.Meter.BarDuration - beat) < 1e-6)).IsTrue().Because($"seed {song.Seed} at {release.Start}");
                await Assert.That(release.End - release.Start).IsGreaterThanOrEqualTo(1 - 1e-9);
            }

            // its note the chord's, marked as a release, on the first soloist's part
            var soloist = song.Track(song.Solos[index].Soloists[0]);
            foreach (var note in song.Song.Notes![soloist].Where(x => x.Value.State.GetStateValue(CompositionStateKinds.SoloRelease) == 1 && x.Position >= span.Start && x.Position < span.End))
            {
                releases++;
                if (ChordTones(song, note.Position, note.Value.State).Contains(note.Value.Pitches[0].Mod(12)))
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
            var soloists = song.Soloists;
            foreach (var (track, notes) in song.Song.Notes!)
            {
                var role = song.Song.TrackDefinitions[track].Role;
                // a soloist that sings one, on an instrument that bends, playing a line, but where it echoes
                var echoes = notes.Any(x => x.Value.State.GetStateValue(CompositionStateKinds.Echo) != 0);
                foreach (var note in notes.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.SoloRelease) == 1))
                {
                    var state = note.Value.State;
                    var program = InstrumentsOverTime.ProgramOf(state, ((PitchInstrumentTrack)song.Song.TrackDefinitions[track]).InstrumentCode);
                    var bend = (BendKind)state.GetStateValue(CompositionStateKinds.Bend);
                    await Assert.That(bend is BendKind.None or BendKind.Scoop or BendKind.PrebendRelease).IsTrue().Because($"seed {song.Seed} at {note.Position}");
                    // nor a stacked one, a chord rather than a line
                    if (!soloists.TryGetValue(role, out var soloist) || !soloist.Vibrates || echoes || !Expression.Bends(program) || note.Value.Duration < 1 || note.Value.Pitches.Length > 1)
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
        static double MelodyOf(CorpusSong song) => song.Unconventionality[Facet.Melody];
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
    ///     What a solo can do and does, at its form, melody and sound facets given together, from fairly plain to the wildest:
    ///     how many songs solo, the soloists' vocabularies, how dense the solos play, how much of them is runs and how fast,
    ///     their gestures, and how their long notes, an 8th or longer, are bent and sung.
    /// </summary>
    [Test]
    [Explicit]
    public async Task CapabilityReport()
    {
        foreach (var value in new[] { 0.25, 0.5, 0.75, 1.0 })
        {
            var overrides = new SongOverrides(Facets: TestCorpus.FacetsOf((Facet.Form, value), (Facet.Melody, value), (Facet.Sound, value)));
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, overrides));
            var soloists = songs.SelectMany(s => s.Soloists.Values).ToArray();
            var plans = songs.SelectMany(Plans).ToArray();
            var segments = plans.SelectMany(x => x.Plan).ToArray();
            var runs = segments.Where(x => x.Gesture == SoloGesture.Run).ToArray();
            var notes = songs.SelectMany(s => s.Song.Notes!.Values.SelectMany(n => n).Where(n => n.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 1)).Select(n => n.Value).ToArray();
            var longNotes = notes.Where(n => n.Duration >= 0.5).ToArray();
            var beats = plans.Sum(x => x.Plan[^1].End - x.Plan[0].Start);
            string Share(double part, double whole) => $"{part / Math.Max(1, whole):P0}";
            string Has(Func<Soloist, bool> has) => Share(soloists.Count(has), soloists.Length);
            Console.WriteLine($"  facets {value}: songs with a pitched solo {Share(songs.Count(s => s.Solos.Values.Any(x => !x.IsDrumSolo)), songs.Length)}, " +
                              $"solos a song {plans.Length / (double)songs.Length:F2}, own {Share(plans.Count(x => x.IsOwn), plans.Length)}");
            Console.WriteLine($"    vocabulary of {soloists.Length}: speeds {string.Join(" ", new[] { 3, 6, 8, 12, 16 }.Select(x => $"{x}:{Has(s => s.Speeds.Contains(x))}"))}; " +
                              $"cells of 5 or more {Has(s => s.CellSizes.Any(c => c >= 5))}, grouped {Has(s => s.Groups)}; stamina {soloists.Average(x => x.Stamina):F1} beats, " +
                              $"cap {soloists.Average(x => x.MaxNotesASecond):F1} a second; volatility {soloists.Average(x => x.Volatility):F2}; bends {Has(s => s.Bends)}, vibrato {Has(s => s.Vibrates)}");
            Console.WriteLine($"    played: {notes.Length / Math.Max(1, beats):F2} notes a beat, runs {Share(runs.Sum(x => x.End - x.Start), beats)} of the time, " +
                              $"at {string.Join(" ", runs.GroupBy(x => x.Speed).OrderBy(x => x.Key).Select(x => $"{x.Key}:{Share(x.Count(), runs.Length)}"))} a beat; " +
                              $"gestures {string.Join(" ", segments.GroupBy(x => x.Gesture).OrderBy(x => x.Key).Select(x => $"{x.Key}:{Share(x.Count(), segments.Length)}"))}");
            Console.WriteLine($"    {longNotes.Length} long notes: bent {Share(longNotes.Count(n => (BendKind)n.State.GetStateValue(CompositionStateKinds.Bend) is not (BendKind.None or BendKind.Slide)), longNotes.Length)}, " +
                              $"slid {Share(longNotes.Count(n => (BendKind)n.State.GetStateValue(CompositionStateKinds.Bend) == BendKind.Slide), longNotes.Length)}, " +
                              $"a vibrato {Share(longNotes.Count(n => n.State.GetStateValue(CompositionStateKinds.Vibrato) > 0 || n.State.GetStateValue(CompositionStateKinds.WideVibrato) > 0), longNotes.Length)}, " +
                              $"a wide one {Share(longNotes.Count(n => n.State.GetStateValue(CompositionStateKinds.WideVibrato) > 0), longNotes.Length)}");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    ///     The share of a song's solos' phrases opening on the phrase before's first cell moved by a step: their first
    ///     run's or lick's first cell of the same contour, its intervals each within a semitone, a step's move on the scale.
    /// </summary>
    private static (int Answering, int Phrases) Motifs(CorpusSong song)
    {
        var (answering, phrases) = (0, 0);
        var cells = song.Trace.Where(x => x.Point == TracePoints.SoloCells).Select(x => ((double Start, SoloShape, int[] Cells))x.Value!).ToDictionary(x => Math.Round(x.Start, 6), x => x.Cells);
        foreach (var (index, _, plan) in Plans(song).Where(x => song.Solos[x.Index].Soloists.Length == 1))
        {
            var notes = song.Song.Notes!.Single(x => song.Song.TrackDefinitions[x.Key].Role == song.Solos[index].Soloists[0]).Value;
            int[]? before = null;
            foreach (var opening in plan.Where(x => x.Gesture is SoloGesture.Run or SoloGesture.Lick).GroupBy(x => x.Phrase).Select(x => x.First()))
            {
                if (!cells.TryGetValue(Math.Round(opening.Start, 6), out var sizes) || sizes.Length == 0)
                    continue;

                var pitches = notes.Where(x => x.Position >= opening.Start - 1e-9 && x.Position < opening.End - 1e-9).Take(sizes[0]).Select(x => x.Value.Pitches[0]).ToArray();
                int[] intervals = [..pitches.Zip(pitches.Skip(1), (a, b) => b - a)];
                if (before is not null && intervals.Length > 0 && intervals.Length == before.Length)
                {
                    phrases++;
                    // moved a step, its intervals about the same, or inverted, about the same mirrored
                    if (intervals.Zip(before).All(x => Math.Sign(x.First) == Math.Sign(x.Second) && Math.Abs(x.First - x.Second) <= 1)
                        || intervals.Zip(before).All(x => Math.Sign(x.First) == -Math.Sign(x.Second) && Math.Abs(x.First + x.Second) <= 1))
                        answering++;
                }

                before = intervals;
            }
        }

        return (answering, phrases);
    }

    [Test]
    public async Task ASolosPhrase_OpensOnThePhraseBeforesMotif_MovedAStepOrInverted_NowAndThen()
    {
        var motifs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => Motifs(TestCorpus.Get(seed, Facets(1, 0.5))));
        var (answering, phrases) = (motifs.Sum(x => x.Answering), motifs.Sum(x => x.Phrases));

        await Assert.That(phrases).IsGreaterThan(50);
        // a calibration, its bound against the answers gone: 38% where no phrase answers, a run of one shape after
        // another's alike by itself, about 0.47 with them (October 2026)
        Console.WriteLine($"phrases opening on the motif before: {answering / (double)phrases:P0} of {phrases}");
        await Assert.That(answering / (double)phrases).IsGreaterThan(0.42);
    }

    [Test]
    public async Task ASoloInOctaves_IsItsSoloistsChoice_ItsRunsAlone()
    {
        // the solos of one soloist, whose line is its own, each note with its soloist and the gesture it plays in
        var notes = TestCorpus.InParallel(Enumerable.Range(0, 128), seed =>
        {
            var song = TestCorpus.Get(seed, Facets(1, 1));
            var soloists = song.Soloists;
            return Plans(song).Where(x => song.Solos[x.Index].Soloists.Length == 1).SelectMany(plan =>
            {
                var role = song.Solos[plan.Index].Soloists[0];
                return song.Song.Notes!.Single(x => song.Song.TrackDefinitions[x.Key].Role == role).Value
                    .Where(n => n.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 1 && n.Position >= plan.Plan[0].Start - 1e-9 && n.Position < plan.Plan[^1].End - 1e-9)
                    .Select(n => (Soloist: soloists[role], plan.Plan.Last(g => g.Start <= n.Position + 1e-9).Gesture, Note: n.Value));
            }).ToArray();
        }).SelectMany(x => x).ToArray();
        var octaves = notes.Where(x => x.Note.State.GetStateValue(CompositionStateKinds.Octave) > 0).ToArray();

        await Assert.That(octaves.Length).IsGreaterThan(0);
        await Assert.That(octaves.All(x => x.Gesture != SoloGesture.Run)).IsTrue();
        await Assert.That(octaves.All(x => x.Soloist.Octaves)).IsTrue();
    }

    /// <summary>Where the solos fall, by the role of the section they play: the share of the solos, and of the role's later appearances that solo.</summary>
    [Test]
    [Explicit]
    public async Task RoleReport()
    {
        var appearances = TestCorpus.Measure(1024, song =>
        {
            var structure = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
            return song.Map.Sections.Select((span, index) => (Role: structure.Roles.GetValueOrDefault(span.SectionId, SectionRole.Free), Index: index, Solo: song.Solos.ContainsKey(index))).Skip(1).ToArray();
        }).SelectMany(x => x).ToArray();
        var solos = appearances.Where(x => x.Solo).ToArray();
        Console.WriteLine($"  {solos.Length} solos: {string.Join(", ", appearances.GroupBy(x => x.Role).OrderBy(x => x.Key).Select(g => $"{g.Key} {g.Count(x => x.Solo) / (double)solos.Length:P0} of them, {g.Count(x => x.Solo) / (double)g.Count():P1} of its appearances"))}");
        await Task.CompletedTask;
    }

    /// <summary>Every note of a song's pitched solos, with its soloist and the gesture it plays in.</summary>
    private static (Soloist Soloist, SoloGesture Gesture, RealizedNote Note)[] SoloistNotes(CorpusSong song)
    {
        var soloists = song.Soloists;
        return
        [
            ..Plans(song).SelectMany(plan => song.Song.Notes!
                .Where(x => song.Solos[plan.Index].Soloists.Contains(song.Song.TrackDefinitions[x.Key].Role))
                .SelectMany(x => x.Value
                    .Where(n => n.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 1 && n.Position >= plan.Plan[0].Start - 1e-9 && n.Position < plan.Plan[^1].End - 1e-9)
                    .Select(n => (soloists[song.Song.TrackDefinitions[x.Key].Role], plan.Plan.Last(g => g.Start <= n.Position + 1e-9).Gesture, n.Value))))
        ];
    }

    private static bool IsStacked(RealizedNote note) => note.Pitches.Length > 1 && note.State.GetStateValue(CompositionStateKinds.LineVoices) > 1;

    [Test]
    public async Task ASoloistOnAnInstrumentThatPlaysChords_StacksItsHeldNotesMoreThanItsRuns_TheBandsChordUnderItsOwnNote()
    {
        var notes = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => SoloistNotes(TestCorpus.Get(seed, Facets(1, 0.5)))).SelectMany(x => x).ToArray();
        var plain = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => SoloistNotes(TestCorpus.Get(seed, Facets(1, 0)))).SelectMany(x => x).ToArray();

        // only a soloist on an instrument that plays chords stacks, and a plain one never planes
        await Assert.That(notes.Concat(plain).Where(x => IsStacked(x.Note)).All(x => x.Soloist.ChordRate > 0)).IsTrue().Because("chord instruments");
        await Assert.That(plain.All(x => !x.Soloist.Planes)).IsTrue().Because("plain planes");
        var chordal = notes.Where(x => x.Soloist.ChordRate > 0).ToArray();
        double Stacked(SoloGesture gesture) => chordal.Count(x => x.Gesture == gesture && IsStacked(x.Note)) / (double)Math.Max(1, chordal.Count(x => x.Gesture == gesture));
        await Assert.That(Stacked(SoloGesture.Held)).IsGreaterThan(Stacked(SoloGesture.Run) * 2);

        var (following, planing) = (0, 0);
        foreach (var (_, _, note) in notes.Concat(plain).Where(x => IsStacked(x.Note)))
        {
            // its own note on top, no more notes than its voices, the line's soloist's, who may not be the one playing it
            await Assert.That(note.Pitches.Skip(1).All(x => x < note.Pitches[0])).IsTrue().Because($"top {string.Join(",", note.Pitches)}");
            await Assert.That(note.Pitches.Length).IsLessThanOrEqualTo(note.State.GetStateValue(CompositionStateKinds.LineVoices));
            if (note.State.GetStateValue(CompositionStateKinds.LinePlanes) > 0)
            {
                planing++;
                continue;
            }

            // the band's chord, its note the chord's, the others a minor third or more under it: in whatever key a key
            // change moved it to, its notes as far from its top as the chord's are from one of them
            following++;
            var (_, _, classes) = Realizer.GetChordNotes(note.State);
            await Assert.That(classes.Any(top => note.Pitches.All(x => classes.Contains((x - note.Pitches[0] + top).Mod(12))))).IsTrue().Because($"chord {string.Join(",", note.Pitches)} of {string.Join(",", classes)}");
            await Assert.That(note.Pitches.Skip(1).All(x => note.Pitches[0] - x >= 3)).IsTrue().Because($"gap {string.Join(",", note.Pitches)}");
        }

        await Assert.That(following).IsGreaterThan(50);
        await Assert.That(planing).IsGreaterThan(0);
    }

    [Test]
    public async Task AChordSoloistsApproach_IsAPlanedStackedNote_NeverAPlainSoloists()
    {
        var notes = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => SoloistNotes(TestCorpus.Get(seed, Facets(1, 0.5)))).SelectMany(x => x).ToArray();
        var plain = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => SoloistNotes(TestCorpus.Get(seed, Facets(1, 0)))).SelectMany(x => x).ToArray();

        // a plain soloist neither approaches nor planes; a middling one that does not plane planes its approaches alone (a
        // note's line may be another soloist's, so the notes are counted, not their players)
        await Assert.That(plain.All(x => !x.Soloist.Approaches && x.Note.State.GetStateValue(CompositionStateKinds.LinePlanes) == 0)).IsTrue();
        var approaches = notes.Where(x => !x.Soloist.Planes && IsStacked(x.Note) && x.Note.State.GetStateValue(CompositionStateKinds.LinePlanes) > 0).ToArray();
        // a floor against the mechanism breaking, which a few songs move (10 at offset 942000000)
        await Assert.That(approaches.Length).IsGreaterThan(4);
    }

    /// <summary>How a solo stacks its notes, by its facets: the soloists that can and that plane, and the share of their notes stacked by gesture and by voices.</summary>
    [Test]
    [Explicit]
    public async Task ChordReport()
    {
        foreach (var value in new[] { 0.0, 0.25, 0.5, 0.75, 1.0 })
        {
            var notes = TestCorpus.InParallel(Enumerable.Range(0, 128), seed => SoloistNotes(TestCorpus.Get(seed, Facets(1, value)))).SelectMany(x => x).ToArray();
            var soloists = notes.Select(x => x.Soloist).Distinct().ToArray();
            var chordal = notes.Where(x => x.Soloist.ChordRate > 0).ToArray();
            var stacked = chordal.Where(x => IsStacked(x.Note)).ToArray();
            string Share(double part, double whole) => $"{part / Math.Max(1, whole):P0}";
            Console.WriteLine($"  melody {value}: {soloists.Length} soloists, {Share(soloists.Count(x => x.ChordRate > 0), soloists.Length)} on chords, of those planing {Share(soloists.Count(x => x.ChordRate > 0 && x.Planes), soloists.Count(x => x.ChordRate > 0))}; " +
                              $"their notes stacked {Share(stacked.Length, chordal.Length)}, by gesture {string.Join(" ", chordal.GroupBy(x => x.Gesture).OrderBy(x => x.Key).Select(g => $"{g.Key}:{Share(g.Count(x => IsStacked(x.Note)), g.Count())}"))}; " +
                              $"of {stacked.Length} stacked {string.Join(" ", stacked.GroupBy(x => x.Note.Pitches.Length).OrderBy(x => x.Key).Select(g => $"{g.Key} notes:{Share(g.Count(), stacked.Length)}"))}, " +
                              $"planed by a soloist that does not plane, an approach, {Share(stacked.Count(x => !x.Soloist.Planes && x.Note.State.GetStateValue(CompositionStateKinds.LinePlanes) > 0), stacked.Length)}");
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
        static double MelodyOf(CorpusSong song) => song.Unconventionality[Facet.Melody];
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
        static double MelodyOf(CorpusSong song) => song.Unconventionality[Facet.Melody];
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
        foreach (var band in songs.GroupBy(x => Math.Clamp((int)(x.Unconventionality[Facet.Form] * 5), 0, 4)).OrderBy(x => x.Key))
            Console.WriteLine($"  form {band.Key}/5, {band.Count()} songs: with a solo {band.Count(x => !x.Solos.IsEmpty) / (double)band.Count():P0}, solos a song {band.Average(x => x.Solos.Count):F2}");
        foreach (var band in songs.GroupBy(x => Math.Clamp((int)(x.Unconventionality[Facet.Melody] * 5), 0, 4)).OrderBy(x => x.Key))
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
