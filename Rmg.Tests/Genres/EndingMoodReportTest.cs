using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>How a song ends against how it played: its last section's harmony and its final chord beside the rest of its sections'.</summary>
public sealed class EndingMoodReportTest
{
    /// <summary>Whether a chord is a plain triad, major or minor, three notes and no other.</summary>
    private static bool Plain(IEnumerable<int> pitches)
    {
        var classes = pitches.Select(x => x % 12).ToHashSet();
        return classes.Count == 3 && Enumerable.Range(0, 12).Any(r => classes.SetEquals([r, (r + 3) % 12, (r + 7) % 12]) || classes.SetEquals([r, (r + 4) % 12, (r + 7) % 12]));
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var (label, base_, genre) in new (string, double?, string?)[] { ("wildest songs", 0.9, null), ("middling songs", null, null), ("djent", null, "djent"), ("blackMetal", null, "blackMetal"), ("idm", null, "idm"), ("metal", null, "metal") })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Base: base_, Genre: genre is null ? null : GenrePresets.Genre(genre))));
            int planedOthers = 0, others = 0, planedLast = 0, raised = 0, plainEnd = 0, ended = 0;
            double plainShare = 0;
            foreach (var song in songs)
            {
                var last = song.Map.Sections[^1].SectionId;
                var planing = song.Trace.Where(t => t.Point == TracePoints.Planing).GroupBy(t => t.Section).ToDictionary(x => x.Key, x => x.First().Value is ImmutableArray<int>);
                foreach (var (id, planes) in planing.Where(x => x.Key != last)) { others++; planedOthers += planes ? 1 : 0; }
                planedLast += planing.GetValueOrDefault(last) ? 1 : 0;
                raised += song.Trace.Where(t => t.Point == TracePoints.CadenceRaise && t.Section == last).Select(t => (((int?, bool))t.Value!).Item2).FirstOrDefault() ? 1 : 0;
                if (!song.Song.Notes!.TryGetValue(SongTracks.ChordsTrack, out var chords) || chords.Count == 0)
                    continue;
                var body = chords.Where(x => x.Position < song.Map.Sections[^1].Start && x.Value.Pitches.Length >= 3).ToArray();
                if (chords.LastOrDefault(x => x.Value.Pitches.Length >= 2) is not { Value: not null } final)
                    continue;
                ended++;
                plainEnd += Plain(final.Value.Pitches) ? 1 : 0;
                plainShare += body.Length == 0 ? 0 : body.Sum(x => Plain(x.Value.Pitches) ? x.Value.Duration : 0) / body.Sum(x => x.Value.Duration);
            }

            var approaches = songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.Progression && t.Section == x.Map.Sections[^1].SectionId).Take(1)
                .Select(t => ((((ImmutableArray<int>)t.Value!)[^1] % 7) + 7) % 7)).ToArray();
            Console.WriteLine($"{label}: the last section's last chord, by degree, {string.Join(", ", approaches.GroupBy(x => x).OrderBy(x => x.Key).Select(x => $"{x.Key + 1}: {x.Count() / (double)approaches.Length:P0}"))}");
            Console.WriteLine($"{label}: sections planed but the last {planedOthers / (double)Math.Max(1, others):P0}, the last {planedLast / (double)songs.Length:P0}; last cadence raised {raised / (double)songs.Length:P0}; " +
                              $"final chord a plain triad {plainEnd / (double)Math.Max(1, ended):P0}, the songs' chords before the last section plain triads {plainShare / Math.Max(1, ended):P0} of their time");
        }

        await Task.CompletedTask;
    }
}
