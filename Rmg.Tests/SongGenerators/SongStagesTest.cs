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
        var song = SongGenerator.GenerateSong((ulong)seed, ProgressionSettings.Default, SongOverrides.None, skipped.ToImmutableHashSet(), CancellationToken.None);
        return (song, [..trace.Entries.Where(x => x.Point == TracePoints.SongStage).Select(x => ((string, ImmutableSortedDictionary<int, int>))x.Value!)]);
    }

    [Test]
    public async Task EveryStage_PlaysInItsOrder_EachRecorded()
    {
        var (_, stages) = Generate(1);

        await Assert.That(stages.Select(x => x.Name)).IsEquivalentTo(SongStages.All.Select(x => x.Name), TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task AStageLeftOut_LeavesWhatTheOthersMakeAsItWas()
    {
        // the cues before a bigger section are the drums' alone: the melody is as it was without them
        foreach (var seed in Enumerable.Range(0, 8))
        {
            var (with, _) = Generate(seed);
            var (without, stages) = Generate(seed, "Lift cues");
            await Assert.That(stages.Select(x => x.Name)).DoesNotContain("Lift cues");
            var (melodyWith, melodyWithout) = (with.Notes![SongTracks.MelodyTrack], without.Notes![SongTracks.MelodyTrack]);
            await Assert.That(melodyWithout.Select(x => (x.Position, x.Value.Pitches[0])))
                .IsEquivalentTo(melodyWith.Select(x => (x.Position, x.Value.Pitches[0])), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
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
