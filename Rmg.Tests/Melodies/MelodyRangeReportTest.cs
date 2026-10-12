using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Melodies;

/// <summary>How wide a genre's melody ranges in a section, from its lowest note to its highest.</summary>
public sealed class MelodyRangeReportTest
{
    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var genre in new[] { "nuMetal", "thrash", "punk", "trap", "hipHop", "rock" })
        {
            var ranges = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre))))
                .Where(x => !x.Absent.Contains(TrackRole.Melody))
                .SelectMany(x => x.Map.Sections.Select(s => x.Notes(x.Track(TrackRole.Melody)).Where(n => n.Position >= s.Start - 1e-9 && n.Position < s.End - 1e-9).Select(n => n.Value.Offset).ToArray()))
                .Where(x => x.Length > 1).Select(x => (double)(x.Max() - x.Min())).Order().ToArray();
            Console.WriteLine($"{genre}: a section's melody spans {ranges.Average():0.0} semitones, its median {ranges[ranges.Length / 2]}");
        }

        await Task.CompletedTask;
    }
}
