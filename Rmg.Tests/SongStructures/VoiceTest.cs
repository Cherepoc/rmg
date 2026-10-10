using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.SongStructures;

/// <summary>Verses for a voice: a genre's verses leaving their melody out for a rap, its choruses keeping their sung hook.</summary>
public sealed class VoiceTest
{
    [Test]
    public async Task AHipHopVerse_LeavesItsMelodyOut_Often_ItsChorusKeepsIt()
    {
        var (verses, versesOut, choruses, chorusesOut) = (0, 0, 0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("hipHop")))))
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

        Console.WriteLine($"melody out: verses {versesOut} of {verses}, choruses {chorusesOut} of {choruses}");
        await Assert.That(versesOut / (double)verses).IsGreaterThan(0.3);
        await Assert.That(chorusesOut / (double)choruses).IsLessThan(0.2);
    }
}
