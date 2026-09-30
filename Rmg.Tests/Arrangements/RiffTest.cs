using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Arrangements;

public sealed class RiffTest
{
    private static ImmutableHashSet<TrackRole> Absent(CorpusSong song) =>
        (ImmutableHashSet<TrackRole>)song.Trace.Single(x => x.Point == TracePoints.SongParts).Value!;

    private static (int Index, TrackRole Source, TrackRole Target, int Step)[] Doublings(CorpusSong song) =>
        [..song.Trace.Where(x => x.Point == TracePoints.LineDoubling).Select(x => ((int, TrackRole, TrackRole, int))x.Value!)];

    [Test]
    public async Task HalfTheSongs_HaveARiff()
    {
        var absent = TestCorpus.Measure(256, Absent);

        await Assert.That(absent.Count(x => !x.Contains(TrackRole.Riff)) / 256.0).IsEqualTo(0.5).Within(0.08);
    }

    [Test]
    public async Task ADoubledLine_PlaysTheOthersNotes_AtTheirPlaces()
    {
        var doubled = 0;
        foreach (var song in TestCorpus.Range(64))
        foreach (var (index, source, target, _) in Doublings(song))
        {
            var span = song.Map.Sections[index];
            double[] At(TrackRole role) => [..song.Song.Notes!.Single(x => song.Song.TrackDefinitions[x.Key].Role == role).Value
                .Where(x => x.Position >= span.Start && x.Position < span.End).Select(x => x.Position)];
            var (sourceAt, targetAt) = (At(source), At(target));
            if (targetAt.Length == 0)
                continue;

            // the doubling part's notes are the doubled part's, where it plays, and a held note struck again where a chord changes
            var changes = song.ChordChanges;
            await Assert.That(targetAt.All(x => sourceAt.Concat(changes).Any(y => Math.Abs(x - y) < 1e-6))).IsTrue().Because($"seed {song.Seed}, section {index}");
            doubled++;
        }

        await Assert.That(doubled).IsGreaterThan(10);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Range(256);
        var withRiff = songs.Where(x => !Absent(x).Contains(TrackRole.Riff)).ToArray();
        var riffSections = withRiff.Sum(x => x.Map.Sections.Count(span => !x.Resting(span).Contains(TrackRole.Riff)));
        var sections = withRiff.Sum(x => x.Map.Sections.Length);
        var doublings = songs.SelectMany(Doublings).ToArray();
        Console.WriteLine($"a riff in {withRiff.Length} of {songs.Length} songs, playing in {riffSections / (double)sections:P0} of their sections; " +
                          $"doublings: {string.Join(", ", doublings.GroupBy(x => (x.Source, x.Target)).Select(x => $"{x.Key.Target} with {x.Key.Source} {x.Count()} ({string.Join("/", x.GroupBy(y => y.Step).OrderBy(y => y.Key).Select(y => $"{y.Key}:{y.Count()}"))})"))}");
        await Task.CompletedTask;
    }
}
