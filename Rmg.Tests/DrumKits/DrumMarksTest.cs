using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.DrumKits;

/// <summary>The drums marking the form, a genre's: struck where the form moves, never keeping time.</summary>
public sealed class DrumMarksTest
{
    private static Genre Marking(double often = 0.5, double swells = 0.5) => new(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty
        .Add("drumJob", ImmutableDictionary<string, ByConvention>.Empty.Add("marks", new(1, 1, 1)))
        .Add("marks", ImmutableDictionary<string, ByConvention>.Empty.Add("often", new(often, often, often)).Add("swells", new(swells, swells, swells))));

    private static DrumJob Job(CorpusSong song) => (DrumJob)song.Trace.Single(x => x.Point == TracePoints.DrumJob).Value!;

    [Test]
    public async Task TheSongs_KeepTime_Always()
    {
        foreach (var song in TestCorpus.Range(64))
            await Assert.That(Job(song)).IsEqualTo(DrumJob.Groove);
    }

    [Test]
    public async Task MarkingDrums_StrikeOnlyWhereTheFormMoves_OrRollIntoASection_OrSwellThroughItsStart_OneOrTwoOfThem()
    {
        var marked = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Marking()))))
        {
            await Assert.That(Job(song)).IsEqualTo(DrumJob.Marks);
            var drums = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key] is PercussionInstrumentTrack && x.Value.Count > 0).ToArray();
            await Assert.That(drums.Length).IsLessThanOrEqualTo(2).Because($"seed {song.Seed}");
            var map = song.Map;
            var changes = song.ChordChanges;
            bool Marks(double at) =>
                map.Sections.Any(span => Math.Abs(at - span.Start) < DrumMarks.SwellBeats + 1e-6
                                         || at > span.Start && at < span.End && Math.Abs(Math.IEEERemainder(at - span.Start, span.Meter.PhraseDuration)) < 1e-6)
                || changes.Any(x => Math.Abs(x - at) < 1e-6)
                || Math.Abs(at - map.Ending.Start) < DrumMarks.SwellBeats + 1e-6;
            foreach (var (_, notes) in drums)
            foreach (var note in notes)
                await Assert.That(Marks(note.Position)).IsTrue().Because($"seed {song.Seed} at {note.Position}");
            marked += drums.Sum(x => x.Value.Count);
        }

        await Assert.That(marked).IsGreaterThan(0);
    }

    [Test]
    public async Task ASwell_QuickensAndGrowsToItsPeak_ThenSlowsAndFades()
    {
        // a note at the middle of the velocity scale, where a sum scaled by a factor would not swell at all
        var notes = DrumMarks.Roll(new RealizedNote([49], 0, 1, StateMap.Default), 8, DrumMarks.SwellBeats, DrumMarks.SwellBeats, 8).ToArray();
        var peak = notes.MaxBy(x => x.Value.Velocity)!;
        await Assert.That(peak.Position).IsEqualTo(8).Within(0.2);
        // as heard, the first stroke the share of the peak's it is meant to be, or the scale's softest
        double Heard(TimelineItem<RealizedNote> x) => Rmg.Core.Rendering.Render.ToMidiVelocity(x.Value.Velocity);
        await Assert.That(Heard(notes.First())).IsEqualTo(Math.Max(Heard(peak) * DrumMarks.RollFrom, 0.2)).Within(1e-3);
        await Assert.That(notes.All(x => Math.Abs(x.Position - 8) <= DrumMarks.SwellBeats + 1e-9)).IsTrue();
        var (rise, fall) = (notes.TakeWhile(x => x.Position <= peak.Position).ToArray(), notes.SkipWhile(x => x.Position < peak.Position).ToArray());
        // louder and closer up to the peak, softer and further apart after it
        await Assert.That(rise.Zip(rise.Skip(1)).All(x => x.Second.Value.Velocity > x.First.Value.Velocity)).IsTrue();
        await Assert.That(fall.Zip(fall.Skip(1)).All(x => x.Second.Value.Velocity < x.First.Value.Velocity)).IsTrue();
        var gaps = notes.Zip(notes.Skip(1)).Select(x => x.Second.Position - x.First.Position).ToArray();
        await Assert.That(gaps[0]).IsGreaterThan(gaps[rise.Length - 1]);
        await Assert.That(gaps[^1]).IsGreaterThan(gaps[rise.Length - 1]);
    }

    /// <summary>How often the drums mark the form, a bar's hits and where they fall, by how often a genre has them.</summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var (often, swells) in new[] { (0.0, 0.5), (0.5, 0.0), (0.5, 0.5), (0.5, 1.0), (1.0, 0.5) })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Marking(often, swells))));
            var (bars, hits, starts, phrases, rolls, sections, swelling) = (0.0, 0, 0, 0, 0, 0, 0);
            foreach (var song in songs)
            {
                var map = song.Map;
                bars += map.Sections.Sum(x => x.Duration / x.Meter.BarDuration);
                var notes = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key] is PercussionInstrumentTrack).SelectMany(x => x.Value).ToArray();
                hits += notes.Length;
                starts += notes.Count(n => map.Sections.Any(s => Math.Abs(n.Position - s.Start) < 1e-6) || Math.Abs(n.Position - map.Ending.Start) < 1e-6);
                phrases += notes.Count(n => map.Sections.Any(s => n.Position > s.Start + 1e-6 && n.Position < s.End && Math.Abs(Math.IEEERemainder(n.Position - s.Start, s.Meter.PhraseDuration)) < 1e-6));
                rolls += notes.Count(n => map.Sections.Any(s => n.Position > s.Start - 1 - 1e-6 && n.Position < s.Start - 1e-6));
                // a swell's fading notes after a section's start
                sections += map.Sections.Length;
                swelling += map.Sections.Count(s => notes.Count(n => n.Position > s.Start + 1e-6 && n.Position <= s.Start + DrumMarks.SwellBeats + 1e-6) >= 3);
            }

            Console.WriteLine($"often {often}, swells {swells}: sections swelling {swelling / (double)sections:P0}, hits a bar {hits / bars:F2}, at a section's start or the ending {starts / (double)hits:P0}, a phrase's {phrases / (double)hits:P0}, rolls' notes {rolls / (double)hits:P0}, a chord's change the rest");
        }

        await Task.CompletedTask;
    }
}
