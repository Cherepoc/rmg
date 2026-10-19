using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>How a line's bends and vibratos move in time, as rendered: how fast a bend lands, how long after it a vibrato starts.</summary>
public sealed class ShapeTimingTest
{
    /// <summary>
    ///     A shaped note as rendered: its kind, its depth, its widths, its bend's rise, its vibrato's rate and wait, whether its
    ///     instrument sings its vibrato, its length in seconds as decided and as swung, an 8th there, and its pitch over it, in
    ///     semitones, by the seconds since its start.
    /// </summary>
    internal sealed record Shaped(BendKind Kind, int Depth, int Cents, int Wide, double Rise, double Rate, double Wait, bool Sings, double Decided, double Seconds, double Eighth,
        (double At, double Semitones)[] Pitch);

    /// <summary>Every shaped note of a song's lines, their tape's drift aside, rendered as its part plays it.</summary>
    internal static IEnumerable<Shaped> ShapedNotes(CorpusSong song)
    {
        var tempo = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.Tempo);
        foreach (var (track, notes) in song.Song.Notes!)
        {
            if (song.Song.TrackDefinitions[track] is not PitchInstrumentTrack { InstrumentCode: var program })
                continue;
            var shaped = notes.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Wow) == 0 && x.Value.State.GetStateValue(CompositionStateKinds.Detune) == 0
                                          && (x.Value.State.GetStateValue(CompositionStateKinds.Bend) != 0 || x.Value.State.GetStateValue(CompositionStateKinds.Vibrato) > 0
                                              || x.Value.State.GetStateValue(CompositionStateKinds.WideVibrato) > 0)).ToArray();
            if (shaped.Length == 0)
                continue;
            var bends = ExpressionRender.Render(notes, song.Map, song.Swing, tempo, 0, []).Bends;
            foreach (var note in shaped)
            {
                var start = song.Swing.Apply(note.Position);
                var end = song.Swing.Apply(note.Position + note.Value.Duration);
                var seconds = Meter.SecondsPerBeat(tempo.GetEffectiveValueAt(note.Position));
                // the last bend written at a place, the note before's return there giving way to the note's own
                var pitch = bends.Where(x => x.Position >= start - 1e-9 && x.Position < end - 1e-9).GroupBy(x => x.Position).Select(x => x.Last())
                    .Select(x => ((x.Position - start) * seconds, (x.Value - 8192) / 8191.0 * ExpressionRender.BendRange)).ToArray();
                var state = note.Value.State;
                yield return new Shaped((BendKind)state.GetStateValue(CompositionStateKinds.Bend), state.GetStateValue(CompositionStateKinds.BendDepth),
                    state.GetStateValue(CompositionStateKinds.Vibrato), state.GetStateValue(CompositionStateKinds.WideVibrato), state.GetStateValue(CompositionStateKinds.BendRise) / 1000.0,
                    state.GetStateValue(CompositionStateKinds.VibratoRate) / 10.0, state.GetStateValue(CompositionStateKinds.VibratoDelay) / 10.0,
                    InstrumentTraits.Has(InstrumentsOverTime.ProgramOf(state, program), InstrumentTrait.SingsVibrato) || state.GetStateValue(CompositionStateKinds.Pinch) == 1,
                    note.Value.Duration * seconds, (end - start) * seconds, 0.5 * seconds, pitch);
            }
        }
    }

    /// <summary>How long a bend takes to land, in seconds: to its top, a bend and release's, or onto the note, a scoop's, a slide's or a prebend's.</summary>
    internal static double? Lands(Shaped note)
    {
        var depth = Math.Abs(note.Depth);
        (double At, double Semitones)? landed = note.Kind switch
        {
            BendKind.BendAndRelease or BendKind.ReleaseAndBend => note.Pitch.FirstOrDefault(x => Math.Abs(x.Semitones) >= 0.9 * depth),
            BendKind.Scoop or BendKind.Slide or BendKind.PrebendRelease => note.Pitch.FirstOrDefault(x => Math.Abs(x.Semitones) <= 0.1 * depth),
            _ => null
        };
        return landed is { At: var at } && (at > 0 || note.Pitch.Length > 0 && landed != note.Pitch[0] || note.Kind is BendKind.BendAndRelease or BendKind.ReleaseAndBend) ? at : null;
    }

    /// <summary>The songs a rule is held to: the corpus's, and Metal's and Blues', which bend and shake the most.</summary>
    private static Shaped[] RuledNotes() =>
    [
        ..TestCorpus.InParallel(Enumerable.Range(0, 64), seed => ShapedNotes(TestCorpus.Get(seed)).ToArray()).SelectMany(x => x),
        ..TestCorpus.InParallel(Enumerable.Range(0, 32), seed => ShapedNotes(TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("metal")))).ToArray()).SelectMany(x => x),
        ..TestCorpus.InParallel(Enumerable.Range(0, 32), seed => ShapedNotes(TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("blues")))).ToArray()).SelectMany(x => x)
    ];

    [Test]
    public async Task ABend_LandsWithinItsRise_QuickOrInTime_AndOnANoteLongEnoughToHoldIt()
    {
        var bent = RuledNotes().Where(x => x.Kind is BendKind.Scoop or BendKind.Slide or BendKind.BendAndRelease or BendKind.ReleaseAndBend or BendKind.PrebendRelease or BendKind.FallOff).ToArray();
        await Assert.That(bent.Length).IsGreaterThan(500);
        foreach (var note in bent)
        {
            var because = $"{note.Kind} of {note.Decided:F3} s, rising in {note.Rise:F3} s";
            // quick, or an 8th where that is slower
            await Assert.That(note.Rise is >= Expression.QuickestRise - 1e-3 and <= Expression.SlowestQuickRise + 1e-3 || Math.Abs(note.Rise - note.Eighth) < 1e-3).IsTrue().Because(because);
            await Assert.That(Expression.Fits(note.Kind, note.Rise, note.Decided, note.Eighth)).IsTrue().Because(because);
            // within the 32nd of a beat the pitch is written at
            if (Lands(note) is { } lands)
                await Assert.That(lands).IsLessThanOrEqualTo((note.Kind == BendKind.PrebendRelease ? note.Rise + Math.Max(note.Eighth, note.Rise) : note.Rise) + note.Eighth / 16).Because(because);
        }
    }

    [Test]
    public async Task AVibrato_WaitsAShareOfItsHeldNoteInItsCycles_AfterTheAttackOrTheBendsLanding_OnANoteLongEnoughToBeHeard()
    {
        var shaking = RuledNotes().Where(x => x.Cents > 0 || x.Wide > 0).ToArray();
        await Assert.That(shaking.Length).IsGreaterThan(500);
        foreach (var note in shaking)
        {
            var because = $"{note.Kind} of {note.Decided:F3} s, a vibrato at {note.Rate} a second waiting {note.Wait} cycles";
            await Assert.That(Expression.HoldsVibrato(note.Kind, note.Rise, note.Decided, note.Eighth, note.Rate, note.Sings)).IsTrue().Because(because);
            // its wait a share of the held note, in its cycles, within its instrument's bounds
            await Assert.That(Math.Abs(note.Wait - Expression.VibratoWait(note.Kind, note.Rise, note.Decided, note.Eighth, note.Rate, note.Sings))).IsLessThan(0.051).Because(because);
            // still, or moving one way only, as its bend goes, until its cycles after the pitch holds
            var (held, _) = Expression.Held(note.Kind, note.Rise, note.Seconds, note.Eighth);
            var before = note.Pitch.Where(x => x.At < held + note.Wait / note.Rate - 1e-6).Select(x => x.Semitones).ToArray();
            var steps = before.Zip(before.Skip(1), (a, b) => Math.Sign(Math.Round(b - a, 4))).Where(x => x != 0).Distinct().Count();
            await Assert.That(steps).IsLessThanOrEqualTo(1).Because(because);
            if (note.Kind == BendKind.None)
                await Assert.That(before.All(x => Math.Abs(x) < 1e-3)).IsTrue().Because(because);
        }
    }

    /// <summary>
    ///     A bent note where it plays: its kind, whether it ends its phrase, the line resting a beat after it, starting a new
    ///     phrase or a new section, how long it is in beats, and whether its top, the note or the note bent up to, is its chord's.
    /// </summary>
    internal static IEnumerable<(BendKind Kind, bool Ends, double Beats, bool TopOnChord)> Placed(CorpusSong song) =>
        song.Song.Notes!.Values.SelectMany(notes => notes.Select((note, i) => (note, i))
            .Where(x => (BendKind)x.note.Value.State.GetStateValue(CompositionStateKinds.Bend) is not (BendKind.None or BendKind.Slide or BendKind.Dive))
            .Select(x =>
            {
                var (note, i) = x;
                var state = note.Value.State;
                var kind = (BendKind)state.GetStateValue(CompositionStateKinds.Bend);
                var ends = i + 1 >= notes.Count || notes[i + 1].Position >= note.Position + note.Value.Duration + 1 - 1e-9 || notes[i + 1].Value.State.GetStateValue(CompositionStateKinds.LinePhraseStart) > 0
                           || notes[i + 1].Position >= (song.Map.SectionAt(note.Position)?.End ?? double.MaxValue) - 1e-9;
                var top = note.Value.Pitches[0] + (kind is BendKind.BendAndRelease or BendKind.PrebendRelease ? state.GetStateValue(CompositionStateKinds.BendDepth) : 0);
                return (kind, ends, note.Value.Duration, Realizer.GetChordNotes(state).Classes.Contains(top.Mod(12)));
            }));

    [Test]
    public async Task AFallOff_EndsItsPhrase_AndAPrebend_IsHeldABeatOrMore()
    {
        var bends = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => Placed(TestCorpus.Get(seed)).ToArray())
            .Concat(TestCorpus.InParallel(Enumerable.Range(0, 32), seed => Placed(TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("blues")))).ToArray()))
            .SelectMany(x => x).ToArray();
        await Assert.That(bends.Count(x => x.Kind == BendKind.FallOff)).IsGreaterThan(20);
        await Assert.That(bends.Where(x => x.Kind == BendKind.FallOff).All(x => x.Ends)).IsTrue();
        await Assert.That(bends.Where(x => x.Kind == BendKind.PrebendRelease).All(x => x.Beats >= 1 - 1e-9)).IsTrue();
    }

    [Test]
    [Explicit]
    public async Task PlacementReport()
    {
        foreach (var (name, overrides) in new (string, SongOverrides?)[] { ("the corpus", null), ("sound 0.2 given", new SongOverrides(Facets: TestCorpus.FacetsOf((Facet.Sound, 0.2)))), ("Blues", new SongOverrides(Genre: GenrePresets.Genre("blues"))) })
        {
            var bends = TestCorpus.InParallel(Enumerable.Range(0, name == "the corpus" ? 256 : 64), seed => Placed(overrides is null ? TestCorpus.Get(seed) : TestCorpus.Get(seed, overrides)).ToArray()).SelectMany(x => x).ToArray();
            string Share(Func<(BendKind Kind, bool Ends, double Beats, bool TopOnChord), bool> of, IEnumerable<(BendKind Kind, bool Ends, double Beats, bool TopOnChord)> among) =>
                among.ToArray() is var some ? $"{some.Count(of) / (double)Math.Max(1, some.Length):P0} of {some.Length}" : "";
            Console.WriteLine($"{name}: kinds {string.Join(", ", bends.GroupBy(x => x.Kind).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count() / (double)bends.Length:P0}"))}");
            Console.WriteLine($"  fall-offs ending a phrase {Share(x => x.Ends, bends.Where(x => x.Kind == BendKind.FallOff))}; prebends of a beat or more {Share(x => x.Beats >= 1 - 1e-9, bends.Where(x => x.Kind == BendKind.PrebendRelease))}");
            Console.WriteLine($"  tops on the chord: bends up {Share(x => x.TopOnChord, bends.Where(x => x.Kind is BendKind.BendAndRelease or BendKind.PrebendRelease))}, onto the note {Share(x => x.TopOnChord, bends.Where(x => x.Kind is BendKind.Scoop))}");
        }

        await Task.CompletedTask;
    }

    /// <summary>The vibratos of a song's guitars and synth leads, which may shake wide, by their width and rate: "35 cents", "a step at 6.5".</summary>
    internal static IEnumerable<string> GuitarVibratos(CorpusSong song) => song.Song.Notes!
        .Where(x => song.Song.TrackDefinitions[x.Key] is PitchInstrumentTrack { InstrumentCode: >= 24 and <= 31 or >= 80 and <= 87 })
        .SelectMany(x => x.Value).Select(x => x.Value.State)
        .Where(x => x.GetStateValue(CompositionStateKinds.Vibrato) > 0 || x.GetStateValue(CompositionStateKinds.WideVibrato) > 0)
        .Select(x => x.GetStateValue(CompositionStateKinds.WideVibrato) > 0
            ? $"{(x.GetStateValue(CompositionStateKinds.WideVibrato) > 2 ? "wider" : "a step")} at {x.GetStateValue(CompositionStateKinds.VibratoRate) / 10.0}"
            : $"{x.GetStateValue(CompositionStateKinds.Vibrato)} cents");

    [Test]
    public async Task AGenresVibratos_AreTheWidthsItNames_AJazzGuitarsNeverAStepWide_AMetalOnesOften()
    {
        string[] Of(string genre) => [..TestCorpus.InParallel(Enumerable.Range(0, 32), seed => GuitarVibratos(TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))).ToArray()).SelectMany(x => x)];
        var jazz = Of("jazz");
        await Assert.That(jazz.Length).IsGreaterThan(20);
        await Assert.That(jazz.All(x => x.EndsWith("cents"))).IsTrue();
        // Metal's a step fast, never slow, its pinches' 40 cents beside
        var metal = Of("metal");
        Console.WriteLine($"Metal's guitars a step wide {metal.Count(x => !x.EndsWith("cents")) / (double)metal.Length:P0} of {metal.Length}");
        await Assert.That(metal.Any(x => x.StartsWith("a step at 4.5"))).IsFalse();
        await Assert.That(metal.Count(x => !x.EndsWith("cents"))).IsGreaterThan(metal.Length / 5);
    }

    [Test]
    public async Task AVibrato_SuitsItsNotesInstrument_AStepWideOnlyAnElectricGuitarsOrALeads_ABroadOrAFastOneNeverOnAStringThatRings()
    {
        // the corpus's, and the presets' that name a fast step or play acoustic guitars
        var genres = new[] { "metal", "doom", "folk", "country" };
        var songs = TestCorpus.Range(64).Concat(genres.SelectMany(genre => TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre))))));
        var wrong = songs.AsParallel().Select(song => song.Song.Notes!
                .Where(x => song.Song.TrackDefinitions[x.Key] is PitchInstrumentTrack)
                .SelectMany(x => x.Value.Select(note => (Program: InstrumentsOverTime.ProgramOf(note.Value.State, ((PitchInstrumentTrack)song.Song.TrackDefinitions[x.Key]).InstrumentCode), note.Value.State)))
                // a pinch's squeal shaken at its own width, none of the table's
                .Where(x => (x.State.GetStateValue(CompositionStateKinds.WideVibrato) > 0 || x.State.GetStateValue(CompositionStateKinds.Vibrato) > 0) && x.State.GetStateValue(CompositionStateKinds.Pinch) == 0)
                .Select(x => (x.Program, Width: Expression.Vibratos.Select(v => v.Width).Single(v => x.State.GetStateValue(CompositionStateKinds.WideVibrato) > 0
                    ? v.Steps > 0 && Math.Abs(10 * v.Rate - x.State.GetStateValue(CompositionStateKinds.VibratoRate)) < 1e-9
                    : v.Steps == 0 && v.Cents == x.State.GetStateValue(CompositionStateKinds.Vibrato))))
                .Where(x => !Expression.Suits(x.Width, x.Program))
                .Select(x => $"{song.Seed}: {Expression.LabelOf(x.Width)} on program {x.Program}").ToArray())
            .SelectMany(x => x).ToArray();
        await Assert.That(wrong).IsEmpty().Because(string.Join("; ", wrong.Take(5)));
    }

    /// <summary>A song's vibratos as <see cref="GuitarVibratos" /> words them, each with its note's instrument's sound: driven, a synth lead, clean, acoustic or other.</summary>
    internal static IEnumerable<(string Sound, string Width)> VibratosBySound(CorpusSong song) => song.Song.Notes!
        .Where(x => song.Song.TrackDefinitions[x.Key] is PitchInstrumentTrack)
        .SelectMany(x => x.Value.Select(note => (Program: InstrumentsOverTime.ProgramOf(note.Value.State, ((PitchInstrumentTrack)song.Song.TrackDefinitions[x.Key]).InstrumentCode), note.Value.State)))
        .Where(x => x.State.GetStateValue(CompositionStateKinds.Vibrato) > 0 || x.State.GetStateValue(CompositionStateKinds.WideVibrato) > 0)
        .Select(x => (x.Program switch { 29 or 30 => "driven", >= 80 and <= 87 => "lead", >= 26 and <= 28 => "clean", 24 or 25 => "acoustic", _ => "other" },
            x.State.GetStateValue(CompositionStateKinds.WideVibrato) > 0
                ? $"{(x.State.GetStateValue(CompositionStateKinds.WideVibrato) > 2 ? "wider" : "a step")} at {x.State.GetStateValue(CompositionStateKinds.VibratoRate) / 10.0}"
                : $"{x.State.GetStateValue(CompositionStateKinds.Vibrato)} cents"));

    [Test]
    [Explicit]
    public async Task WidthBySoundReport()
    {
        foreach (var name in new string?[] { null, "metal", "thrash", "doom", "rock", "blues", "folk", "country" })
        {
            var widths = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => VibratosBySound(name is null ? TestCorpus.Get(seed) : TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(name)))).ToArray())
                .SelectMany(x => x).ToArray();
            foreach (var sound in widths.GroupBy(x => x.Sound).Where(x => x.Key != "other").OrderBy(x => x.Key))
                Console.WriteLine($"{name ?? "the corpus"} {sound.Key}: wide {sound.Count(x => !x.Width.EndsWith("cents")) / (double)sound.Count():P0} of {sound.Count()}; " +
                                  string.Join(", ", sound.GroupBy(x => x.Width).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count() / (double)sound.Count():P0}")));
        }

        await Task.CompletedTask;
    }

    [Test]
    [Explicit]
    public async Task WidthReport()
    {
        foreach (var name in new string?[] { null, "metal", "thrash", "doom", "rock", "blues", "ballad", "jazz", "fusion" })
        {
            var widths = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => GuitarVibratos(name is null ? TestCorpus.Get(seed) : TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(name)))).ToArray())
                .SelectMany(x => x).ToArray();
            Console.WriteLine($"{name ?? "the corpus"}: wide {widths.Count(x => !x.EndsWith("cents")) / (double)Math.Max(1, widths.Length):P0} of {widths.Length}; " +
                              string.Join(", ", widths.GroupBy(x => x).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count() / (double)widths.Length:P0}")));
        }

        await Task.CompletedTask;
    }

    private static string Spread(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        return sorted.Length == 0 ? "none" : $"median {1000 * sorted[sorted.Length / 2]:0} ms, 10-90% {1000 * sorted[sorted.Length / 10]:0}-{1000 * sorted[sorted.Length * 9 / 10]:0} of {sorted.Length}";
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var (name, overrides) in new (string, SongOverrides?)[] { ("the corpus", null), ("Metal", new SongOverrides(Genre: GenrePresets.Genre("metal"))), ("Blues", new SongOverrides(Genre: GenrePresets.Genre("blues"))) })
        {
            var notes = TestCorpus.InParallel(Enumerable.Range(0, name == "the corpus" ? 256 : 64), seed => ShapedNotes(overrides is null ? TestCorpus.Get(seed) : TestCorpus.Get(seed, overrides)).ToArray()).SelectMany(x => x).ToArray();
            Console.WriteLine($"{name}:");
            foreach (var kind in new[] { BendKind.Scoop, BendKind.BendAndRelease, BendKind.PrebendRelease, BendKind.Slide, BendKind.FallOff })
                Console.WriteLine($"  {kind} lands: {Spread(notes.Where(x => x.Kind == kind).Select(Lands).OfType<double>())}");
            // a vibrato on a note bent no way, where it first moves the pitch
            var vibratos = notes.Where(x => x.Kind == BendKind.None && (x.Cents > 0 || x.Wide > 0)).ToArray();
            var onsets = vibratos.Select(x => x.Pitch.FirstOrDefault(p => Math.Abs(p.Semitones) > 0.02)).Where(x => x != default).Select(x => x.At).ToArray();
            Console.WriteLine($"  a vibrato starts: {Spread(onsets)}; sounds for {Spread(vibratos.Select(x => x.Seconds - (x.Pitch.FirstOrDefault(p => Math.Abs(p.Semitones) > 0.02) is { At: var at } && at > 0 ? at : x.Seconds)))}");
            Console.WriteLine($"  notes with a vibrato: {Spread(notes.Where(x => x.Cents > 0 || x.Wide > 0).Select(x => x.Seconds))}; of them bent too {notes.Count(x => (x.Cents > 0 || x.Wide > 0) && x.Kind != BendKind.None) / (double)Math.Max(1, notes.Count(x => x.Cents > 0 || x.Wide > 0)):P0}");
        }

        await Task.CompletedTask;
    }
}
