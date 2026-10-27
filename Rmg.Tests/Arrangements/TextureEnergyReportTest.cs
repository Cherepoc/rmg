using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Arrangements;

/// <summary>
///     How an appearance's texture follows its section's energy (<see cref="Texture.Kinds" />): each texture's share of the
///     quietest, the middle and the loudest third of the appearances, and how much of the band plays the first phrase of
///     the loudest third, which a texture of fewer parts empties.
/// </summary>
public sealed class TextureEnergyReportTest
{
    [Test]
    [Explicit]
    [Arguments(null)]
    [Arguments("trance")]
    [Arguments("postRock")]
    [Arguments("ambient")]
    [Arguments("metal")]
    public async Task Report(string? genre)
    {
        var appearances = new List<(double Energy, TextureKind Kind, double FirstPhrase)>();
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre is null ? null : GenrePresets.Genre(genre)))))
        {
            var energies = song.Trace.Where(x => x.Point == TracePoints.SectionEnergy).DistinctBy(x => x.Section).ToDictionary(x => x.Section, x => ((SectionEnergyTrace)x.Value!).Energy);
            var roles = song.Song.TrackDefinitions.Values.Select(x => x.Role).Where(x => x != TrackRole.Drum).ToHashSet();
            foreach (var span in song.PlayedSpans)
            {
                var (_, kind, silent) = song.Texture(span);
                var playing = roles.Except(song.Resting(span)).ToArray();
                var first = playing.Length == 0 ? 1 : playing.Count(x => silent.Length == 0 || !silent[0].Contains(x)) / (double)playing.Length;
                appearances.Add((energies[span.SectionId], kind, first));
            }
        }

        var ordered = appearances.OrderBy(x => x.Energy).ToArray();
        var thirds = Enumerable.Range(0, 3).Select(t => ordered.Skip(t * ordered.Length / 3).Take((t + 1) * ordered.Length / 3 - t * ordered.Length / 3).ToArray()).ToArray();
        string Shares(IReadOnlyCollection<(double Energy, TextureKind Kind, double FirstPhrase)> part) =>
            string.Join(" ", Enum.GetValues<TextureKind>().Select(k => $"{k} {part.Count(x => x.Kind == k) / (double)Math.Max(1, part.Count):P0}"));
        Console.WriteLine($"{genre ?? "corpus"}: {ordered.Length} appearances; quietest third {Shares(thirds[0])}; middle {Shares(thirds[1])}; loudest {Shares(thirds[2])}; " +
                          $"the loudest third's first phrase playing {thirds[2].Average(x => x.FirstPhrase):P0} of its pitched parts");
        await Task.CompletedTask;
    }
}
