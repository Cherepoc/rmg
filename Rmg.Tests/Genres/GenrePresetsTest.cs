using Rmg.Core.Songs;
using Rmg.Core.Composition;

namespace Rmg.Tests.Genres;

public sealed class GenrePresetsTest
{
    [Test]
    public async Task EveryChoice_IsAFamilysMemberAtMost_SoThatAFileWritesItInFamilies()
    {
        // a family's choice one dot deep, its family no choice's option, so that a file's entry holds both apart
        foreach (var choice in GenreChoices.All)
            await Assert.That(choice.Name.Count(x => x == '.')).IsLessThanOrEqualTo(1).Because(choice.Name);
        foreach (var family in GenreChoices.All.Where(x => x.Name.Contains('.')).Select(x => x.Name.Split('.')[0]).Distinct())
        foreach (var own in GenreChoices.All.Where(x => x.Name == family))
            await Assert.That(own.Options.Any(o => GenreChoices.All.Any(c => c.Name == $"{family}.{o.Name}"))).IsFalse().Because(family);
        await Assert.That(GenrePresets.All.SelectMany(x => x.Choices.Keys).All(x => GenreChoices.All.Any(c => c.Name == x))).IsTrue();
    }

    [Test]
    public async Task EveryChoice_HasADescription_ThatNamesNoFamilyLeftUnreplaced()
    {
        foreach (var choice in GenreChoices.All)
            await Assert.That(choice.Description.Length).IsGreaterThan(20).Because(choice.Name);
    }

    [Test]
    public async Task EveryPreset_NamesTheInstrumentsOfEveryPartItMayPlay_SoThatNoneFallsOnTheSongsPool()
    {
        foreach (var preset in GenrePresets.All)
        {
            var genre = GenrePresets.Genre(preset);
            foreach (var (role, pool) in GenreChoices.Instruments)
            {
                // the riff's twin plays the riff's instrument
                var plays = (role == TrackRole.Riff ? [role, TrackRole.RiffTwin] : new[] { role }).Any(part =>
                    (genre.Chance(GenreChoices.Parts, GenreChoices.Name(part.ToString())) ?? SongParts.ChanceOf(part)) is var chance
                    && (chance.Plain > 0 || chance.Tuned > 0 || chance.Wild > 0));
                if (plays)
                    await Assert.That(genre.Names(pool)).IsTrue().Because($"{preset.Name} may play the {role} but names none of its instruments");
            }
        }
    }

    [Test]
    public async Task EveryPreset_IsAGenre_AsARequestReadsIt()
    {
        foreach (var preset in GenrePresets.All)
            _ = GenrePresets.Genre(preset);

        await Assert.That(GenrePresets.All.Select(x => x.Id).Distinct().Count()).IsEqualTo(GenrePresets.All.Count);
        await Assert.That(GenrePresets.All.Count).IsEqualTo(29);
    }

    [Test]
    public async Task APreset_MakesSongs()
    {
        foreach (var preset in GenrePresets.All)
        foreach (var @base in new[] { 0.0, 1.0 })
            await Assert.That(TestCorpus.Get(1, new SongOverrides(Base: @base, Genre: GenrePresets.Genre(preset))).Song.Notes!.Values.Sum(x => x.Count)).IsGreaterThan(0).Because(preset.Name);
    }
}

public sealed class GenrePresetsReportTest
{
    private const int Count = 64;

    [Test]
    [Explicit]
    public async Task EveryPreset_AgainstNone()
    {
        var none = TestCorpus.Range(Count);
        void Print(string name, IReadOnlyList<CorpusSong> songs)
        {
            GenreReportTest.Report(name, [..songs.Select(GenreReportTest.Measure)]);
            var kits = songs.Where(x => x.PlaysDrums).GroupBy(x => Rmg.Core.Songs.GeneralMidi.KitName(x.Rendered.Tracks.Single(t => t.IsPercussionInstrument).PitchInstrumentCode));
            Console.WriteLine("  kit: " + string.Join(", ", kits.OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {100.0 * x.Count() / songs.Count:0}%")));
            var shapes = songs.SelectMany(GenreReportTest.Shapes).ToArray();
            Console.WriteLine("  shapes: " + string.Join(", ", shapes.GroupBy(x => x.Name).OrderByDescending(x => x.Count()).Take(6).Select(x => $"{x.Key} {100.0 * x.Count() / shapes.Length:0}%")));
            GenreRhythmReportTest.Report(name, [..songs.Select(GenreRhythmReportTest.Falls)]);
        }

        Print("none", none);
        foreach (var preset in GenrePresets.All)
            Print(preset.Name, TestCorpus.InParallel(Enumerable.Range(0, Count), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(preset)))));
        await Task.CompletedTask;
    }

