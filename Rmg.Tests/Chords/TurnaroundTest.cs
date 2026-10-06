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
        await Assert.That(Rmg.Core.Composition.Progressions.TurnAround([0, 3, 2, 1], 1)).IsEquivalentTo(ImmutableArray.Create(0, 3, 2, -3), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Rmg.Core.Composition.Progressions.TurnAround([0, 3, 2, 3], 2)).IsEquivalentTo(ImmutableArray.Create(0, 3, 1, -3), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        // the first chord stays home
        await Assert.That(Rmg.Core.Composition.Progressions.TurnAround([0, 3], 2)).IsEquivalentTo(ImmutableArray.Create(0, -3), TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task ASectionTurnedAround_EndsOnV_AndAJazzSongsSectionsMostly()
    {
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("jazz"))));
        // but a section that planes, its chords the home's shape moved rather than its progression's
        var sections = songs.SelectMany(song => song.Trace.Where(x => x.Point == TracePoints.Turnaround)
            .Zip(song.Trace.Where(x => x.Point == TracePoints.Progression), (turn, progression) => (Turns: (bool)turn.Value!, Progression: (ImmutableArray<int>)progression.Value!, turn.Section))
            .Where(x => song.Trace.Where(p => p.Point == TracePoints.Planing && p.Section == x.Section).All(p => p.Value is null))
            .Select(x => (x.Turns, x.Progression))).ToArray();

        await Assert.That(sections.Where(x => x.Item1).All(x => x.Item2[^1] == -3)).IsTrue();
        await Assert.That(sections.Count(x => x.Item1) / (double)sections.Length).IsGreaterThan(0.6);
    }

    [Test]
    public async Task AGenresTurnaround_IsNeverPlayedAtThePlainEnd()
    {
        var genre = Genre.Read(new Dictionary<string, IReadOnlyDictionary<string, ByConvention>> { ["turnaround"] = new Dictionary<string, ByConvention> { ["sections"] = new(1, 1, 1) } }, out _)!;

        await Assert.That(genre.Chance(GenreChoices.Turnaround, "sections")!.Value.Plain).IsEqualTo(0);
        await Assert.That(Rmg.Core.Composition.Progressions.Turnaround.At(0)).IsEqualTo(0);
    }
}
