using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Fills;

/// <summary>The fills' sounds by convention: a plain section's fills and landings crash, a wild one's reach for the china and the splash.</summary>
public sealed class FillSoundsByConventionTest
{
    /// <summary>
    ///     The cymbal's hits of the songs' drums at a fills facet, the china's or the splash's among them: those of the fills
    ///     and their landings, from a fill's start to a 16th past its line, and those of the whole song, the groove's
    ///     cymbal strokes and a drum solo's stabs among them, which the fills facet does not lean.
    /// </summary>
    private static (double Fills, double Song) ChinaAndSplash(double fills)
    {
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Facets: TestCorpus.FacetsOf((Facet.Fills, fills))))).Where(x => x.PlaysDrums).ToArray();
        static bool Cymbal(int x) => x is 49 or 52 or 55 or 57;
        static double Share(int[] hits) => hits.Length > 0 ? hits.Count(x => x is 52 or 55) / (double)hits.Length : double.NaN;
        var all = songs.SelectMany(x => x.Rendered.Tracks.Where(t => t.IsPercussionInstrument).SelectMany(t => t.NoteTimeline)).Select(x => x.Value.Offset).Where(Cymbal).ToArray();
        var played = songs.SelectMany(song =>
        {
            var decisions = song.Trace.Where(x => x.Point == TracePoints.FillDecision).Select(x => (FillDecision)x.Value!).ToArray();
            var lines = song.FillLines();
            if (lines.Length != decisions.Length)
                throw new InvalidOperationException($"seed {song.Seed}: {lines.Length} lines, {decisions.Length} fill decisions");
            var windows = lines.Zip(decisions).Where(x => x.Second.Span > 0).Select(x => (From: x.First - x.Second.Span, To: x.First + 0.25)).ToArray();
            return song.Song.Notes!.Where(x => song.IsDrum(x.Key)).SelectMany(x => x.Value)
                .Where(n => windows.Any(w => n.Position >= w.From - 1e-6 && n.Position < w.To - 1e-6)).SelectMany(n => n.Value.Pitches);
        }).Where(Cymbal).ToArray();
        Console.WriteLine($"fills {fills}: {played.Length} cymbal hits in the fills and landings, china or splash {Share(played):P1}; {all.Length} in the songs, {Share(all):P1}");
        return (Share(played), Share(all));
    }

    [Test]
    public async Task APlainSectionsCymbals_AreCrashes_AndAWildOnesChinasAndSplashes_Oftener()
    {
        var (plain, middle, wild) = (ChinaAndSplash(0), ChinaAndSplash(0.5), ChinaAndSplash(1));
        // a plain fill and its landing crash, the china and the splash no weight in them (FillSounds), but where the groove's
        // own cymbal strokes on a china fall in the window: seen 0.7% to 2.5% over three corpora's 64 songs, 29% to 30% wild
        await Assert.That(plain.Fills).IsLessThan(0.08);
        await Assert.That(wild.Fills).IsGreaterThan(middle.Fills);
        // the songs' cymbals at large, the groove's strokes among them: seen 2.0% to 10.6% over three corpora's 64 songs
        await Assert.That(plain.Song).IsLessThan(0.25);
    }
}
