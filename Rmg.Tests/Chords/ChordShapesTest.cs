using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.Chords;

public sealed class ChordShapesTest
{
    [Test]
    public async Task EveryUnconventionality_HasShapes()
    {
        var levels = ChordShapes.All.Select(x => x.Unconventionality).Distinct().Order().ToArray();

        await Assert.That(levels).IsEquivalentTo(Enumerable.Range(0, ChordShapes.MaxUnconventionality + 1).ToArray(), CollectionOrdering.Matching);
    }

    [Test]
    public async Task EveryShape_StartsAtItsRoot_AndRises()
    {
        foreach (var shape in ChordShapes.All)
        {
            await Assert.That(shape.Targets[0]).IsEqualTo(0).Because(shape.Name);
            await Assert.That(shape.Targets.Zip(shape.Targets.Skip(1)).All(x => x.First < x.Second))
                .IsTrue()
                .Because(shape.Name);
            await Assert.That(shape.Weight).IsGreaterThan(0).Because(shape.Name);
        }
    }

    [Test]
    public async Task Pick_ResultsIn_ShapesOfTheUnconventionality_TheHeavierMoreOften()
    {
        var context = new GenerationContext(0);
        var picks = Enumerable.Range(0, 10000).Select(_ => ChordShapes.Pick(context, 1, default)).ToArray();

        await Assert.That(picks.All(x => x.Unconventionality == 1)).IsTrue();
        // the seventh weighs 1 and the sixth 0.5
        await Assert.That(picks.Count(x => x.Name == "Seventh")).IsGreaterThan(picks.Count(x => x.Name == "Sixth") * 3 / 2);
    }

    /// <summary>
    ///     A level's shapes leaned by the song's mood where no genre's pool weighs them: a bright mood's add9, sixth and sus2
    ///     oftener than a dark one's, a dark mood's diminished and Phrygian chords oftener than a bright one's.
    /// </summary>
    [Test]
    public async Task AMoodsDarkness_LeansALevelsShapes()
    {
        ChordShape[] Picks(int level, double darkness)
        {
            var context = new GenerationContext(0);
            return [..Enumerable.Range(0, 10000).Select(_ => ChordShapes.Pick(context, level, new Mood(darkness, 0)))];
        }

        double Bright(ChordShape[] picks) => picks.Count(x => x.Darkness < 0) / (double)picks.Length;
        double Dark(ChordShape[] picks) => picks.Count(x => x.Darkness > 0) / (double)picks.Length;
        Console.WriteLine($"level 1, bright shapes: {Bright(Picks(1, -1)):P0} bright, {Bright(Picks(1, 0)):P0} middling, {Bright(Picks(1, 1)):P0} dark; level 4, dark shapes: {Dark(Picks(4, -1)):P0}, {Dark(Picks(4, 0)):P0}, {Dark(Picks(4, 1)):P0}");
        await Assert.That(Bright(Picks(1, -1))).IsGreaterThan(Bright(Picks(1, 0)));
        await Assert.That(Bright(Picks(1, 1))).IsLessThan(Bright(Picks(1, 0)));
        await Assert.That(Dark(Picks(4, 1))).IsGreaterThan(Dark(Picks(4, -1)));
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        // the shapes the chords play, a pick of every bar's chord, by level
        var shapes = TestCorpus.Measure(256, song => song.Trace
                .Where(x => x.Point == TracePoints.Chord && x.Track == song.Track(TrackRole.Chords))
                .Select(x => PatternGenerator.ChordOf(x.StateMap))
                .Select(x => x.Shape)
                .ToArray())
            .SelectMany(x => x)
            .ToArray();
        foreach (var level in shapes.GroupBy(x => x.Unconventionality).OrderBy(x => x.Key))
            Console.WriteLine($"  level {level.Key}, {level.Count() / (double)shapes.Length:P1}: {string.Join(", ", level.GroupBy(x => x.Name).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count() / (double)level.Count():P0}"))}");
        await Task.CompletedTask;
    }
}
