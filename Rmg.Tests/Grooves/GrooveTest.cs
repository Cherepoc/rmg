using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;

namespace Rmg.Tests.Grooves;

public sealed class GrooveTest
{
    [Test]
    public async Task ASwing_PlaysAPairsMiddleLate_AndLeavesItsEndsWhereTheyAre()
    {
        var swing = new Swing(1.0 / 6, 1);

        await Assert.That(swing.Apply(0)).IsEqualTo(0);
        await Assert.That(swing.Apply(0.5)).IsEqualTo(2.0 / 3).Within(1e-9);
        await Assert.That(swing.Apply(1)).IsEqualTo(1).Within(1e-9);
        await Assert.That(swing.Apply(3.5)).IsEqualTo(3 + 2.0 / 3).Within(1e-9);
    }

    [Test]
    public async Task ASwing_KeepsTheOrderOfAnyTwoPositions()
    {
        foreach (var swing in new[] { new Swing(1.0 / 6, 1), new Swing(0.05, 0.5), Swing.None })
        {
            var positions = Enumerable.Range(0, 400).Select(x => x / 96.0).ToArray();
            var swung = positions.Select(swing.Apply).ToArray();
            await Assert.That(swung.Zip(swung.Skip(1)).All(x => x.First < x.Second)).IsTrue();
        }
    }

    [Test]
    public async Task ASong_SwingsItsNotesAsItsSwingSays()
    {
        foreach (var song in TestCorpus.Range(32))
        {
            var swing = Render.GetSwing(song.Song);
            var drawn = song.Trace.Single(x => x.Point == TracePoints.Swing).Value;
            await Assert.That(drawn).IsEqualTo(swing);

            var generated = song.Song.Notes!.Values.SelectMany(x => x).Select(x => Math.Round(swing.Apply(x.Position), 6)).ToHashSet();
            var rendered = song.Rendered.Tracks.SelectMany(x => x.NoteTimeline).Select(x => Math.Round(x.Position, 6));
            await Assert.That(rendered.All(generated.Contains)).IsTrue();
        }
    }

    [Test]
    public async Task TheBassPlaysInTheMiddle_TheMelodyNearIt_TheChordsAndThePadOnSidesApart()
    {
        foreach (var song in TestCorpus.Range(32))
        {
            var pans = (ImmutableDictionary<int, double>)song.Trace.Single(x => x.Point == TracePoints.Panning).Value!;
            await Assert.That(pans[SongTracks.BassTrack]).IsEqualTo(0);
            await Assert.That(Math.Abs(pans[SongTracks.MelodyTrack])).IsLessThanOrEqualTo(Panning.GetSpread(Rmg.Core.Songs.TrackRole.Melody));
            await Assert.That(Math.Sign(pans[SongTracks.ChordsTrack])).IsEqualTo(-Math.Sign(pans[SongTracks.PadTrack]));
        }
    }

    /// <summary>How many songs swing, by how conventional their rhythm is and by their tempo, and how much.</summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Range(256).Select(song => (
            Rhythm: ((RhythmicUnconventionality)song.Trace.Single(x => x.Point == TracePoints.SongRhythm).Value!).Value,
            Tempo: song.Rendered.TempoTimeline.GetEffectiveValueAt(0) * Meter.BaseTempo,
            Swing: Render.GetSwing(song.Song)
        )).ToArray();

        foreach (var band in songs.GroupBy(x => Math.Min(2, (int)(x.Rhythm * 3))).OrderBy(x => x.Key))
            Console.WriteLine($"rhythm {band.Key}/3: {band.Count(x => x.Swing.Delay > 0)} of {band.Count()} swing");
        foreach (var band in songs.Where(x => x.Swing.Delay > 0).GroupBy(x => x.Swing.Period).OrderBy(x => x.Key))
            Console.WriteLine($"period {band.Key}: {band.Count()} songs, tempo {band.Min(x => x.Tempo):F0} to {band.Max(x => x.Tempo):F0}, " +
                              $"a triplet's {band.Average(x => x.Swing.Delay / (x.Swing.Period / 6)):P0} on average");
        await Task.CompletedTask;
    }
}
