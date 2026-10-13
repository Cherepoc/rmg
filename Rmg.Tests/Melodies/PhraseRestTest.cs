using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Melodies;

/// <summary>A melody's phrase within its pattern ending in a rest, as a blues' every line does.</summary>
public sealed class PhraseRestTest
{
    /// <summary>The share of a pattern's inner phrases whose last bar has a rest of half a beat or more, or no melody at all.</summary>
    internal static double InnerRests(IEnumerable<CorpusSong> songs)
    {
        var (rests, count) = (0, 0);
        foreach (var song in songs.Where(x => !x.Absent.Contains(TrackRole.Melody)))
        foreach (var span in song.Map.Sections.Where(s => !song.IsSolo(s) && s.Meter.Phrases > 1))
        {
            var notes = song.Notes(song.Track(TrackRole.Melody)).Where(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9)
                .GroupBy(x => Math.Round(x.Position, 6)).Select(g => g.MaxBy(x => x.Value.Offset)).ToArray();
            var bar = span.Meter.BarDuration;
            for (var end = span.Start + span.Meter.PhraseDuration; end < span.End - 1e-9; end += span.Meter.PhraseDuration)
            {
                if (Math.Abs((end - span.Start) % span.Meter.PatternDuration) < 1e-6)
                    continue;
                var inBar = Enumerable.Range(0, notes.Length).Where(i => notes[i].Position >= end - bar - 1e-9 && notes[i].Position < end - 1e-9).ToArray();
                count++;
                rests += inBar.Length == 0 || inBar.Any(i => (i + 1 == notes.Length ? end : notes[i + 1].Position) - (notes[i].Position + notes[i].Value.Duration) >= 0.5 - 1e-6) ? 1 : 0;
            }
        }

        return rests / (double)Math.Max(1, count);
    }

    [Test]
    public async Task APhraseWithinItsPattern_EndsInARest()
    {
        var share = InnerRests(TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("blues")))));
        Console.WriteLine($"{share:P0} of Blues' inner phrases end in a rest");
        // measured 100% at offset 0, 32% before every phrase ended
        await Assert.That(share).IsGreaterThan(0.9);
    }
}
