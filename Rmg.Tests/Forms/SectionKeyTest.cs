using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.Forms;

public sealed class SectionKeyTest
{
    [Test]
    public async Task ASectionInAKeyOfItsOwn_PlaysItEveryTime_AndTheFirstInTheSongs()
    {
        var songs = TestCorpus.Range(128)
            .Where(x => ((Unconventionality)x.Trace.Single(t => t.Point == TracePoints.SongUnconventionality).Value!)[Facet.Scale] <= 0.5)
            .ToArray();
        var ownKeys = 0;
        foreach (var song in songs)
        {
            // but for a last section going up, which only the last does, every section starts in its key every time
            var keys = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.KeyOffset);
            var home = keys.GetEffectiveValueAt(song.Map.Sections[0].Start);
            var structure = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
            var played = song.Map.Sections.Take(structure.SectionIds.Length - 1).ToArray();
            foreach (var appearances in played.GroupBy(x => x.SectionId))
                await Assert.That(appearances.Select(x => keys.GetEffectiveValueAt(x.Start)).Distinct().Count()).IsEqualTo(1).Because($"seed {song.Seed}");

            await Assert.That(played.Where(x => x.SectionId == song.Map.Sections[0].SectionId).All(x => keys.GetEffectiveValueAt(x.Start) == home)).IsTrue();
            ownKeys += played.Any(x => keys.GetEffectiveValueAt(x.Start) != home) ? 1 : 0;
        }

        await Assert.That(ownKeys).IsGreaterThan(0);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        // every section by its role and its song's scale fifth: whether it is in another scale than the song's, and in
        // another key than the song's where it starts
        var sections = TestCorpus.Measure(1024, song =>
        {
            var structure = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
            var (_, songScale) = ((HarmonicUnconventionality, Scale))song.Trace.Single(x => x.Point == TracePoints.SongHarmony).Value!;
            var facet = ((Unconventionality)song.Trace.Single(x => x.Point == TracePoints.SongUnconventionality).Value!)[Facet.Scale];
            var scales = song.Trace.Where(x => x.Point == TracePoints.SectionScale).ToDictionary(x => x.Section, x => (Scale)x.Value!);
            var keys = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.KeyOffset);
            return song.Map.Sections.Skip(1).DistinctBy(x => x.SectionId).Select(x => (
                Band: Math.Clamp((int)(facet * 5), 0, 4),
                Role: structure.Roles[x.SectionId],
                OtherScale: scales[x.SectionId] != songScale,
                OtherKey: keys.GetEffectiveValueAt(x.Start) != keys.GetEffectiveValueAt(song.Map.Sections[0].Start)
            )).ToArray();
        }).SelectMany(x => x).ToArray();

        foreach (var band in sections.GroupBy(x => x.Band).OrderBy(x => x.Key))
            Console.WriteLine($"  scale {band.Key}/5: " + string.Join("; ", band.GroupBy(x => x.Role is SectionRole.Chorus or SectionRole.Bridge ? "chorus or bridge" : "other")
                .OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count()}, other scale {x.Count(y => y.OtherScale) / (double)x.Count():P0}, other key {x.Count(y => y.OtherKey) / (double)x.Count():P0}")));
        await Task.CompletedTask;
    }
}
