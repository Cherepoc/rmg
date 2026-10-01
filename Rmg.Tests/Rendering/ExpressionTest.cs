using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.Rendering;

public sealed class ExpressionTest
{
    private static SongOverrides Sound(double sound) => new(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Sound, sound));

    private static int Count(CorpusSong song, Func<RealizedNote, bool> has) => song.Song.Notes!.Values.Sum(x => x.Count(n => has(n.Value)));

    [Test]
    public async Task ThePlainestSongs_PlayAReverbAlone()
    {
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 8), seed => TestCorpus.Get(seed, Sound(0))))
        {
            await Assert.That(PartEffects(song).Sum(x => x.Count(has => has))).IsEqualTo(0);
            await Assert.That(song.Rendered.Tracks.All(x => x.Controllers.Any(c => c.Controller == ExpressionRender.ReverbController))).IsTrue();
        }
    }

    [Test]
    public async Task TheWildestSongs_PlayOneStrongEffect_OnOnePart_AndTheGentleOnes_Together()
    {
        var wild = TestCorpus.InParallel(Enumerable.Range(0, 16), seed => PartEffects(TestCorpus.Get(seed, Sound(1))));
        var strong = Effects.Select((e, i) => (e, i)).Where(x => x.e.IsStrong).Select(x => x.i).ToArray();
        var gentle = Effects.Select((e, i) => (e, i)).Where(x => !x.e.IsStrong).Select(x => x.i).ToArray();

        foreach (var song in wild)
        {
            await Assert.That(strong.Count(i => song.Any(p => p[i]))).IsLessThanOrEqualTo(1);
            await Assert.That(song.Count(p => strong.Any(i => p[i]))).IsLessThanOrEqualTo(1);
        }

        await Assert.That(wild.Count(song => strong.Any(i => song.Any(p => p[i])))).IsGreaterThan(wild.Length * 3 / 4);
        await Assert.That(wild.SelectMany(x => x).Any(p => gentle.Count(i => p[i]) >= 2)).IsTrue();
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
            .Where(x => x.note.Value.State.GetStateValue(CompositionStateKinds.BendDepth) != 0)
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
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, Sound(1))))
        foreach (var note in song.Song.Notes!.Values.SelectMany(x => x))
        {
            var kind = (BendKind)note.Value.State.GetStateValue(CompositionStateKinds.Bend);
            var depth = note.Value.State.GetStateValue(CompositionStateKinds.BendDepth);
            if (kind is not (BendKind.Scoop or BendKind.BendAndRelease or BendKind.FallOff))
                continue;

            var direction = kind == BendKind.BendAndRelease ? 1 : -1;
            await Assert.That(depth == Expression.ScaleDistance(note.Value, direction) || depth == Expression.ScaleDistance(note.Value, 2 * direction)).IsTrue();
            await Assert.That(Math.Abs(depth)).IsBetween(1, 4);
        }
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

    /// <summary>How big the bends are: the share of two steps, by fifth of the sound facet and at its ends given, and by third of the sections' energy.</summary>
    [Test]
    [Explicit]
    public async Task BendReport()
    {
        static double SoundOf(CorpusSong song) => ((Unconventionality)song.Trace.Single(t => t.Point == TracePoints.SongUnconventionality).Value!)[Facet.Sound];
        static string Far(IReadOnlyCollection<(int, int Steps, double?)> some) => $"{some.Count(x => Math.Abs(x.Steps) == 2) / (double)Math.Max(1, some.Count):P0} two steps of {some.Count}";

        var drawn = TestCorpus.Measure(512, song => (Fifth: Math.Clamp((int)(SoundOf(song) * 5), 0, 4), Bends: Bends(song)));
        foreach (var band in drawn.GroupBy(x => x.Fifth).OrderBy(x => x.Key))
            Console.WriteLine($"  sound {band.Key}/5: {Far(band.SelectMany(x => x.Bends).ToArray())}");
        var wild = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => Bends(TestCorpus.Get(seed, Sound(1))));
        Console.WriteLine($"  sound 1 given: {Far(wild.SelectMany(x => x).ToArray())}");
        var placed = wild.SelectMany(x => x).Where(x => x.Energy is not null).OrderBy(x => x.Energy).ToArray();
        foreach (var (name, third) in new[] { ("quietest", 0), ("middle", 1), ("loudest", 2) })
            Console.WriteLine($"  sound 1 given, {name} third of the sections: {Far(placed.Skip(third * placed.Length / 3).Take(placed.Length / 3).ToArray())}");
        var parts = wild.SelectMany(song => song.GroupBy(x => x.Track).Where(x => x.Count() >= 8).Select(x => x.Count(b => Math.Abs(b.Steps) == 2) / (double)x.Count())).Order().ToArray();
        Console.WriteLine($"  sound 1 given, parts of 8 bends or more: two steps from {parts.First():P0} to {parts.Last():P0}, the middle {parts[parts.Length / 2]:P0}");

        await Task.CompletedTask;
    }

    [Test]
    public async Task OnlyALineOfAnInstrumentThatBends_Bends_InOctavesToo()
    {
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 8), seed => TestCorpus.Get(seed, Sound(1))))
        foreach (var (track, notes) in song.Song.Notes!)
        foreach (var note in notes.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Bend) != 0 || x.Value.State.GetStateValue(CompositionStateKinds.Vibrato) != 0))
        {
            var program = note.Value.State.GetStateValue(CompositionStateKinds.Program) is var stated and > 0 ? stated - 1 : ((PitchInstrumentTrack)song.Song.TrackDefinitions[track]).InstrumentCode;
            await Assert.That(Expression.Bends(program)).IsTrue();
            await Assert.That(note.Value.Pitches.Length == 1 || note.Value.State.GetStateValue(CompositionStateKinds.Octave) > 0).IsTrue();
        }
    }

    private static readonly (string Name, bool IsStrong, Core.Events.StateKind<int> Kind)[] Effects =
    [
        ("vibrato", false, CompositionStateKinds.Vibrato), ("chorus", false, CompositionStateKinds.Chorus),
        ("swell", false, CompositionStateKinds.Swell), ("bends", false, CompositionStateKinds.Bend),
        ("tremolo", true, CompositionStateKinds.Tremolo), ("echo", true, CompositionStateKinds.Echo),
        ("sweep", true, CompositionStateKinds.AutoPan)
    ];

    /// <summary>Which effects each pitched part of a song plays anywhere in it, in the order of <see cref="Effects" />.</summary>
    private static bool[][] PartEffects(CorpusSong song) => song.Song.Notes!
        .Where(x => x.Value.Count > 0 && song.Song.TrackDefinitions[x.Key] is PitchInstrumentTrack)
        .Select(x => Effects.Select(e => x.Value.Any(n => n.Value.State.GetStateValue(e.Kind) != 0)).ToArray())
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
        static double SoundOf(CorpusSong song) => ((Unconventionality)song.Trace.Single(t => t.Point == TracePoints.SongUnconventionality).Value!)[Facet.Sound];

        var drawn = TestCorpus.Measure(512, song => (Band: $"sound {Math.Clamp((int)(SoundOf(song) * 5), 0, 4)}/5", Parts: PartEffects(song)));
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
}
