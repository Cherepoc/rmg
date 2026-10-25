using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>A line heard plain and then again tremolo-picked, in one song, as the tremolo follows the energy (<see cref="Articulations.TremoloLean" />).</summary>
public sealed class TremoloRepriseTest
{
    private static readonly TrackRole[] Parts = [TrackRole.Riff, TrackRole.RiffTwin, TrackRole.Rhythm, TrackRole.Melody, TrackRole.CounterMelody, TrackRole.Bass];

    /// <summary>
    ///     A genre's songs by their parts: the songs where a part plays a line of long notes, a beat or longer, in two of its
    ///     appearances or more, a line the section's or the one whose lines it plays, and those where it plays one plain in
    ///     one appearance and picked in another, and plain and later picked; how often a line's first hearing is picked, the
    ///     strokes by their step, and each part's appearances with long notes, and those picked (measured over 64 songs on 25
    ///     October 2026, a section's tremolo drawn for every time it played: Black metal's lines plain and later picked in 4
    ///     songs, its first hearings picked 79 of 711, Surf's 6 and 69 of 624; the tremolo following the appearances' energy:
    ///     48 and 146 of 745, Surf's 56 and 110 of 638, Black metal's riff picking 254 of its 409 appearances and its rhythm
    ///     part 141 of 244, its strokes four times as many).
    /// </summary>
    [Test]
    [Explicit]
    [Arguments("blackMetal")]
    [Arguments("surf")]
    public async Task Report(string genre)
    {
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))).ToArray();
        var (recurring, contrasted, building, picking) = (0, 0, 0, 0);
        var steps = new Dictionary<double, int>();
        var (firsts, firstsPicked) = (0, 0);
        var counts = Parts.ToDictionary(x => x, _ => (Long: 0, Picked: 0, Recurring: 0));
        foreach (var song in songs)
        {
            var sources = song.Trace.Where(x => x.Point == TracePoints.LinesFrom).ToDictionary(x => x.Section, x => (int)x.Value!);
            var (recurs, contrasts, builds) = (false, false, false);
            picking += song.Trace.Any(x => x.Point == TracePoints.Tremolo) ? 1 : 0;
            foreach (var role in Parts.Where(x => song.Song.TrackDefinitions.Values.Any(t => t.Role == x)))
            {
                var notes = song.Song.Notes!.GetValueOrDefault(song.Track(role))?.ToArray() ?? [];
                // an appearance's long notes, a picked one's first stroke among them, and whether it picks them
                var appearances = notes.Where(x => x.Value.Duration >= Articulations.TremoloStrokes * Articulations.TremoloSteps[0] - 1e-9 && x.Value.State.GetStateValue(CompositionStateKinds.Picked) == 0
                                                   || x.Value.State.GetStateValue(CompositionStateKinds.Picked) == 1)
                    .Select(x => song.Map.PlayingAt(x.Position)).Where(x => x is { IsEnding: false })
                    .GroupBy(x => x!.Value.Index)
                    .Select(x => (Index: x.Key, Line: sources.GetValueOrDefault(x.First()!.Value.Span.SectionId, x.First()!.Value.Span.SectionId),
                        Picked: notes.Any(n => n.Value.State.GetStateValue(CompositionStateKinds.Picked) > 0 && song.Map.PlayingAt(n.Position)?.Index == x.Key)))
                    .ToArray();
                var (longs, picked, again) = counts[role];
                counts[role] = (longs + appearances.Length, picked + appearances.Count(x => x.Picked), again + appearances.GroupBy(x => x.Line).Where(x => x.Count() > 1).Sum(x => x.Count()));
                recurs |= role is TrackRole.Riff or TrackRole.Rhythm or TrackRole.Melody && appearances.GroupBy(x => x.Line).Any(x => x.Count() > 1);
                contrasts |= appearances.GroupBy(x => x.Line).Any(x => x.Any(a => a.Picked) && x.Any(a => !a.Picked));
                // a line heard plain and later picked
                builds |= appearances.GroupBy(x => x.Line).Any(x => x.Any(a => !a.Picked && x.Any(b => b.Picked && b.Index > a.Index)));
                // a line's first hearing, picked or not
                firsts += appearances.GroupBy(x => x.Line).Count();
                firstsPicked += appearances.GroupBy(x => x.Line).Count(x => x.MinBy(a => a.Index).Picked);
                // the steps its strokes are struck at
                foreach (var (a, b) in notes.Zip(notes.Skip(1)).Where(x => x.First.Value.State.GetStateValue(CompositionStateKinds.Picked) is > 0 and var k && x.Second.Value.State.GetStateValue(CompositionStateKinds.Picked) == k + 1))
                    steps[Math.Round(b.Position - a.Position, 3)] = steps.GetValueOrDefault(Math.Round(b.Position - a.Position, 3)) + 1;
            }

            recurring += recurs ? 1 : 0;
            contrasted += contrasts ? 1 : 0;
            building += builds ? 1 : 0;
        }

        Console.WriteLine($"{genre}: {songs.Length} songs, {picking} picking, {recurring} with a riff's, a rhythm's or a melody's line of long notes recurring, {contrasted} with a line plain and picked, {building} plain and later picked");
        Console.WriteLine($"  lines' first hearings picked {firstsPicked} of {firsts}");
        Console.WriteLine($"  strokes by step: {string.Join(", ", steps.OrderByDescending(x => x.Key).Select(x => $"{x.Key} {x.Value}"))}");
        foreach (var (role, (longs, picked, again)) in counts)
            Console.WriteLine($"  {role,-14} appearances with long notes {longs,4}, of a line recurring {again,4}, picked {picked,4}");
        await Task.CompletedTask;
    }
}
