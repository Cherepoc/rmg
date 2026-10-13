using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Chords;

/// <summary>A turnaround: a pattern's last bar leading back to its start, a V, or a ii–V where the bar has two chords.</summary>
public sealed class TurnaroundTest
{
    [Test]
    public async Task ALastBar_TurnsToV_OrToIiV_TheChordsBeforeKept()
    {
        await Assert.That(Rmg.Core.Composition.Progressions.TurnAround([0, 3, 2, 1], 1)).IsEquivalentTo(ImmutableArray.Create(0, 3, 2, -3), CollectionOrdering.Matching);
        await Assert.That(Rmg.Core.Composition.Progressions.TurnAround([0, 3, 2, 3], 2)).IsEquivalentTo(ImmutableArray.Create(0, 3, 1, -3), CollectionOrdering.Matching);
        // the first chord stays home
        await Assert.That(Rmg.Core.Composition.Progressions.TurnAround([0, 3], 2)).IsEquivalentTo(ImmutableArray.Create(0, -3), CollectionOrdering.Matching);
    }

    [Test]
    public async Task ASectionTurnedAround_EndsOnV_AndAJazzSongsSectionsMostly()
    {
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("jazz"))));
        // but a section that planes, its chords the home's shape moved rather than its progression's, or vamps, on its home
        // each section's turnaround with its own progression, joined by the section
        var sections = songs.SelectMany(song => song.Trace.Where(x => x.Point == TracePoints.Turnaround)
            .Select(turn => (Turns: (((bool Turns, bool))turn.Value!).Turns, Progression: (ImmutableArray<int>)song.Trace.Last(p => p.Point == TracePoints.Progression && p.Section == turn.Section).Value!, turn.Section))
            .Where(x => song.Trace.Where(p => p.Point == TracePoints.Planing && p.Section == x.Section).All(p => p.Value is null)
                        && !song.Trace.Any(p => p.Point == TracePoints.Vamp && p.Section == x.Section && (bool)p.Value!))
            .Select(x => (x.Turns, x.Progression))).ToArray();

        await Assert.That(sections.Where(x => x.Item1).All(x => x.Item2[^1] == -3)).IsTrue();
        Console.WriteLine($"Jazz's sections turned around: {sections.Count(x => x.Item1) / (double)sections.Length:P0}");
        // seen 72% to 83% over three corpora's 64 songs
        await Assert.That(sections.Count(x => x.Item1) / (double)sections.Length).IsGreaterThan(0.6);
    }

    [Test]
    public async Task TheSongsTurnaround_IsNeverPlayedAtThePlainEnd_AndAGenresIs_AsItSays()
    {
        var genre = Genre.Read(new Dictionary<string, IReadOnlyDictionary<string, ByConvention>> { ["turnaround"] = new Dictionary<string, ByConvention> { ["sections"] = new(1, 1, 1) } }, out _)!;

        await Assert.That(Rmg.Core.Composition.Progressions.Turnaround.At(0)).IsEqualTo(0);
        // the plainest songs of a genre that turns around at its plain end turning every section around
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Base: 0, Genre: genre))))
        foreach (var turn in song.Trace.Where(x => x.Point == TracePoints.Turnaround))
            await Assert.That((((bool Turns, bool))turn.Value!).Turns).IsTrue().Because($"seed {song.Seed}, section {turn.Section}");
    }
}
