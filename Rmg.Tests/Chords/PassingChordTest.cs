using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Chords;

/// <summary>A passing chord: the next chord's dominant over the second half of a chord a bar long, never in a plain song.</summary>
public sealed class PassingChordTest
{
    /// <summary>Every section's chords as it records them: where they start, their roots, and which of them pass.</summary>
    private static IEnumerable<(HarmonicRhythm Rhythm, ImmutableArray<int> Progression)> Sections(CorpusSong song) =>
        song.Trace.Where(x => x.Point == TracePoints.HarmonicRhythm).Select(x => (HarmonicRhythm)x.Value!)
            .Zip(song.Trace.Where(x => x.Point == TracePoints.Progression).Select(x => (ImmutableArray<int>)x.Value!));

    [Test]
    public async Task APassingChord_IsTheNextsDominant_HalfwayThroughAChordABarLong()
    {
        var passing = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("jazz")))))
        foreach (var (rhythm, progression) in Sections(song))
        {
            await Assert.That(progression.Length).IsEqualTo(rhythm.Count);
            foreach (var at in rhythm.Passing)
            {
                passing++;
                var chord = rhythm.Changes.IndexOf(at);
                await Assert.That(((progression[chord] - progression[chord + 1] - 4) % 7 + 7) % 7).IsEqualTo(0);
                await Assert.That(rhythm.LengthOf(chord - 1) + rhythm.LengthOf(chord)).IsGreaterThanOrEqualTo(rhythm.Meter.BarDuration - 1e-9);
            }
        }

        await Assert.That(passing).IsGreaterThan(10);
    }

    [Test]
    public async Task ThePlainestSongs_PassNoChord()
    {
        var plain = new SongOverrides(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Progression, 0), Genre: GenrePresets.Genre("jazz"));
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, plain)))
            await Assert.That(Sections(song).All(x => x.Rhythm.Passing.IsEmpty)).IsTrue();
    }
}
