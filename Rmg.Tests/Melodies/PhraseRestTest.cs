using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Melodies;

/// <summary>A melody's phrase within its pattern ending in a rest, as a blues' every line does, and a sung section's at half its phrase too.</summary>
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

    private static Genre Sung(double chance) => new(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty.Add(GenreChoices.Sung.Name,
        ImmutableDictionary<string, ByConvention>.Empty.Add("halves", new ByConvention(chance, chance, chance))));

    /// <summary>Every section's phrase ends, by bar of its pattern, as traced, with its phrase's length.</summary>
    private static (int PhraseBars, ImmutableArray<int> Ends)[] Ends(CorpusSong song) =>
        [..song.Trace.Where(t => t.Point == TracePoints.MelodyPhraseEnds).Select(t => (song.MeterOf(t.Section).PhraseBars, (ImmutableArray<int>)t.Value!))];

    /// <summary>
    ///     A sung section ends its melody's every phrase at its half too, and an unsung one never, its phrases' own ends the
    ///     same either way, drawn apart; and its melody rests there far more often.
    /// </summary>
    [Test]
    public async Task ASungSection_RestsAtHalfItsPhrase_ItsPhrasesEndsAsTheyWere()
    {
        var seeds = Enumerable.Range(0, 64).ToArray();
        var sung = TestCorpus.InParallel(seeds, seed => TestCorpus.Get(seed, new SongOverrides(Genre: Sung(1))));
        var unsung = TestCorpus.InParallel(seeds, seed => TestCorpus.Get(seed, new SongOverrides(Genre: Sung(0))));
        var halves = 0;
        foreach (var (on, off) in sung.Zip(unsung))
        {
            // the same sections sung and not, every one traced in both
            await Assert.That(Ends(on).Length).IsEqualTo(Ends(off).Length).Because($"seed {on.Seed}");
            foreach (var ((bars, ends), (_, plain)) in Ends(on).Zip(Ends(off)))
            for (var bar = 0; bar < ends.Length; bar++)
            {
                var (isHalf, isEnd) = (bars >= 4 && bar % bars == bars / 2 - 1, bar % bars == bars - 1);
                halves += isHalf ? 1 : 0;
                var at = $"bar {bar} of {string.Join(",", ends)} and {string.Join(",", plain)}, phrases of {bars}";
                await Assert.That(isHalf ? ends[bar] > 0 && plain[bar] == 0 : isEnd ? ends[bar] == plain[bar] : ends[bar] == 0 && plain[bar] == 0).IsTrue().Because(at);
            }
        }

        await Assert.That(halves).IsGreaterThan(50);

        var (withRests, without) = (HalfRests(sung), HalfRests(unsung));
        Console.WriteLine($"a rest at half a phrase: sung {withRests:P0}, not {without:P0}");
        // measured 100% and 26% at offset 0
        await Assert.That(withRests).IsGreaterThan(0.75);
        await Assert.That(withRests - without).IsGreaterThan(0.4);
    }

    /// <summary>The share of phrases of four bars or more whose half's last bar has a rest of half a beat or more.</summary>
    private static double HalfRests(IEnumerable<CorpusSong> songs)
    {
        var (rests, count) = (0, 0);
        foreach (var song in songs.Where(x => !x.Absent.Contains(TrackRole.Melody)))
        foreach (var span in song.Map.Sections.Where(s => !song.IsSolo(s) && s.Meter.PhraseBars >= 4))
        {
            var notes = song.Notes(song.Track(TrackRole.Melody)).Where(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9)
                .GroupBy(x => Math.Round(x.Position, 6)).Select(g => g.MaxBy(x => x.Value.Offset)).ToArray();
            var bar = span.Meter.BarDuration;
            for (var half = span.Start + span.Meter.PhraseDuration / 2; half < span.End - 1e-9; half += span.Meter.PhraseDuration)
            {
                var inBar = Enumerable.Range(0, notes.Length).Where(i => notes[i].Position >= half - bar - 1e-9 && notes[i].Position < half - 1e-9).ToArray();
                if (inBar.Length == 0)
                    continue;
                count++;
                rests += inBar.Any(i => (i + 1 == notes.Length ? half : notes[i + 1].Position) - (notes[i].Position + notes[i].Value.Duration) >= 0.5 - 1e-6) ? 1 : 0;
            }
        }

        return rests / (double)Math.Max(1, count);
    }
}
