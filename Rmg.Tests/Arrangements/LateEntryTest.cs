using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Arrangements;

public sealed class LateEntryTest
{
    private static ImmutableDictionary<TrackRole, int> Entries(CorpusSong song) =>
        (ImmutableDictionary<TrackRole, int>)song.Trace.Single(x => x.Point == TracePoints.LateEntries).Value!;

    private static SongOverrides Form(double form) => new(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Form, form));

    [Test]
    public async Task AHeldPart_PlaysNothingBeforeItsEntry_AndComesInThere()
    {
        var (held, heard) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 256), seed => TestCorpus.Get(seed, Form(0.5))).Where(x => !Entries(x).IsEmpty))
        foreach (var (part, entry) in Entries(song))
        {
            var sections = song.Map.Sections;
            // the bar before the entry is the fill's, which brings the drums in
            var until = sections[entry].Start - (part == TrackRole.Drum ? sections[entry - 1].Meter.BarDuration : 0);
            int Notes(double from, double to) => song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role == part)
                .Sum(x => x.Value.Count(n => n.Position >= from - 1e-9 && n.Position < to - 1e-9));
            // but in a section it would otherwise leave silent, which plays it
            int Others(SectionSpan span) => song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role != part && song.Song.TrackDefinitions[x.Key].Role != TrackRole.Drum)
                .Sum(x => x.Value.Count(n => n.Position >= span.Start - 1e-9 && n.Position < span.End - 1e-9));
            foreach (var span in sections.Take(entry).Where(x => Others(x) > 0))
                await Assert.That(Notes(span.Start, Math.Min(span.End, until))).IsEqualTo(0).Because($"seed {song.Seed}, {part} before {entry}");
            held++;
            if (Notes(sections[entry].Start, sections[entry].End) > 0)
                heard++;
        }

        await Assert.That(held).IsGreaterThan(20);
        await Assert.That(heard / (double)held).IsGreaterThan(0.9);
    }

    [Test]
    public async Task ThePlainestSongs_HoldNoPartBack()
    {
        foreach (var seed in Enumerable.Range(0, 16))
            await Assert.That(Entries(TestCorpus.Get(seed, Form(0)))).IsEmpty();
    }

    /// <summary>The songs holding parts back, by fifth of the form facet, by part and by how many, and where the parts come in.</summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Measure(1024, song => (Form: ((Unconventionality)song.Trace.Single(t => t.Point == TracePoints.SongUnconventionality).Value!)[Facet.Form], Entries: Entries(song),
            Returns: Enumerable.Range(1, song.Map.Sections.Length - 1).FirstOrDefault(i => song.Map.Sections.Take(i).Any(s => s.SectionId == song.Map.Sections[i].SectionId))));
        foreach (var band in songs.GroupBy(x => Math.Clamp((int)(x.Form * 5), 0, 4)).OrderBy(x => x.Key))
            Console.WriteLine($"  form {band.Key}/5, {band.Count()} songs: holding back {band.Count(x => !x.Entries.IsEmpty) / (double)band.Count():P0}, two or more {band.Count(x => x.Entries.Count > 1) / (double)band.Count():P1}");
        var entries = songs.SelectMany(x => x.Entries.Select(e => (Part: e.Key, Entry: e.Value, x.Returns))).ToArray();
        var several = songs.Where(x => x.Entries.Count > 1).ToArray();
        Console.WriteLine($"  {entries.Length} parts held back: {string.Join(", ", entries.GroupBy(x => x.Part).Select(g => $"{g.Key} {g.Count()}"))}; entering at the section {string.Join(", ", entries.GroupBy(x => x.Entry).OrderBy(g => g.Key).Select(g => $"{g.Key + 1}: {g.Count()}"))}, " +
                          $"at the first return {entries.Count(x => x.Entry == x.Returns) / (double)Math.Max(1, entries.Length):P0}; of {several.Length} songs holding several, entering apart {several.Count(x => x.Entries.Values.Distinct().Count() > 1) / (double)Math.Max(1, several.Length):P0}");
        await Task.CompletedTask;
    }
}
