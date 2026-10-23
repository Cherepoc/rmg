using System.Collections.Immutable;
using Rmg.Core.Composition;

namespace Rmg.Tests.Forms;

/// <summary>
///     How every stage treats an intro of entries' window, the first section's first bars played before it: the stages that
///     change a part's notes in the first section's first bars but leave them as they were in the window, where the part
///     plays, which a stage walking the song's plays (<see cref="Rmg.Core.Songs.SongMap.Plays" />) does only at what marks
///     the section's own start, its fill's landing, a lift into it.
/// </summary>
public sealed class WindowStagesReportTest
{
    /// <summary>A track's events in a stretch, each by its place in it, as a stage leaves them: its notes, or before them its states.</summary>
    private static (double At, object Value)[] In(SongDraft draft, int track, double from, double to) =>
        draft.Notes is { } notes
            ? notes.TryGetValue(track, out var timeline) ? [..timeline.Where(x => x.Position >= from - 1e-9 && x.Position < to - 1e-9).Select(x => (Math.Round(x.Position - from, 6), (object)x.Value))] : []
            : draft.Timeline.TrackTimelineMap.TryGetValue(track, out var map)
                ? [..map.EventTimeline.Where(x => x.Position >= from - 1e-9 && x.Position < to - 1e-9).Select(x => (Math.Round(x.Position - from, 6), (object)x.Value))]
                : [];

    [Test]
    [Explicit]
    public async Task Report()
    {
        var missed = new Dictionary<string, int>();
        var windows = 0;
        foreach (var seed in Enumerable.Range(0, 128))
        {
            var song = TestCorpus.Get(seed);
            if (song.Map.Window is not { } window)
                continue;
            windows++;
            var (origin, length) = (song.Map.Origin, window.Duration);
            var previous = new Dictionary<int, ((double, object)[] Window, (double, object)[] First)>();
            SongGenerator.GenerateSong(TestCorpus.SongSeed(seed), Rmg.Core.Composition.SongOverrides.None, ImmutableHashSet<string>.Empty, (stage, draft) =>
            {
                foreach (var track in song.Song.TrackDefinitions.Keys)
                {
                    var now = (Window: In(draft, track, 0, length), First: In(draft, track, origin, origin + length));
                    if (previous.TryGetValue(track, out var before) && now.Window.Length > 0
                        && !before.First.SequenceEqual(now.First) && before.Window.SequenceEqual(now.Window))
                        missed[stage] = missed.GetValueOrDefault(stage) + 1;
                    previous[track] = now;
                }
            }, CancellationToken.None);
        }

        Console.WriteLine($"{windows} songs of 128 with a window: the parts a stage changed in the first section's first bars and not in the window");
        foreach (var (stage, count) in missed.OrderByDescending(x => x.Value))
            Console.WriteLine($"  {stage}: {count}");
        await Task.CompletedTask;
    }
}
