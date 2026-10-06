using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.SongStructures;

/// <summary>A genre's arrangement: a part resting oftener or rarer as the genre says, the songs' rests otherwise.</summary>
public sealed class GenreRestsTest
{
    private static double DrumsRest(Genre genre) => Enumerable.Range(0, 2000)
        .Count(seed => Arrangement.DrawRests(new GenerationContext((ulong)seed), Tilt.None, SectionRole.Verse, ImmutableHashSet<TrackRole>.Empty, genre, 0.5).Contains(TrackRole.Drum)) / 2000.0;

    private static Genre Drums(double value) =>
        Genre.Read(new Dictionary<string, IReadOnlyDictionary<string, ByConvention>> { ["rests"] = new Dictionary<string, ByConvention> { ["drum"] = new(value, value, value) } }, out _)!;

    [Test]
    public async Task AGenresPart_RestsOftenerTheMoreItSays_AndAsTheSongsAtTheMiddle()
    {
        var songs = DrumsRest(Genre.None);

        await Assert.That(DrumsRest(Drums(0.5))).IsEqualTo(songs);
        await Assert.That(DrumsRest(Drums(1))).IsGreaterThan(songs * 3);
        await Assert.That(DrumsRest(Drums(0))).IsLessThan(songs / 2);
    }
}
