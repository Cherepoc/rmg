using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.SongGenerators;

/// <summary>The song's stages after it is put together (<see cref="SongStages" />): their order, each recorded, and one left out.</summary>
public sealed class SongStagesTest
{
    /// <summary>The song of a seed with the stages given left out, and what its trace recorded of the stages.</summary>
    private static (Song Song, (string Name, ImmutableSortedDictionary<int, int> Counts)[] Stages) Generate(int seed, params string[] skipped)
    {
        using var trace = StateTrace.Start(explains: false);
        var song = SongGenerator.GenerateSong(TestCorpus.SongSeed(seed), SongOverrides.None, skipped.ToImmutableHashSet(), (_, _) => { }, CancellationToken.None);
        return (song, [..trace.Entries.Where(x => x.Point == TracePoints.SongStage).Select(x => ((string, ImmutableSortedDictionary<int, int>))x.Value!)]);
    }

    [Test]
    public async Task EveryStage_PlaysInItsOrder_EachRecorded()
    {
        var (_, stages) = Generate(1);

        await Assert.That(stages.Select(x => x.Name)).IsEquivalentTo(SongStages.All.Select(x => x.Name), CollectionOrdering.Matching);
    }

    [Test]
    public async Task AStageLeftOut_LeavesWhatTheOthersMakeAsItWas()
    {
        // the cues before a bigger section are the drums' alone: every other part's notes are as they were without them
        foreach (var seed in Enumerable.Range(0, 8))
        {
            var (with, _) = Generate(seed);
            var (without, stages) = Generate(seed, "Lift cues");
            await Assert.That(stages.Select(x => x.Name)).DoesNotContain("Lift cues");
            foreach (var (track, notes) in with.Notes!.Where(x => with.TrackDefinitions[x.Key].Role != TrackRole.Drum))
                await Assert.That(without.Notes![track].Select(x => (x.Position, x.Value.Duration, string.Join(",", x.Value.Pitches))))
                    .IsEquivalentTo(notes.Select(x => (x.Position, x.Value.Duration, string.Join(",", x.Value.Pitches))), CollectionOrdering.Matching).Because($"seed {seed}, track {track}");
        }
    }

    [Test]
    public async Task EveryStage_LeavesTheSongWhole_ItsEventsWithinIt_InOrder_ItsNotesPlayable()
    {
        var problems = TestCorpus.InParallel(Enumerable.Range(0, 32), seed =>
        {
            var found = new List<string>();
            SongGenerator.GenerateSong(TestCorpus.SongSeed(seed), SongOverrides.None, ImmutableHashSet<string>.Empty, (stage, draft) =>
            {
                var duration = draft.Timeline.Duration;
                foreach (var (track, events) in draft.Timeline.TrackTimelineMap)
                {
                    var positions = events.EventTimeline.Select(x => x.Position).ToArray();
                    if (positions.Any(x => x < -1e-9 || x > duration + 1e-9))
                        found.Add($"seed {seed}, after {stage}: track {track} has an event outside the song");
                    if (positions.Zip(positions.Skip(1)).Any(x => x.Second < x.First - 1e-9))
                        found.Add($"seed {seed}, after {stage}: track {track} has its events out of order");
                }
                foreach (var (track, notes) in draft.Notes ?? ImmutableSortedDictionary<int, EventTimeline<RealizedNote>>.Empty)
                    if (notes.Any(x => x.Value.Duration <= 0 || x.Value.Pitches.Any(p => p is < 0 or > 127)))
                        found.Add($"seed {seed}, after {stage}: track {track} has a note no MIDI plays");
            }, CancellationToken.None);
            return found;
        }).SelectMany(x => x).ToArray();

        await Assert.That(problems).IsEmpty();
    }

    [Test]
    public async Task AStageAnotherReads_MayNotBeLeftOut()
    {
        await Assert.That(() => Generate(1, "Fills")).Throws<ArgumentException>();
        await Assert.That(() => Generate(1, "No such stage")).Throws<ArgumentException>();
    }

    /// <summary>How many events or notes each stage adds or takes away, over the corpus, as its tracks have them after it.</summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        var changes = TestCorpus.Measure(64, song =>
        {
            var stages = song.Trace.Where(x => x.Point == TracePoints.SongStage).Select(x => ((string Name, ImmutableSortedDictionary<int, int> Counts))x.Value!).ToArray();
            // a stage's change against the one before it, the first's against nothing, the notes realized against the events
            return stages.Select((x, i) => (x.Name, Added: x.Counts.Sum(c => Math.Max(0, c.Value - (i > 0 ? stages[i - 1].Counts.GetValueOrDefault(c.Key) : 0))),
                Removed: i > 0 ? stages[i - 1].Counts.Sum(c => Math.Max(0, c.Value - x.Counts.GetValueOrDefault(c.Key))) : 0)).ToArray();
        });
        foreach (var stage in SongStages.All)
        {
            var of = changes.SelectMany(x => x).Where(x => x.Name == stage.Name).ToArray();
            Console.WriteLine($"{stage.Name}: {of.Average(x => x.Added):0} added, {of.Average(x => x.Removed):0} taken away a song");
        }

        await Task.CompletedTask;
    }
}
