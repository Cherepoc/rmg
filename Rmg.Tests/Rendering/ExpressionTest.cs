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

    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Range(256);
        foreach (var band in songs.GroupBy(x => Math.Clamp((int)(((Unconventionality)x.Trace.Single(t => t.Point == TracePoints.SongUnconventionality).Value!)[Facet.Sound] * 5), 0, 4)).OrderBy(x => x.Key))
        {
            double Share(Func<CorpusSong, bool> has) => band.Count(has) / (double)band.Count();
            Console.WriteLine($"  sound {band.Key}/5, {band.Count()} songs: vibrato {Share(x => Count(x, n => n.State.GetStateValue(CompositionStateKinds.Vibrato) > 0) > 0):P0}, " +
                              $"bends {Share(x => Count(x, n => n.State.GetStateValue(CompositionStateKinds.Bend) != 0) > 0):P0}, echo {Share(x => x.Rendered.Tracks.Any(t => !t.Echoes.IsEmpty)):P0}, " +
                              $"swell or tremolo {Share(x => x.Rendered.Tracks.Any(t => !t.Expression.IsEmpty)):P0}, sweep {Share(x => x.Rendered.Tracks.Any(t => t.Controllers.Any(c => c.Controller == 10))):P0}, " +
                              $"chorus {Share(x => x.Rendered.Tracks.Any(t => t.Controllers.Any(c => c.Controller == ExpressionRender.ChorusController && c.Value > 0))):P0}");
        }
        await Task.CompletedTask;
    }
}
