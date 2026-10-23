using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Chords;

/// <summary>A chromatic mediant (<see cref="ChordScales.Mediant" />): a minor chord a third from the home, iii or vi, turned major.</summary>
public sealed class MediantTest
{
    /// <summary>The share of a song's chords on iii or vi turned major, and how many there were, its sections' progressions and chord scales read from the trace.</summary>
    private static async Task<(int ThirdsAway, int Mediants)> Count(CorpusSong song)
    {
        var (thirds, mediants) = (0, 0);
        foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.ChordScales))
        {
            var scales = (ImmutableArray<ChordScale>)entry.Value!;
            var roots = (ImmutableArray<int>)song.Trace.Last(x => x.Point == TracePoints.Progression && x.Section == entry.Section).Value!;
            foreach (var (root, scale) in roots.Zip(scales))
            {
                var awayByAThird = ((root % 7) + 7) % 7 is 2 or 5;
                thirds += awayByAThird ? 1 : 0;
                if (scale.Kind != ChordScaleKind.Mediant)
                    continue;
                mediants++;
                // on iii or vi alone, a step raised, its third
                await Assert.That(awayByAThird).IsTrue().Because($"seed {song.Seed} section {entry.Section}");
                await Assert.That(scale.Steps.Raised).IsNotEqualTo(0).Because($"seed {song.Seed} section {entry.Section}");
            }
        }

        return (thirds, mediants);
    }

    /// <summary>
    ///     A rule, and a calibration: the songs' iii and vi turned major now and then at the middle (4.0-5.1% of 128 songs
    ///     at offsets 0, a million and 958 million, where applied dominants and borrowed chords come first and a minor
    ///     home's are major already), Orchestral's 13-15%, the plainest songs' never.
    /// </summary>
    [Test]
    public async Task AMediant_IsIiiOrViTurnedMajor_NowAndThenInMiddlingSongs_NeverInThePlainest()
    {
        foreach (var (name, songs) in new[]
                 {
                     ("middling songs", TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, new SongOverrides(Base: 0.5)))),
                     ("orchestral", TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("orchestral"))))),
                     ("the plainest songs", TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Base: 0))))
                 })
        {
            var (thirds, mediants) = (0, 0);
            foreach (var song in songs)
            {
                var (t, m) = await Count(song);
                (thirds, mediants) = (thirds + t, mediants + m);
            }

            Console.WriteLine($"{name}: {mediants} of {thirds} chords on iii or vi turned major, {mediants / (double)thirds:P1}");
            if (name == "the plainest songs")
                await Assert.That(mediants).IsEqualTo(0);
            else
                await Assert.That(mediants / (double)thirds).IsBetween(0.02, 0.4);
        }
    }

    /// <summary>
    ///     A minor mediant (<see cref="ChordScales.MinorMediant" />): Black metal's ♭III turned minor (its ♭VI would lower the key's tonic), its third lowered,
    ///     on a third from the home alone; the songs' never, as they give it no chance.
    /// </summary>
    [Test]
    public async Task AMinorMediant_IsAThirdFromHomeTurnedMinor_BlackMetals()
    {
        var minors = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("blackMetal")))))
        foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.ChordScales))
        {
            var roots = (ImmutableArray<int>)song.Trace.Last(x => x.Point == TracePoints.Progression && x.Section == entry.Section).Value!;
            foreach (var (root, scale) in roots.Zip((ImmutableArray<ChordScale>)entry.Value!).Where(x => x.Second.Kind == ChordScaleKind.MinorMediant))
            {
                minors++;
                await Assert.That(((root % 7) + 7) % 7 is 2 or 5).IsTrue().Because($"seed {song.Seed} section {entry.Section}");
                await Assert.That(scale.Steps.Lowered).IsNotEqualTo(0).Because($"seed {song.Seed} section {entry.Section}");
            }
        }

        var own = TestCorpus.Range(64).SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.ChordScales)).SelectMany(x => (ImmutableArray<ChordScale>)x.Value!);
        Console.WriteLine($"{minors} minor mediants in 128 Black metal songs");
        // measured 9-14 in 128 songs at 20-30-40 over five offsets of the corpus (16 in 64 at 0, 7 at 10-20-30), nearly all on
        // ♭III, as a minor key's ♭VI would lower its tonic
        await Assert.That(minors).IsGreaterThan(4);
        await Assert.That(own.Any(x => x.Kind == ChordScaleKind.MinorMediant)).IsFalse();
    }
}
