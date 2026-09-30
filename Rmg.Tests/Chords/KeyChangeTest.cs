using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;

namespace Rmg.Tests.Chords;

public sealed class KeyChangeTest
{
    private static ImmutableArray<KeyChange> Changes(CorpusSong song) =>
        (ImmutableArray<KeyChange>)song.Trace.Single(x => x.Point == TracePoints.KeyChange).Value!;

    // every pattern's start in the song's sections, the song's own first left out
    private static double[] PatternStarts(CorpusSong song) => song.Map.Sections
        .SelectMany(x => Enumerable.Range(0, (int)Math.Round((x.End - x.Start) / song.Map.Meter.PatternDuration)).Select(p => x.Start + p * song.Map.Meter.PatternDuration))
        .Skip(1)
        .ToArray();

    [Test]
    public async Task ThePlainestSongs_KeepTheirKey()
    {
        foreach (var seed in Enumerable.Range(0, 8))
            await Assert.That(Changes(TestCorpus.Get(seed, new SongOverrides(Base: 0)))).IsEmpty();
    }

    [Test]
    public async Task TheWildestSongs_ChangeKeyAtEveryPatternsStart_ByAnyStep()
    {
        var steps = new List<int>();
        foreach (var seed in Enumerable.Range(0, 8))
        {
            var song = TestCorpus.Get(seed, new SongOverrides(Base: 1));
            var changes = Changes(song);

            await Assert.That(changes.Select(x => x.Position).ToArray()).IsEquivalentTo(PatternStarts(song));
            await Assert.That(changes.All(x => x.Semitones is >= -5 and <= 6)).IsTrue();
            // every change moves the key
            await Assert.That(changes.Zip(changes.Skip(1)).All(x => x.First.Semitones != x.Second.Semitones)).IsTrue();
            steps.AddRange(changes.Zip(changes.Skip(1), (first, second) => (second.Semitones - first.Semitones).Mod(12)));
        }

        // far beyond the whole and half steps up of a last chorus
        await Assert.That(steps.Distinct().Count()).IsEqualTo(11);
    }

    [Test]
    public async Task AMiddlingSong_ChangesKeyOnlyForALastSectionThatCameBack_UpAWholeOrAHalfStep()
    {
        var middling = new SongOverrides(Base: 0.5, Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Scale, 0.5));
        var changes = TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, middling))
            .Select(x => (Song: x, Changes: Changes(x)))
            .Where(x => !x.Changes.IsEmpty)
            .ToArray();

        await Assert.That(changes.Length).IsGreaterThan(0);
        foreach (var (song, change) in changes)
        {
            // the section it starts, and one before it the same, a fading song's last played once or twice more after it
            var at = song.Map.Sections.Select((x, i) => (Section: x, Index: i)).Single(x => x.Section.Start == change.Single().Position);
            await Assert.That(song.Map.Sections.Take(at.Index).Any(x => x.SectionId == at.Section.SectionId)).IsTrue();
            await Assert.That(at.Index).IsGreaterThanOrEqualTo(song.Map.Sections.Length - 3);
            await Assert.That(change.Single().Semitones).IsBetween(1, 2);
        }
    }
}
