using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.SongStructures;

/// <summary>Verses for a voice: a genre's verses leaving their melody out for a rap, its choruses keeping their sung hook.</summary>
public sealed class VoiceTest
{
    /// <summary>The melody's rests in verses and in choruses, over the songs of a genre given.</summary>
    private static (int Verses, int VersesOut, int Choruses, int ChorusesOut) Rests(Genre genre)
    {
        var (verses, versesOut, choruses, chorusesOut) = (0, 0, 0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))))
        {
            var structure = song.Trace.Where(x => x.Point == TracePoints.SongForm).Select(x => (SongStructure)x.Value!).Single();
            foreach (var span in song.Song.Map!.Sections)
            {
                var resting = song.Resting(span).Contains(TrackRole.Melody) ? 1 : 0;
                switch (structure.Roles.GetValueOrDefault(span.SectionId))
                {
                    case SectionRole.Verse:
                        (verses, versesOut) = (verses + 1, versesOut + resting);
                        break;
                    case SectionRole.Chorus:
                        (choruses, chorusesOut) = (choruses + 1, chorusesOut + resting);
                        break;
                }
            }
        }

        return (verses, versesOut, choruses, chorusesOut);
    }

    /// <summary>
    ///     Hip-hop's verses leave the melody out far oftener than the same songs' without its voice choice, its choruses
    ///     keeping their hook (measured over 64: 41% of the verses against 2% without, 2% of the choruses).
    /// </summary>
    [Test]
    public async Task AHipHopVerse_LeavesItsMelodyOut_Often_ItsChorusKeepsIt()
    {
        var (verses, versesOut, choruses, chorusesOut) = Rests(GenrePresets.Genre("hipHop"));
        var without = Rests(GenrePresets.Without("hipHop", GenreChoices.Voice.Name));
        Console.WriteLine($"melody out: verses {versesOut} of {verses}, choruses {chorusesOut} of {choruses}; without the voice, verses {without.VersesOut} of {without.Verses}");
        // a share decided once a song, against the mechanism breaking (measured: 41% to 52% of the verses, at offsets 0 and a million)
        await Assert.That(versesOut / (double)verses).IsGreaterThan(0.2);
        await Assert.That(versesOut / (double)verses).IsGreaterThan(2 * without.VersesOut / (double)without.Verses);
        await Assert.That(chorusesOut / (double)choruses).IsLessThan(0.2);
    }
}
