using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Melodies;

/// <summary>The melody's range by genre: a hook's narrow, a jazz tune's wide, the songs' between.</summary>
public sealed class GenreMelodyRangeTest
{
    /// <summary>The melody's span in a section's appearance, its tenth highest note over its tenth lowest, on average over the songs of a genre.</summary>
    private static double Span(Genre genre) => TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre)))
        // a song that leaves its melody out has none to measure
        .Where(song => song.Song.Notes!.ContainsKey(song.Song.Track(TrackRole.Melody)))
        .SelectMany(song => song.Song.Map!.Sections.Select(span => song.Song.Notes![song.Song.Track(TrackRole.Melody)].Where(x => x.Position >= span.Start && x.Position < span.End)
            .Select(x => x.Value.Pitches[0]).Order().ToArray()))
        .Where(x => x.Length >= 10)
        .Average(x => x[^(x.Length / 10 + 1)] - x[x.Length / 10]);

    [Test]
    public async Task AHipHopMelody_KeepsNarrower_AndAJazzOne_Wider_ThanWithoutTheirRange()
    {
        // each preset against the same songs of it without its melody's range, the songs' own
        double Without(string preset) => Span(GenrePresets.Without(preset, GenreChoices.MelodyRange.Name));
        var (hipHop, hipHopOwn, jazz, jazzOwn) = (Span(GenrePresets.Genre("hipHop")), Without("hipHop"), Span(GenrePresets.Genre("jazz")), Without("jazz"));
        Console.WriteLine($"the melody's span: hip hop's {hipHop:F2} against {hipHopOwn:F2} without its range, jazz's {jazz:F2} against {jazzOwn:F2}");

        await Assert.That(MelodyLayers.LineOf(Genre.None).RangeWidth).IsEqualTo(MelodyLayers.Line.RangeWidth);
        // measured hip hop's 6.5 to 7.3 against 8.1 to 8.2, jazz's 10.7 to 11.2 against 10.1 to 10.2, at offsets 0 and a million
        await Assert.That(hipHop).IsLessThan(hipHopOwn);
        await Assert.That(jazz).IsGreaterThan(jazzOwn);
    }
}
