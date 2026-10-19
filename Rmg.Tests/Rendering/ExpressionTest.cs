using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.Rendering;

public sealed class ExpressionTest
{
    private static SongOverrides Sound(double sound) => new(Facets: TestCorpus.FacetsOf((Facet.Sound, sound)));

    [Test]
    public async Task ThePlainestSongs_PlayAReverbAlone()
    {
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 8), seed => TestCorpus.Get(seed, Sound(0))))
        {
            await Assert.That(PartEffects(song).Sum(x => x.Count(has => has))).IsEqualTo(0);
            await Assert.That(song.Rendered.Tracks.All(x => x.Controllers.Any(c => c.Controller == ExpressionRender.ReverbController))).IsTrue();
        }
    }

    /// <summary>
    ///     The plainest sound plays no part's tone, no tape, no pump and no roll; the plainest form no heavy or calm section
    ///     and no mix filtered.
    /// </summary>
    [Test]
    public async Task ThePlainestSoundAndForm_PlayNoneOfTheirDevices()
    {
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 8), seed => TestCorpus.Get(seed, new SongOverrides(Facets: TestCorpus.FacetsOf((Facet.Sound, 0), (Facet.Form, 0))))))
        {
            var notes = song.Song.Notes!.SelectMany(x => x.Value.Select(n => (Track: x.Key, Note: n.Value))).ToArray();
            await Assert.That(notes.All(x => x.Note.State.GetStateValue(CompositionStateKinds.Tone) == 0 && x.Note.State.GetStateValue(CompositionStateKinds.Wow) == 0
                                             && x.Note.State.GetStateValue(CompositionStateKinds.Detune) == 0 && x.Note.State.GetStateValue(CompositionStateKinds.Pump) == 0)).IsTrue().Because($"seed {song.Seed}");
            // a roll, a chord that a piano spreads, as a strum is a guitar's own
            await Assert.That(notes.Where(x => song.Song.TrackDefinitions[x.Track] is PitchInstrumentTrack { InstrumentCode: var program } && !InstrumentTraits.Has(InstrumentsOverTime.ProgramOf(x.Note.State, program), InstrumentTrait.Strums))
                .All(x => x.Note.State.GetStateValue(CompositionStateKinds.Strum) == 0)).IsTrue().Because($"seed {song.Seed}");
            await Assert.That(song.Trace.Where(x => x.Point == TracePoints.SectionForm).All(x => (SectionFormKind)x.Value! == SectionFormKind.Own)).IsTrue().Because($"seed {song.Seed}");
            await Assert.That(song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(CompositionStateKinds.MixClosed).All(x => x.Value == 0)).IsTrue().Because($"seed {song.Seed}");
        }
    }

    [Test]
    public async Task TheWildestSongs_PlayAStrongEffect_OthersBesideItNowAndThen_AndTheGentleOnesTogether()
    {
        var wild = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => PartEffects(TestCorpus.Get(seed, Sound(1))));
        var strong = Effects.Select((e, i) => (e, i)).Where(x => x.e.IsStrong).Select(x => x.i).ToArray();
        var gentle = Effects.Select((e, i) => (e, i)).Where(x => !x.e.IsStrong).Select(x => x.i).ToArray();

        // most wildest songs playing a strong effect, every one drawing one but where no part of it may carry it
        var (striking, two) = (wild.Count(song => strong.Any(i => song.Any(p => p[i]))), wild.Count(song => strong.Count(i => song.Any(p => p[i])) >= 2));
        Console.WriteLine($"of 64 wildest songs, {striking} play a strong effect, {two} two or more");
        // measured 57 to 60 of 64, 32 to 34 two or more, at offsets 0 and a million
        await Assert.That(striking).IsGreaterThan(wild.Length * 3 / 4);
        await Assert.That(wild.Any(song => strong.Count(i => song.Any(p => p[i])) >= 2)).IsTrue();
        await Assert.That(wild.SelectMany(x => x).Any(p => gentle.Count(i => p[i]) >= 2)).IsTrue();
    }

    /// <summary>A tremolo moves a held note alone, a beat or longer, as a gate chops a held chord, never a run's notes.</summary>
    [Test]
    public async Task ATremolo_MovesHeldNotesAlone()
    {
        var trembling = new Genre(ImmutableDictionary<string, ImmutableDictionary<string, Rmg.Core.Probabilities.ByConvention>>.Empty
            .Add(GenreChoices.StrongEffect.Name, ImmutableDictionary<string, Rmg.Core.Probabilities.ByConvention>.Empty.Add(GenreChoices.Name(nameof(StrongEffect.Tremolo)), new(1, 1, 1))));
        var held = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: trembling))))
        foreach (var note in song.Song.Notes!.Values.SelectMany(x => x).Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Tremolo) > 0))
        {
            await Assert.That(note.Value.Duration).IsGreaterThanOrEqualTo(1 - 1e-9).Because($"seed {song.Seed} at {note.Position}");
            held++;
        }

        await Assert.That(held).IsGreaterThan(0);
    }

    [Test]
    public async Task ThePartThatEchoes_NeitherSingsAVibrato_NorBends()
    {
        var parts = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => PartEffects(TestCorpus.Get(seed, Sound(1)))).SelectMany(x => x).ToArray();
        var (vibrato, bends, echo) = (Array.FindIndex(Effects, x => x.Name == "vibrato"), Array.FindIndex(Effects, x => x.Name == "bends"), Array.FindIndex(Effects, x => x.Name == "echo"));

        await Assert.That(parts.Count(p => p[echo])).IsGreaterThan(0);
        await Assert.That(parts.Count(p => p[echo] && (p[vibrato] || p[bends]))).IsEqualTo(0);
    }

    /// <summary>
    ///     Every bend of a song, by its part: its size in scale steps, up or down, and the energy of its section, as the
    ///     trace of the octaves records it, where a part that may play them plays.
    /// </summary>
    private static (int Track, int Steps, double? Energy)[] Bends(CorpusSong song)
    {
        var energies = song.Trace.Where(x => x.Point == TracePoints.Octave).Select(x => ((int Index, TrackRole, double Energy, bool))x.Value!)
            .GroupBy(x => x.Index).ToDictionary(x => x.Key, x => x.First().Energy);
        int? SectionOf(double position) => song.Map.Sections.Select((x, i) => (x, i)).Where(x => position >= x.x.Start - 1e-9 && position < x.x.End - 1e-9).Select(x => (int?)x.i).FirstOrDefault();
        return song.Song.Notes!
            .SelectMany(track => track.Value.Select(note => (track.Key, note)))
            // a slide is as far as the note before, and a dive an octave, not a size
            .Where(x => x.note.Value.State.GetStateValue(CompositionStateKinds.BendDepth) != 0
                        && (BendKind)x.note.Value.State.GetStateValue(CompositionStateKinds.Bend) is not (BendKind.Slide or BendKind.Dive))
            .Select(x =>
            {
                var depth = x.note.Value.State.GetStateValue(CompositionStateKinds.BendDepth);
                var steps = Enumerable.Range(1, 2).Select(n => n * Math.Sign(depth)).First(n => Expression.ScaleDistance(x.note.Value, n) == depth);
                return (x.Key, steps, SectionOf(x.note.Position) is { } i && energies.TryGetValue(i, out var energy) ? energy : (double?)null);
            })
            .ToArray();
    }

    [Test]
    public async Task ABend_StartsAndEndsOnTheScale_AStepOrTwoAway()
    {
        var bent = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, Sound(1))))
        foreach (var note in song.Song.Notes!.Values.SelectMany(x => x))
        {
            var kind = (BendKind)note.Value.State.GetStateValue(CompositionStateKinds.Bend);
            var depth = note.Value.State.GetStateValue(CompositionStateKinds.BendDepth);
            // but a slide, from the note before, and a dive, a whammy bar's drop by its own depth
            if (kind is BendKind.None or BendKind.Slide or BendKind.Dive)
                continue;

            var direction = kind is BendKind.BendAndRelease or BendKind.PrebendRelease ? 1 : -1;
            await Assert.That(depth == Expression.ScaleDistance(note.Value, direction) || depth == Expression.ScaleDistance(note.Value, 2 * direction)).IsTrue();
            await Assert.That(Math.Abs(depth)).IsBetween(1, Expression.WidestBend);
            bent++;
        }

        await Assert.That(bent).IsGreaterThan(50);
    }

    [Test]
    public async Task AWildSong_Prebends_BothWays_AndAPlainOneNever()
    {
        static BendKind[] Kinds(CorpusSong song) => song.Song.Notes!.Values.SelectMany(x => x).Select(x => (BendKind)x.Value.State.GetStateValue(CompositionStateKinds.Bend)).ToArray();

        var wild = TestCorpus.InParallel(Enumerable.Range(0, 32), seed => Kinds(TestCorpus.Get(seed, Sound(1)))).SelectMany(x => x).ToArray();
        await Assert.That(wild.Count(x => x == BendKind.ReleaseAndBend)).IsGreaterThan(0);
        await Assert.That(wild.Count(x => x == BendKind.PrebendRelease)).IsGreaterThan(0);

        var plain = TestCorpus.InParallel(Enumerable.Range(0, 16), seed => Kinds(TestCorpus.Get(seed, Sound(0)))).SelectMany(x => x);
        await Assert.That(plain.Count(x => x != BendKind.None)).IsEqualTo(0);
    }

    /// <summary>
    ///     Every pitched part of a song that bends, by its track: the note changes it may slide over, from a note that ended a
    ///     moment before and a pitch bend away, and the ones it slides over.
    /// </summary>
    private static (int Track, int May, int Slides)[] Slides(CorpusSong song) => song.Song.Notes!
        .Where(x => song.Song.TrackDefinitions[x.Key] is PitchInstrumentTrack pitched && Expression.Bends(pitched.InstrumentCode))
        .Select(x =>
        {
            var notes = x.Value;
            var may = Enumerable.Range(1, Math.Max(0, notes.Count - 1)).Count(i => notes[i - 1].Position + notes[i - 1].Value.Duration >= notes[i].Position - Expression.SlideGap
                                                                                   && Math.Abs(notes[i - 1].Value.Pitches[0] - notes[i].Value.Pitches[0]) is >= 1 and <= 12);
            return (x.Key, may, notes.Count(n => (BendKind)n.Value.State.GetStateValue(CompositionStateKinds.Bend) == BendKind.Slide));
        })
        .Where(x => x.may > 0)
        .ToArray();

    [Test]
    public async Task ASlide_IsFromTheNoteBefore_HeardAMomentBefore_AndAPitchBendAway()
    {
        var slides = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, Sound(1))))
        foreach (var notes in song.Song.Notes!.Values)
            for (var i = 0; i < notes.Count; i++)
            {
                if ((BendKind)notes[i].Value.State.GetStateValue(CompositionStateKinds.Bend) != BendKind.Slide)
                    continue;

                slides++;
                await Assert.That(i).IsGreaterThan(0);
                await Assert.That(notes[i - 1].Position + notes[i - 1].Value.Duration).IsGreaterThanOrEqualTo(notes[i].Position - Expression.SlideGap);
                var from = notes[i].Value.State.GetStateValue(CompositionStateKinds.BendDepth);
                await Assert.That(from).IsEqualTo(notes[i - 1].Value.Pitches[0] - notes[i].Value.Pitches[0]);
                await Assert.That(Math.Abs(from)).IsBetween(1, 12);
            }

        await Assert.That(slides).IsGreaterThan(0);
    }

    [Test]
    public async Task APlainSong_SlidesNever_AndAWildOne_OnSomeParts_NowAndThen()
    {
        var plain = TestCorpus.InParallel(Enumerable.Range(0, 16), seed => Slides(TestCorpus.Get(seed, Sound(0)))).SelectMany(x => x).ToArray();
        await Assert.That(plain.Sum(x => x.Slides)).IsEqualTo(0);

        var wild = TestCorpus.InParallel(Enumerable.Range(0, 32), seed => Slides(TestCorpus.Get(seed, Sound(1)))).SelectMany(x => x).ToArray();
        await Assert.That(wild.Count(x => x.Slides > 0)).IsGreaterThan(0);
        await Assert.That(wild.Count(x => x.Slides > 0)).IsLessThan(wild.Length);
        await Assert.That(wild.Where(x => x.May >= 8).All(x => x.Slides < x.May)).IsTrue();
    }

    /// <summary>How many parts that may slide do, and how often, by fifth of the sound facet and at its ends given.</summary>
    [Test]
    [Explicit]
    public async Task SlideReport()
    {
        static string Of((int, int May, int Slides)[] parts)
        {
            var sliding = parts.Where(x => x.Slides > 0).ToArray();
            return $"{sliding.Length / (double)Math.Max(1, parts.Length):P0} of {parts.Length} parts slide, over {sliding.Sum(x => x.Slides) / (double)Math.Max(1, sliding.Sum(x => x.May)):P0} of the changes they may";
        }

        var drawn = TestCorpus.Measure(512, song => (Fifth: Math.Clamp((int)(song.Unconventionality[Facet.Sound] * 5), 0, 4), Parts: Slides(song)));
        foreach (var band in drawn.GroupBy(x => x.Fifth).OrderBy(x => x.Key))
            Console.WriteLine($"  sound {band.Key}/5: {Of(band.SelectMany(x => x.Parts).ToArray())}");
        foreach (var sound in new[] { 0.0, 1.0 })
            Console.WriteLine($"  sound {sound} given: {Of(TestCorpus.InParallel(Enumerable.Range(0, 64), seed => Slides(TestCorpus.Get(seed, Sound(sound)))).SelectMany(x => x).ToArray())}");

        await Task.CompletedTask;
    }

    [Test]
    public async Task InAWildSong_SomePartsBendFar_AndSomeBarely_AndLoudSectionsFarther()
    {
        var bends = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => Bends(TestCorpus.Get(seed, Sound(1))));
        double Far(IEnumerable<(int, int Steps, double?)> some) => some.Count(x => Math.Abs(x.Steps) == 2) / (double)some.Count();

        var parts = bends.SelectMany(song => song.GroupBy(x => x.Track).Where(x => x.Count() >= 8).Select(Far)).ToArray();
        await Assert.That(parts.Max() - parts.Min()).IsGreaterThan(0.5);

        var placed = bends.SelectMany(x => x).Where(x => x.Energy is not null).OrderBy(x => x.Energy).ToArray();
        await Assert.That(Far(placed.TakeLast(placed.Length / 3))).IsGreaterThan(Far(placed.Take(placed.Length / 3)));
    }

    /// <summary>
    ///     How wide the vibratos are, of the notes that sing one on a guitar or a synth lead, which may sing the wide sort:
    ///     the share of each width, by fifth of the sound facet and at its ends given.
    /// </summary>
    [Test]
    [Explicit]
    public async Task VibratoReport()
    {
        static string[] Widths(CorpusSong song) => song.Song.Notes!
            .Where(x => song.Song.TrackDefinitions[x.Key] is PitchInstrumentTrack { InstrumentCode: >= 24 and <= 31 or >= 80 and <= 87 })
            .SelectMany(x => x.Value)
            .Select(x => (Cents: x.Value.State.GetStateValue(CompositionStateKinds.Vibrato), Wide: x.Value.State.GetStateValue(CompositionStateKinds.WideVibrato)))
            .Where(x => x.Cents > 0 || x.Wide > 0)
            .Select(x => x.Wide > 0 ? $"wide {x.Wide} semitones" : $"{x.Cents} cents")
            .ToArray();
        static string Shares(string[] widths) => string.Join(", ", widths.GroupBy(x => x).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count() / (double)widths.Length:P0}")) + $" of {widths.Length}";

        var drawn = TestCorpus.Measure(512, song => (Fifth: Math.Clamp((int)(song.Unconventionality[Facet.Sound] * 5), 0, 4), Widths: Widths(song)));
        foreach (var band in drawn.GroupBy(x => x.Fifth).OrderBy(x => x.Key))
            Console.WriteLine($"  sound {band.Key}/5: {Shares(band.SelectMany(x => x.Widths).ToArray())}");
        Console.WriteLine($"  sound 1 given: {Shares(TestCorpus.InParallel(Enumerable.Range(0, 64), seed => Widths(TestCorpus.Get(seed, Sound(1)))).SelectMany(x => x).ToArray())}");

        await Task.CompletedTask;
    }

    /// <summary>How big the bends are: the share of two steps, by fifth of the sound facet and at its ends given, and by third of the sections' energy; and each kind's share.</summary>
    [Test]
    [Explicit]
    public async Task BendReport()
    {
        static string Far(IReadOnlyCollection<(int, int Steps, double?)> some) => $"{some.Count(x => Math.Abs(x.Steps) == 2) / (double)Math.Max(1, some.Count):P0} two steps of {some.Count}";

        var drawn = TestCorpus.Measure(512, song => (Fifth: Math.Clamp((int)(song.Unconventionality[Facet.Sound] * 5), 0, 4), Bends: Bends(song)));
        foreach (var band in drawn.GroupBy(x => x.Fifth).OrderBy(x => x.Key))
            Console.WriteLine($"  sound {band.Key}/5: {Far(band.SelectMany(x => x.Bends).ToArray())}");
        var wild = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => Bends(TestCorpus.Get(seed, Sound(1))));
        Console.WriteLine($"  sound 1 given: {Far(wild.SelectMany(x => x).ToArray())}");
        var placed = wild.SelectMany(x => x).Where(x => x.Energy is not null).OrderBy(x => x.Energy).ToArray();
        foreach (var (name, third) in new[] { ("quietest", 0), ("middle", 1), ("loudest", 2) })
            Console.WriteLine($"  sound 1 given, {name} third of the sections: {Far(placed.Skip(third * placed.Length / 3).Take(placed.Length / 3).ToArray())}");
        var parts = wild.SelectMany(song => song.GroupBy(x => x.Track).Where(x => x.Count() >= 8).Select(x => x.Count(b => Math.Abs(b.Steps) == 2) / (double)x.Count())).Order().ToArray();
        Console.WriteLine($"  sound 1 given, parts of 8 bends or more: two steps from {parts.First():P0} to {parts.Last():P0}, the middle {parts[parts.Length / 2]:P0}");

        // and how a bend goes, by its kind
        static BendKind[] Kinds(CorpusSong song) => song.Song.Notes!.Values.SelectMany(x => x).Select(x => (BendKind)x.Value.State.GetStateValue(CompositionStateKinds.Bend)).Where(x => x != BendKind.None).ToArray();
        static string KindShares(BendKind[] kinds) => string.Join(", ", kinds.GroupBy(x => x).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count() / (double)kinds.Length:P0}"));
        Console.WriteLine($"  kinds, the corpus: {KindShares(TestCorpus.Measure(512, Kinds).SelectMany(x => x).ToArray())}");
        Console.WriteLine($"  kinds, sound 1 given: {KindShares(TestCorpus.InParallel(Enumerable.Range(0, 64), seed => Kinds(TestCorpus.Get(seed, Sound(1)))).SelectMany(x => x).ToArray())}");

        await Task.CompletedTask;
    }

    [Test]
    public async Task AWideVibrato_IsAGuitarsOrASynthLeads_FromAScaleNoteToTheOneAStepOrTwoAbove_AndAWildSongs()
    {
        static bool IsWide(RealizedNote note) => note.State.GetStateValue(CompositionStateKinds.WideVibrato) > 0;
        int Program(CorpusSong song, int track, RealizedNote note) =>
            InstrumentsOverTime.ProgramOf(note.State, ((PitchInstrumentTrack)song.Song.TrackDefinitions[track]).InstrumentCode);

        var plain = TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, Sound(0)));
        await Assert.That(plain.Sum(x => x.Song.Notes!.Values.Sum(n => n.Count(note => IsWide(note.Value))))).IsEqualTo(0);

        var wide = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, Sound(1))))
        foreach (var (track, notes) in song.Song.Notes!)
        foreach (var note in notes.Where(x => IsWide(x.Value)))
        {
            wide++;
            await Assert.That(Program(song, track, note.Value) is >= 24 and <= 31 or >= 80 and <= 87).IsTrue();
            await Assert.That(Expression.IsOnScale(note.Value)).IsTrue();
            var top = note.Value.State.GetStateValue(CompositionStateKinds.WideVibrato);
            await Assert.That(top == Expression.ScaleDistance(note.Value, 1) || top == Expression.ScaleDistance(note.Value, 2)).IsTrue();
        }

        await Assert.That(wide).IsGreaterThan(0);
    }

    /// <summary>A melody swells only into its phrase's held last note, of two beats or more, on a sound that holds rather than dies away.</summary>
    [Test]
    public async Task AMelodysSwell_IsOnItsPhrasesHeldLastNote_OfASoundThatHolds()
    {
        var swelled = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, Sound(1))))
        foreach (var role in new[] { TrackRole.Melody, TrackRole.CounterMelody })
        {
            if (!song.Song.Notes!.TryGetValue(song.Track(role), out var notes))
                continue;
            foreach (var note in notes.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Swell) == 1))
            {
                var program = InstrumentsOverTime.ProgramOf(note.Value.State, ((PitchInstrumentTrack)song.Song.TrackDefinitions[song.Track(role)]).InstrumentCode);
                await Assert.That(note.Value.State.GetStateValue(Core.Events.StateKinds.HeldDuration)).IsGreaterThan(0).Because($"seed {song.Seed} at {note.Position}");
                await Assert.That(note.Value.Duration).IsGreaterThanOrEqualTo(Expression.LineSwellBeats - 1e-9);
                await Assert.That(InstrumentTraits.Has(program, InstrumentTrait.Decays)).IsFalse();
                swelled++;
            }
        }

        Console.WriteLine($"  {swelled} swells");
        await Assert.That(swelled).IsGreaterThan(0);
    }

    [Test]
    public async Task OnlyALineOfAnInstrumentThatBends_Bends_InOctavesToo()
    {
        var bent = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 8), seed => TestCorpus.Get(seed, Sound(1))))
        foreach (var (track, notes) in song.Song.Notes!)
        foreach (var note in notes.Where(x => Has(CompositionStateKinds.Bend, CompositionStateKinds.Vibrato, CompositionStateKinds.WideVibrato)(x.Value.State)))
        {
            var program = InstrumentsOverTime.ProgramOf(note.Value.State, ((PitchInstrumentTrack)song.Song.TrackDefinitions[track]).InstrumentCode);
            await Assert.That(Expression.Bends(program)).IsTrue();
            // a line's note, in octaves too, or a riff's dive, which takes its power chord down with it
            await Assert.That(note.Value.Pitches.Length == 1 || note.Value.State.GetStateValue(CompositionStateKinds.Octave) > 0
                              || (BendKind)note.Value.State.GetStateValue(CompositionStateKinds.Bend) == BendKind.Dive).IsTrue();
            bent++;
        }

        await Assert.That(bent).IsGreaterThan(0);
    }

    private static Func<Core.Events.StateMap, bool> Has(params Core.Events.StateKind<int>[] kinds) => state => kinds.Any(x => state.GetStateValue(x) != 0);

    private static readonly (string Name, bool IsStrong, Func<Core.Events.StateMap, bool> Has)[] Effects =
    [
        ("vibrato", false, Has(CompositionStateKinds.Vibrato, CompositionStateKinds.WideVibrato)), ("chorus", false, Has(CompositionStateKinds.Chorus)),
        ("swell", false, Has(CompositionStateKinds.Swell)), ("bends", false, Has(CompositionStateKinds.Bend)),
        ("tremolo", true, Has(CompositionStateKinds.Tremolo)), ("echo", true, Has(CompositionStateKinds.Echo)),
        ("sweep", true, Has(CompositionStateKinds.AutoPan)), ("filter", true, Has(CompositionStateKinds.Filter)),
        ("gate", true, Has(CompositionStateKinds.Gate))
    ];

    /// <summary>Which effects each pitched part of a song plays anywhere in it, in the order of <see cref="Effects" />.</summary>
    private static bool[][] PartEffects(CorpusSong song) => song.Song.Notes!
        .Where(x => x.Value.Count > 0 && song.Song.TrackDefinitions[x.Key] is PitchInstrumentTrack)
        .Select(x => Effects.Select(e => x.Value.Any(n => e.Has(n.Value.State))).ToArray())
        .ToArray();

    /// <summary>
    ///     How much of each effect the songs play, by fifths of their sound facet and at its ends given: the share of songs
    ///     that play it anywhere and of pitched parts that do, how many kinds of the gentle effects and of the strong ones a
    ///     song plays, the most strong ones any song does, how many strong ones a pitched part plays, and how many parts of a
    ///     song play one.
    /// </summary>
    [Test]
    [Explicit]
    public async Task Report()
    {

        var drawn = TestCorpus.Measure(512, song => (Band: $"sound {Math.Clamp((int)(song.Unconventionality[Facet.Sound] * 5), 0, 4)}/5", Parts: PartEffects(song)));
        var given = new[] { 0.0, 1.0 }.SelectMany(sound => TestCorpus.InParallel(Enumerable.Range(0, 64), seed => (Band: $"sound {sound} given", Parts: PartEffects(TestCorpus.Get(seed, Sound(sound))))));
        foreach (var band in drawn.OrderBy(x => x.Band).Concat(given).GroupBy(x => x.Band))
        {
            var songs = band.Select(x => x.Parts).ToArray();
            var parts = songs.SelectMany(x => x).ToArray();
            int StrongParts(bool[][] song) => song.Count(p => Effects.Select((e, i) => (e, i)).Any(x => x.e.IsStrong && p[x.i]));
            int Kinds(bool[][] song, bool isStrong) => Effects.Select((e, i) => (e, i)).Count(x => x.e.IsStrong == isStrong && song.Any(p => p[x.i]));
            var shares = Effects.Select((e, i) => $"{e.Name} {songs.Count(x => x.Any(p => p[i])) / (double)songs.Length:P0}/{parts.Count(p => p[i]) / (double)parts.Length:P0}");
            Console.WriteLine($"  {band.Key}, {songs.Length} songs: {string.Join(", ", shares)}; kinds a song: gentle {songs.Average(x => Kinds(x, false)):F1}, " +
                              $"strong {songs.Average(x => Kinds(x, true)):F2}, at most {songs.Max(x => Kinds(x, true))}; strong a part {parts.Average(p => Effects.Select((e, i) => (e, i)).Count(x => x.e.IsStrong && p[x.i])):F2}; " +
                              $"parts with a strong one a song {songs.Average(StrongParts):F2}, at most {songs.Max(StrongParts)}");
        }

        await Task.CompletedTask;
    }

    /// <summary>How many strong effects a song plays by genre, the first and those beside it, and which.</summary>
    [Test]
    [Explicit]
    public async Task ReportStacked()
    {
        foreach (var name in new[] { "trance", "techno", "idm", "reggae", "" })
        {
            var genre = name == "" ? null : Rmg.Tests.Genres.GenrePresets.Genre(name);
            var effects = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre)))
                .Select(x => (SongEffects)x.Trace.Single(t => t.Point == TracePoints.SongEffects).Value!)
                .Select(x => x.Strong == StrongEffect.None ? x.Others : x.Others.Add(x.Strong)).ToArray();
            Console.WriteLine($"{(name == "" ? "no genre" : name)}: any {effects.Average(x => x.Length > 0 ? 1.0 : 0):P0}, two or more {effects.Average(x => x.Length > 1 ? 1.0 : 0):P0}, " +
                              $"a song {effects.Average(x => x.Length):F2}; {string.Join(", ", effects.SelectMany(x => x).GroupBy(x => x).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count()}"))}");
        }

        await Task.CompletedTask;
    }
}