    /// <summary>
    ///     How long the sections' patterns are, and in twelve bars where the phrases start and end: the second from IV, the
    ///     last from V, the first two back home; none's and every preset's.
    /// </summary>
    [Test]
    [Explicit]
    public async Task EveryPreset_Phrases()
    {
        void Print(string name, IReadOnlyList<CorpusSong> songs)
        {
            var sections = songs.SelectMany(song =>
            {
                var meters = (System.Collections.Immutable.ImmutableDictionary<int, Rmg.Core.Composition.Meter>)song.Trace.First(t => t.Point == Rmg.Core.Composition.TracePoints.SectionMeter).Value!;
                return song.Trace.Where(t => t.Point == Rmg.Core.Composition.TracePoints.Progression)
                    .Select(t => (Phrases: meters[t.Section].Phrases, Roots: (System.Collections.Immutable.ImmutableArray<int>)t.Value!));
            }).ToArray();
            int Mod7(int x) => (x % 7 + 7) % 7;
            var twelve = sections.Where(x => x.Phrases == 3).ToArray();
            string At(int phrase, bool start, int root) => twelve.Length == 0 ? "-" :
                $"{100.0 * twelve.Count(x => Mod7(x.Roots[start ? phrase * x.Roots.Length / 3 : (phrase + 1) * x.Roots.Length / 3 - 1]) == root) / twelve.Length:0}%";
            Console.WriteLine($"{name}: " + string.Join(", ", sections.GroupBy(x => x.Phrases).OrderBy(x => x.Key).Select(x => $"{4 * x.Key} bars {100.0 * x.Count() / sections.Length:0}%"))
                              + $"; twelve bars: IV at 5 {At(1, true, 3)}, V at 9 {At(2, true, 4)}, home at 4 {At(0, false, 0)}, at 8 {At(1, false, 0)}"
                              + $"; eight bars: home at 5 {AtEight(0)}, V at 5 {AtEight(4)}");
            string AtEight(int root) => sections.Where(x => x.Phrases == 2).ToArray() is { Length: > 0 } eight
                ? $"{100.0 * eight.Count(x => Mod7(x.Roots[x.Roots.Length / 2]) == root) / eight.Length:0}%"
                : "-";
        }

        Print("none", TestCorpus.Range(Count));
        foreach (var preset in GenrePresets.All)
            Print(preset.Name, TestCorpus.InParallel(Enumerable.Range(0, Count), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(preset)))));
        await Task.CompletedTask;
    }

    /// <summary>The sections' energy on the average, and how far a song's sections spread from its own: none's and every preset's.</summary>
    [Test]
    [Explicit]
    public async Task EveryPreset_Dynamics()
    {
        void Print(string name, IReadOnlyList<CorpusSong> songs)
        {
            var energies = songs.Select(x => x.Trace.Where(t => t.Point == Rmg.Core.Composition.TracePoints.SectionEnergy)
                .Select(t => ((Rmg.Core.Composition.SectionEnergyTrace)t.Value!).Energy).ToArray()).Where(x => x.Length > 0).ToArray();
            var spread = energies.Average(x => Math.Sqrt(x.Average(e => (e - x.Average()) * (e - x.Average()))));
            Console.WriteLine($"{name}: energy {energies.Average(x => x.Average()):0.00}, sections' spread {spread:0.00}");
        }

        Print("none", TestCorpus.Range(Count));
        foreach (var preset in GenrePresets.All)
            Print(preset.Name, TestCorpus.InParallel(Enumerable.Range(0, Count), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(preset)))));
        await Task.CompletedTask;
    }

    /// <summary>The share of songs in one of the forms, and the three commonest forms: none's and every preset's.</summary>
    [Test]
    [Explicit]
    public async Task EveryPreset_Forms()
    {
        void Print(string name, IReadOnlyList<CorpusSong> songs)
        {
            var forms = songs.Select(x => (Rmg.Core.Composition.SongStructure)x.Trace.First(t => t.Point == Rmg.Core.Composition.TracePoints.SongForm).Value!)
                .Select(x => x.Roles.Values.Any(r => r == Rmg.Core.Composition.SectionRole.Free) ? "own" : string.Concat(x.SectionIds.Select(id => x.Roles[id].ToString()[0])))
                .ToArray();
            Console.WriteLine($"{name}: " + string.Join(", ", forms.GroupBy(x => x).OrderByDescending(x => x.Count()).Take(4).Select(x => $"{x.Key} {100.0 * x.Count() / forms.Length:0}%")));
        }

        Print("none", TestCorpus.Range(Count));
        foreach (var preset in GenrePresets.All)
            Print(preset.Name, TestCorpus.InParallel(Enumerable.Range(0, Count), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(preset)))));
        await Task.CompletedTask;
    }

    /// <summary>How far each pitched part sits from the middle on the average, from 0, the middle, to 1, a side: none's and every preset's.</summary>
    [Test]
    [Explicit]
    public async Task EveryPreset_Pans()
    {
        TrackRole[] roles = [TrackRole.Melody, TrackRole.Chords, TrackRole.Bass, TrackRole.Pad, TrackRole.CounterMelody, TrackRole.Riff, TrackRole.Rhythm];
        void Print(string name, IReadOnlyList<CorpusSong> songs)
        {
            var tracks = songs.SelectMany(x => x.Rendered.Tracks).Where(x => !x.IsPercussionInstrument).ToArray();
            Console.WriteLine($"{name}: " + string.Join(", ", roles.Select(role => $"{role} {tracks.Where(x => x.Role == role).Select(x => Math.Abs(x.Pan)).DefaultIfEmpty(0).Average():0.00}")));
        }

        Print("none", TestCorpus.Range(Count));
        foreach (var preset in GenrePresets.All)
            Print(preset.Name, TestCorpus.InParallel(Enumerable.Range(0, Count), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(preset)))));
        await Task.CompletedTask;
    }

    /// <summary>The share of songs playing each effect at all, the gentle ones and the strong: none's and every preset's.</summary>
    [Test]
    [Explicit]
    public async Task EveryPreset_Effects()
    {
        void Print(string name, IReadOnlyList<CorpusSong> songs)
        {
            var effects = songs.Select(x => (Rmg.Core.Composition.SongEffects)x.Trace.Single(t => t.Point == Rmg.Core.Composition.TracePoints.SongEffects).Value!).ToArray();
            string Share(Func<Rmg.Core.Composition.SongEffects, bool> plays) => $"{100.0 * effects.Count(plays) / effects.Length:0}%";
            Console.WriteLine($"{name}: vibrato {Share(x => x.Vibrato)}, chorus {Share(x => x.Chorus)}, swell {Share(x => x.Swell)}, bends {Share(x => x.Bends)}; "
                              + string.Join(", ", effects.GroupBy(x => x.Strong).OrderBy(x => x.Key).Select(x => $"{x.Key} {100.0 * x.Count() / effects.Length:0}%")));
        }

        Print("none", TestCorpus.Range(Count));
        foreach (var preset in GenrePresets.All)
            Print(preset.Name, TestCorpus.InParallel(Enumerable.Range(0, Count), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(preset)))));
        await Task.CompletedTask;
    }

    /// <summary>The chords off the section's scale, of every chord: none's and every preset's, by where their scale comes from.</summary>
    [Test]
    [Explicit]
    public async Task EveryPreset_ChordScales()
    {
        void Print(string name, IReadOnlyList<CorpusSong> songs)
        {
            var kinds = songs.SelectMany(x => x.Trace.Where(t => t.Point == Rmg.Core.Composition.TracePoints.ChordScales)
                .SelectMany(t => (System.Collections.Immutable.ImmutableArray<Rmg.Core.Composition.ChordScale>)t.Value!)).ToArray();
            Console.WriteLine($"{name}: " + string.Join(", ", Enum.GetValues<Rmg.Core.Composition.ChordScaleKind>()
                .Select(k => $"{k} {100.0 * kinds.Count(x => x.Kind == k) / kinds.Length:0}%")));
        }

        Print("none", TestCorpus.Range(Count));
        foreach (var preset in GenrePresets.All)
            Print(preset.Name, TestCorpus.InParallel(Enumerable.Range(0, Count), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(preset)))));
        await Task.CompletedTask;
    }

    /// <summary>How the chord roots move, of every move between a section's chords: none's and every preset's, the four commonest.</summary>
    [Test]
    [Explicit]
    public async Task EveryPreset_RootMotions()
    {
        void Print(string name, IReadOnlyList<CorpusSong> songs)
        {
            var moves = songs.SelectMany(x => x.Trace.Where(t => t.Point == Rmg.Core.Composition.TracePoints.Progression))
                .Select(t => (System.Collections.Immutable.ImmutableArray<int>)t.Value!)
                .SelectMany(x => x.Zip(x.Skip(1)).Select(m => Rmg.Core.Composition.Progressions.Motions[((m.Second - m.First) % 7 + 7) % 7].Name))
                .ToArray();
            Console.WriteLine($"{name}: " + string.Join(", ", moves.GroupBy(x => x).OrderByDescending(x => x.Count()).Take(4).Select(x => $"{x.Key} {100.0 * x.Count() / moves.Length:0}%")));
        }

        Print("none", TestCorpus.Range(Count));
        foreach (var preset in GenrePresets.All)
            Print(preset.Name, TestCorpus.InParallel(Enumerable.Range(0, Count), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(preset)))));
        await Task.CompletedTask;
    }

    /// <summary>
    ///     Every preset's choices at the plainest, a middling and the wildest base, the three commonest options of each
    ///     and their shares, so that what a genre's ends make of it is seen: an end collapsing onto one option, or one
    ///     losing the genre.
    /// </summary>
    [Test]
    [Explicit]
    public async Task EveryPreset_ChoicesByEnd()
    {
        static string Top(IEnumerable<string> values)
        {
            var all = values.ToArray();
            return string.Join(", ", all.GroupBy(x => x).OrderByDescending(x => x.Count()).Take(3).Select(x => $"{x.Key} {100.0 * x.Count() / all.Length:0}%"));
        }

        void Print(string name, Genre genre)
        {
            Console.WriteLine($"== {name}");
            var ends = new[] { 0.0, 0.5, 1.0 }.Select(@base => TestCorpus.InParallel(Enumerable.Range(0, Count), seed => TestCorpus.Get(seed, new SongOverrides(Base: @base, Genre: genre)))).ToArray();
            void Row(string choice, Func<CorpusSong, IEnumerable<string>> of) =>
                Console.WriteLine($"  {choice,-10} " + string.Join(" | ", ends.Select(x => Top(x.SelectMany(of)))));
            Row("meter", x => [Meter.NameOf(x.Map.Meter)]);
            Row("feel", x => [Feels.NameOf(Feels.Of(x.Song.Notes![SongTracks.ChordsTrack].First().Value.State))]);
            Row("scale", x => [x.Song.Draws!.Scale.Name]);
            Row("shapes", x => GenreReportTest.Shapes(x).Select(y => y.Name));
            Row("setup", x => [$"{x.Song.Draws!.DrumSetup}"]);
            Row("kit", x => x.PlaysDrums ? [Rmg.Core.Songs.GeneralMidi.KitName(x.Song.Draws!.Kit)] : []);
            Row("melody", x => x.Song.TrackDefinitions.Values.OfType<Rmg.Core.Songs.PitchInstrumentTrack>().Where(y => y.Role == Rmg.Core.Songs.TrackRole.Melody).Select(y => Rmg.Core.Songs.GeneralMidi.Programs[y.InstrumentCode]));
            Row("parts", x => x.Song.Draws!.Parts.Select(y => $"{y}"));
            Row("timeFeel", x => x.Trace.Where(y => y.Point == TracePoints.TimeFeel).Select(y => (int)y.Value! switch { 1 => "half", -1 => "double", _ => "as it is" }));
        }

        Print("none", Genre.None);
        foreach (var preset in GenrePresets.All)
            Print(preset.Name, GenrePresets.Genre(preset));
        await Task.CompletedTask;
    }

    /// <summary>
    ///     Every preset's songs at the plainest, a middling and the wildest base: how many parts they have, how many in
    ///     4/4, swung, and in the commonest scale, against none, so that a genre's wild end is seen to stray from it.
    /// </summary>
    [Test]
    [Explicit]
    public async Task EveryPreset_PlainMiddlingAndWild()
    {
        void Print(string name, Genre genre)
        {
            var cells = new[] { 0.0, 0.5, 1.0 }.Select(@base =>
            {
                var songs = TestCorpus.InParallel(Enumerable.Range(0, Count), seed => TestCorpus.Get(seed, new SongOverrides(Base: @base, Genre: genre)));
                var scale = songs.GroupBy(x => x.Song.Draws!.Scale.Name).MaxBy(x => x.Count())!;
                // the songs out of the genre's range of tempos at the base, which a song's feel strays around
                var outOfRange = genre.Range(GenreChoices.Tempo, @base) is { } range
                    ? songs.Count(x => GenreChoices.AmountOfTempo(x.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(0).GetStateValue(Rmg.Core.Events.StateKinds.Tempo)) is var amount
                                        && (amount < range.From - 1e-3 || amount > range.To + 1e-3))
                    : 0;
                return $"{@base:0.0}: parts {songs.Average(x => x.Song.Draws!.Parts.Count):0.0}, 4/4 {100.0 * songs.Count(x => Meter.NameOf(x.Map.Meter) == "4/4") / Count:0}%, "
                       + $"swung {100.0 * songs.Count(x => x.Song.Draws!.Swing.Delay > 0) / Count:0}%, {scale.Key} {100.0 * scale.Count() / Count:0}%, tempo out {100.0 * outOfRange / Count:0}%, "
                       + $"bpm {songs.Select(x => x.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(0).GetStateValue(Rmg.Core.Events.StateKinds.Tempo) * Meter.BaseTempo).Order().ElementAt(Count / 2):0}";
            });
            Console.WriteLine($"{name,-12} " + string.Join(" | ", cells));
        }

        Print("none", Genre.None);
        foreach (var preset in GenrePresets.All)
            Print(preset.Name, GenrePresets.Genre(preset));
        await Task.CompletedTask;
    }
}
