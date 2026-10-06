using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.DrumKits;

/// <summary>The drums marking the form, a genre's: struck where the form moves, never keeping time.</summary>
public sealed class DrumMarksTest
{
    private static Genre Marking(double often = 0.5) => new(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty
        .Add("drumJob", ImmutableDictionary<string, ByConvention>.Empty.Add("marks", new(1, 1, 1)))
        .Add("marks", ImmutableDictionary<string, ByConvention>.Empty.Add("often", new(often, often, often))));

    private static DrumJob Job(CorpusSong song) => (DrumJob)song.Trace.Single(x => x.Point == TracePoints.DrumJob).Value!;

    [Test]
    public async Task TheSongs_KeepTime_Always()
    {
        foreach (var song in TestCorpus.Range(64))
            await Assert.That(Job(song)).IsEqualTo(DrumJob.Groove);
    }

    [Test]
    public async Task MarkingDrums_StrikeOnlyWhereTheFormMoves_OrRollIntoASection_OneOrTwoOfThem()
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
                map.Sections.Any(span => Math.Abs(at - span.Start) < 1e-6
                                         || at > span.Start - 1 - 1e-6 && at < span.Start - 1e-6
                                         || at > span.Start && at < span.End && Math.Abs(Math.IEEERemainder(at - span.Start, Meter.PhraseBarCount * span.Meter.BarDuration)) < 1e-6)
                || changes.Any(x => Math.Abs(x - at) < 1e-6)
                || Math.Abs(at - map.Ending.Start) < 1e-6;
            foreach (var (_, notes) in drums)
            foreach (var note in notes)
                await Assert.That(Marks(note.Position)).IsTrue().Because($"seed {song.Seed} at {note.Position}");
            marked += drums.Sum(x => x.Value.Count);
        }

        await Assert.That(marked).IsGreaterThan(0);
    }

    /// <summary>How often the drums mark the form, a bar's hits and where they fall, by how often a genre has them.</summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var often in new[] { 0.0, 0.5, 1.0 })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Marking(often))));
            var (bars, hits, starts, phrases, rolls) = (0.0, 0, 0, 0, 0);
            foreach (var song in songs)
            {
                var map = song.Map;
                bars += map.Sections.Sum(x => x.Duration / x.Meter.BarDuration);
                var notes = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key] is PercussionInstrumentTrack).SelectMany(x => x.Value).ToArray();
                hits += notes.Length;
                starts += notes.Count(n => map.Sections.Any(s => Math.Abs(n.Position - s.Start) < 1e-6) || Math.Abs(n.Position - map.Ending.Start) < 1e-6);
                phrases += notes.Count(n => map.Sections.Any(s => n.Position > s.Start + 1e-6 && n.Position < s.End && Math.Abs(Math.IEEERemainder(n.Position - s.Start, Meter.PhraseBarCount * s.Meter.BarDuration)) < 1e-6));
                rolls += notes.Count(n => map.Sections.Any(s => n.Position > s.Start - 1 - 1e-6 && n.Position < s.Start - 1e-6));
            }

            Console.WriteLine($"often {often}: hits a bar {hits / bars:F2}, at a section's start or the ending {starts / (double)hits:P0}, a phrase's {phrases / (double)hits:P0}, rolls' notes {rolls / (double)hits:P0}, a chord's change the rest");
        }

        await Task.CompletedTask;
    }
}
