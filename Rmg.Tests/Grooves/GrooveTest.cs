using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.Grooves;

public sealed class GrooveTest
{
    /// <summary>Where a place of a note sounds once swung: as the swing has it, or a run's even note's evenly between its run's ends as swung, as a hat roll's and a ratchet's are.</summary>
    internal static double Swung(Swing swing, RealizedNote note, double at)
    {
        var (from, to) = (note.State.GetStateValue(CompositionStateKinds.EvenFrom), note.State.GetStateValue(CompositionStateKinds.EvenTo));
        return to > from ? swing.Apply(from) + (at - from) / (to - from) * (swing.Apply(to) - swing.Apply(from)) : swing.Apply(at);
    }

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
    public async Task ASwing_CountsItsPairsFromEachSpansStart_AndLeavesASpanThatDoesNotPairStraight()
    {
        // a bar of 7/8 from 0, which does not pair 8ths, and 4/4 from 3.5
        var swing = new Swing(1.0 / 6, 1) { Spans = [(0, false), (3.5, true)] };

        await Assert.That(swing.Apply(0.5)).IsEqualTo(0.5);
        await Assert.That(swing.Apply(3.5)).IsEqualTo(3.5).Within(1e-9);
        await Assert.That(swing.Apply(4)).IsEqualTo(3.5 + 2.0 / 3).Within(1e-9);
        await Assert.That(swing.Apply(4.5)).IsEqualTo(4.5).Within(1e-9);
        var positions = Enumerable.Range(0, 400).Select(x => x / 96.0).ToArray();
        var swung = positions.Select(swing.Apply).ToArray();
        await Assert.That(swung.Zip(swung.Skip(1)).All(x => x.First < x.Second)).IsTrue();
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
            var drawn = (Swing)song.Trace.Single(x => x.Point == TracePoints.Swing).Value!;
            await Assert.That((drawn.Delay, drawn.Period)).IsEqualTo((swing.Delay, swing.Period));

            // a note's own place, and its ornament's every step, after it on a line and before it on a drum, a run's even notes
            // evenly between its ends
            var generated = song.Song.Notes!.SelectMany(t => t.Value.Select(x => (Note: x, Direction: song.IsDrum(t.Key) ? -1 : 1))).SelectMany(x =>
                    Enumerable.Range(0, x.Note.Value.State.GetStateValue(CompositionStateKinds.Ornament) is { IsDefault: false } ornament ? ornament.Length + 1 : 1)
                        .Select(i => Math.Round(Swung(swing, x.Note.Value, x.Note.Position + x.Direction * i * x.Note.Value.State.GetStateValue(CompositionStateKinds.OrnamentStep)), 6)))
                .ToHashSet();
            var rendered = song.Rendered.Tracks.SelectMany(x => x.NoteTimeline).Select(x => Math.Round(x.Position, 6));
            await Assert.That(rendered.All(generated.Contains)).IsTrue();
        }
    }

    [Test]
    public async Task TheBassPlaysInTheMiddle_TheMelodyNearIt_TheTwoChordPartsOnSidesApart()
    {
        foreach (var song in TestCorpus.Range(32))
        {
            var pans = (ImmutableDictionary<int, double>)song.Trace.Single(x => x.Point == TracePoints.Panning).Value!;
            await Assert.That(pans[song.Track(TrackRole.Bass)]).IsEqualTo(0);
            await Assert.That(Math.Abs(pans[song.Track(TrackRole.Melody)])).IsLessThanOrEqualTo(Panning.GetSpread(Rmg.Core.Songs.TrackRole.Melody));
            // on sides apart, neither in the middle
            await Assert.That(pans[song.Track(TrackRole.Chords)] * pans[song.Track(TrackRole.Rhythm)]).IsLessThan(0);
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
