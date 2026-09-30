using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;

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

            // nearly every one, a step and a section's own key now and then coming back to the key it left
            await Assert.That(changes.All(x => PatternStarts(song).Contains(x.Position))).IsTrue();
            await Assert.That(changes.Length).IsGreaterThanOrEqualTo(PatternStarts(song).Length * 3 / 4);
            await Assert.That(changes.All(x => x.Semitones is >= -5 and <= 6)).IsTrue();
            // every change moves the key
            await Assert.That(changes.Zip(changes.Skip(1)).All(x => x.First.Semitones != x.Second.Semitones)).IsTrue();
            steps.AddRange(changes.Zip(changes.Skip(1), (first, second) => (second.Semitones - first.Semitones).Mod(12)));
        }

        // far beyond the whole and half steps up of a last chorus
        await Assert.That(steps.Distinct().Count()).IsEqualTo(11);
    }

    [Test]
    public async Task AMiddlingSong_ChangesKeyOnlyWhereASectionStarts_ALastSectionThatCameBackGoingUpAWholeOrAHalfStep()
    {
        var middling = new SongOverrides(Base: 0.5, Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Scale, 0.5));
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, middling));
        var lifts = 0;
        foreach (var song in songs)
        {
            var keys = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.KeyOffset);
            await Assert.That(Changes(song).All(x => song.Map.Sections.Any(y => y.Start == x.Position))).IsTrue();

            // the last section in its key, or a whole or half step above it where it played before
            var structure = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
            var last = song.Map.Sections[structure.SectionIds.Length - 1];
            var before = song.Map.Sections.Take(structure.SectionIds.Length - 1).FirstOrDefault(x => x.SectionId == last.SectionId);
            if (before is null)
                continue;

            var lift = (keys.GetEffectiveValueAt(last.Start) - keys.GetEffectiveValueAt(before.Start)).Mod(12);
            await Assert.That(lift).IsBetween(0, 2).Because($"seed {song.Seed}");
            lifts += lift > 0 ? 1 : 0;
        }

        await Assert.That(lifts).IsGreaterThan(0);
    }
}
