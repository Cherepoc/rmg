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
        .SelectMany(x => x.Patterns.Select(p => p.Start))
        .Skip(1)
        .ToArray();

    [Test]
    public async Task ALastSectionPlayedTwice_IsLiftedAtItsRepeat_NowAndThen()
    {
        var (repeated, lifted) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 256), seed => TestCorpus.Get(seed, new SongOverrides(Facets: TestCorpus.FacetsOf((Facet.Form, 0.5), (Facet.Scale, 0.6))))))
        {
            if (!(bool)song.Trace.Single(x => x.Point == TracePoints.LastRepeat).Value!)
                continue;

            // the last two sections the same, the key moving where the repeat starts now and then, a section's own key apart
            var sections = song.Map.Sections;
            var (first, repeat) = (sections[^2], sections[^1]);
            await Assert.That(first.SectionId).IsEqualTo(repeat.SectionId);
            repeated++;
            if (Changes(song).Any(x => Math.Abs(x.Position - repeat.Start) < 1e-9))
                lifted++;
        }

        await Assert.That(repeated).IsGreaterThan(10);
        await Assert.That(lifted).IsGreaterThan(0);
    }

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
            var starts = PatternStarts(song);

            // nearly every one, a step and a section's own key now and then coming back to the key it left
            await Assert.That(changes.All(x => starts.Any(start => Math.Abs(start - x.Position) < 1e-9))).IsTrue();
            await Assert.That(changes.Length).IsGreaterThanOrEqualTo(starts.Length * 3 / 4);
            // within a fifth of the song's key either way, each the nearest to the key before
            await Assert.That(changes.All(x => x.Semitones is >= -7 and <= 7)).IsTrue();
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
        var middling = new SongOverrides(Base: 0.5, Facets: TestCorpus.FacetsOf((Facet.Scale, 0.5)));
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, middling));
        var lifts = 0;
        foreach (var song in songs)
        {
            var keys = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.KeyOffset);
            await Assert.That(Changes(song).All(x => song.Map.Sections.Any(y => y.Start == x.Position))).IsTrue();

            // the last section in its key, or a whole or half step above it where it played before: the appearance the lift
            // may come at, its repeat where it plays twice, the one before an outro where there is one
            var structure = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
            var ids = structure.SectionIds;
            var at = ids.Length - 1 + ((bool)song.Trace.Single(x => x.Point == TracePoints.LastRepeat).Value! ? 1 : 0) - (structure.Roles[ids[^1]] == SectionRole.Outro ? 1 : 0);
            var last = song.Map.Sections[at];
            var before = song.Map.Sections.Take(at).FirstOrDefault(x => x.SectionId == last.SectionId);
            if (before is null)
                continue;

            var lift = (keys.GetEffectiveValueAt(last.Start) - keys.GetEffectiveValueAt(before.Start)).Mod(12);
            await Assert.That(lift).IsBetween(0, 2).Because($"seed {song.Seed}");
            lifts += lift > 0 ? 1 : 0;
        }

        await Assert.That(lifts).IsGreaterThan(0);
    }
}
