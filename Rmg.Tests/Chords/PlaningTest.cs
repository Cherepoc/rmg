using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.Chords;

/// <summary>A section planing: every chord the home's shape moved by semitones, each by a small move or a fourth, a wild song's alone.</summary>
public sealed class PlaningTest
{
    private static SongOverrides Progression(double value) => new(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Progression, value));

    [Test]
    public async Task APlanedSectionsChords_AreItsHomesShape_MovedBySmallStepsOrAFourth_NeverFarFromHome()
    {
        var planed = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, Progression(1))))
        {
            var progressions = song.Trace.Where(x => x.Point == TracePoints.Progression).Select(x => (x.Section, Roots: (ImmutableArray<int>)x.Value!)).ToArray();
            foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.Planing && x.Value is not null))
            {
                planed++;
                var shifts = (ImmutableArray<int>)entry.Value!;
                await Assert.That(shifts[0]).IsEqualTo(0);
                await Assert.That(shifts.All(x => Math.Abs(x) <= Planing.Reach)).IsTrue();
                await Assert.That(shifts.Zip(shifts.Skip(1), (a, b) => Math.Abs(b - a)).All(x => x is 1 or 2 or 3 or 5)).IsTrue();
                await Assert.That(progressions.Last(x => x.Section == entry.Section).Roots.All(x => x == 0)).IsTrue();
            }
        }

        await Assert.That(planed).IsGreaterThan(5);
    }

    [Test]
    public async Task AMiddlingSong_NeverPlanes()
    {
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, Progression(0.5))))
            await Assert.That(song.Trace.Where(x => x.Point == TracePoints.Planing).All(x => x.Value is null)).IsTrue();
    }
}
