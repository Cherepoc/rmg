using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.Melodies;

public sealed class HarmonyIntervalTest
{
    [Test]
    public async Task AGenreOfFourthsAndFifths_HarmonisesTheMelodyInThemAlone()
    {
        var genre = new Genre(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty
            .Add("harmonyInterval", ImmutableDictionary<string, ByConvention>.Empty.Add("fourths", new(1, 1, 1)))
            .Add("doubling", ImmutableDictionary<string, ByConvention>.Empty.Add("harmony", new(1, 1, 1))));
        var steps = TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre)))
            .SelectMany(song => song.Trace.Where(x => x.Point == TracePoints.LineDoubling).Select(x => ((int, TrackRole Source, TrackRole, int Step))x.Value!))
            .Where(x => x.Source == TrackRole.Melody).Select(x => x.Step).ToArray();

        await Assert.That(steps.Length).IsGreaterThan(0);
        await Assert.That(steps.All(x => x is 3 or -3 or 4 or -4)).IsTrue();
    }
}
