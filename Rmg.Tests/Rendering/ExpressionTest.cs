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
    public async Task ThePlainestSongs_PlayAVibratoAndAReverbAlone_AndTheWildest_EveryEffect()
    {
        var plain = TestCorpus.InParallel(Enumerable.Range(0, 8), seed => TestCorpus.Get(seed, Sound(0)));
        foreach (var song in plain)
        {
            await Assert.That(Count(song, x => x.State.GetStateValue(CompositionStateKinds.Bend) != 0)).IsEqualTo(0);
            await Assert.That(Count(song, x => x.State.GetStateValue(CompositionStateKinds.Echo) != 0)).IsEqualTo(0);
            await Assert.That(Count(song, x => x.State.GetStateValue(CompositionStateKinds.Tremolo) != 0 || x.State.GetStateValue(CompositionStateKinds.AutoPan) != 0)).IsEqualTo(0);
            await Assert.That(song.Rendered.Tracks.All(x => x.Controllers.Any(c => c.Controller == ExpressionRender.ReverbController))).IsTrue();
        }

        var wild = TestCorpus.InParallel(Enumerable.Range(0, 8), seed => TestCorpus.Get(seed, Sound(1)));
        await Assert.That(wild.Sum(x => Count(x, n => n.State.GetStateValue(CompositionStateKinds.Bend) != 0))).IsGreaterThan(0);
        await Assert.That(wild.Sum(x => x.Rendered.Tracks.Sum(t => t.Echoes.Length))).IsGreaterThan(0);
        await Assert.That(wild.Sum(x => x.Rendered.Tracks.Sum(t => t.Expression.Length))).IsGreaterThan(0);
    }

    [Test]
    public async Task OnlyALineOfAnInstrumentThatBends_Bends()
    {
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 8), seed => TestCorpus.Get(seed, Sound(1))))
        foreach (var (track, notes) in song.Song.Notes!)
        foreach (var note in notes.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Bend) != 0 || x.Value.State.GetStateValue(CompositionStateKinds.Vibrato) != 0))
        {
            var program = note.Value.State.GetStateValue(CompositionStateKinds.Program) is var stated and > 0 ? stated - 1 : ((PitchInstrumentTrack)song.Song.TrackDefinitions[track]).InstrumentCode;
            await Assert.That(Expression.Bends(program)).IsTrue();
            await Assert.That(note.Value.Pitches.Length).IsEqualTo(1);
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
    ///     song plays, the most strong ones any song does, and how many strong ones a pitched part plays.
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
            int Kinds(bool[][] song, bool isStrong) => Effects.Select((e, i) => (e, i)).Count(x => x.e.IsStrong == isStrong && song.Any(p => p[x.i]));
            var shares = Effects.Select((e, i) => $"{e.Name} {songs.Count(x => x.Any(p => p[i])) / (double)songs.Length:P0}/{parts.Count(p => p[i]) / (double)parts.Length:P0}");
            Console.WriteLine($"  {band.Key}, {songs.Length} songs: {string.Join(", ", shares)}; kinds a song: gentle {songs.Average(x => Kinds(x, false)):F1}, " +
                              $"strong {songs.Average(x => Kinds(x, true)):F2}, at most {songs.Max(x => Kinds(x, true))}; strong a part {parts.Average(p => Effects.Select((e, i) => (e, i)).Count(x => x.e.IsStrong && p[x.i])):F2}");
        }

        await Task.CompletedTask;
    }
}
