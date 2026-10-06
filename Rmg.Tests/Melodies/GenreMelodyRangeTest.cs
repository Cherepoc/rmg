using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Melodies;

/// <summary>The melody's range by genre: a hook's narrow, a jazz tune's wide, the songs' between.</summary>
public sealed class GenreMelodyRangeTest
{
    /// <summary>The melody's span in a section's appearance, its tenth highest note over its tenth lowest, on average over the songs of a genre.</summary>
    private static double Span(string? genre) => TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre is null ? null : GenrePresets.Genre(genre))))
        .SelectMany(song => song.Song.Map!.Sections.Select(span => song.Song.Notes![SongTracks.MelodyTrack].Where(x => x.Position >= span.Start && x.Position < span.End)
            .Select(x => x.Value.Pitches[0]).Order().ToArray()))
        .Where(x => x.Length >= 10)
        .Average(x => x[^(x.Length / 10 + 1)] - x[x.Length / 10]);

    [Test]
    public async Task AHipHopMelody_KeepsNarrower_AndAJazzOne_Wider_ThanTheSongs()
    {
        var songs = Span(null);

        await Assert.That(MelodyLayers.LineOf(Genre.None).RangeWidth).IsEqualTo(MelodyLayers.Line.RangeWidth);
        await Assert.That(Span("hipHop")).IsLessThan(songs);
        await Assert.That(Span("jazz")).IsGreaterThan(songs);
    }
}
