using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.Chords;

public sealed class KeyPreparationTest
{
    private static ImmutableArray<(double Change, ImmutableArray<double> Chords)> Chains(CorpusSong song) =>
        (ImmutableArray<(double, ImmutableArray<double>)>)song.Trace.Single(x => x.Point == TracePoints.KeyPreparation).Value!;

    /// <summary>The root's pitch class of the chord sounding at a place, as every part reads it.</summary>
    private static int Root(CorpusSong song, double position) =>
        Realizer.GetChord(song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(position)).Chord.GetPitch(0).Mod(12);

    /// <summary>Every chain's links, the semitones from each chord's root down to the next's: 7 a dominant's, 1 a substitute's, 11 a leading tone's, 4 a mediant's, in a major scale.</summary>
    private static int[][] Links(CorpusSong song) =>
    [
        ..Chains(song).Select(chain => chain.Chords.Append(chain.Change).ToArray())
            .Select(places => places.Zip(places.Skip(1), (chord, next) => (Root(song, chord) - Root(song, next)).Mod(12)).ToArray())
    ];

    private static SongOverrides Scale(double scale) => new(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Scale, scale).Add(Facet.Form, 0.5));

    [Test]
    public async Task EveryChordOfAChain_LeadsToTheNext_ByItsLink_OneLinkAChain()
    {
        var chains = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => Links(TestCorpus.Get(seed, Scale(1)))).SelectMany(x => x).ToArray();

        await Assert.That(chains.Length).IsGreaterThan(20);
        // one link a chain, its intervals as its section's scale has them: the leading tone's a whole step in a minor
        // scale, the subtonic, and the dominant's a diminished fifth in Locrian
        foreach (var links in chains)
            await Assert.That(links.Distinct().Count()).IsEqualTo(1).Because(string.Join(" ", links));
        await Assert.That(chains.Count(x => x[0] is 7 or 1 or 11 or 4) / (double)chains.Length).IsGreaterThan(0.8);
    }

    [Test]
    public async Task AMiddlingSongsChain_IsTheDominant_MostOften()
    {
        var links = TestCorpus.InParallel(Enumerable.Range(0, 128), seed => Links(TestCorpus.Get(seed, Scale(0.5)))).SelectMany(x => x).Select(x => x[0]).ToArray();

        await Assert.That(links.Length).IsGreaterThan(10);
        await Assert.That(links.Count(x => x == 7) / (double)links.Length).IsGreaterThan(0.6);
    }

    /// <summary>How many key changes are prepared, by fifth of the scale facet, with how long chains of which links.</summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Measure(1024, song => (Scale: ((Unconventionality)song.Trace.Single(t => t.Point == TracePoints.SongUnconventionality).Value!)[Facet.Scale],
            Changes: ((ImmutableArray<KeyChange>)song.Trace.Single(t => t.Point == TracePoints.KeyChange).Value!).Length, Links: Links(song)));
        foreach (var band in songs.GroupBy(x => Math.Clamp((int)(x.Scale * 5), 0, 4)).OrderBy(x => x.Key))
        {
            var chains = band.SelectMany(x => x.Links).ToArray();
            Console.WriteLine($"  scale {band.Key}/5: {band.Sum(x => x.Changes)} key changes, {chains.Length} prepared; chains of {string.Join(" ", chains.GroupBy(x => x.Length).OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Count()}"))}, " +
                              $"links {string.Join(" ", chains.GroupBy(x => x[0]).OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Count()}"))}");
        }

        await Task.CompletedTask;
    }
}
