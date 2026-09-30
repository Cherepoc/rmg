using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.Chords;

public sealed class ChordVoicingTest
{
    [Test]
    public async Task FixedShape_KeepsItsLayout()
    {
        var shape = ChordShapes.All.First(x => x.Name == "Polychord");
        var context = new GenerationContext(0);

        for (var i = 0; i < 100; i++)
            await Assert.That(ChordVoicing.Apply(context, shape, 0.5)).IsEquivalentTo(shape.Targets);
    }

    [Test]
    public async Task Voicing_OnlyMovesNotesByOctaves()
    {
        var context = new GenerationContext(0);
        foreach (var shape in ChordShapes.All)
        {
            var pitchClasses = shape.Targets.Select(x => Math.Round(x.Mod(12), 6)).Order().ToArray();
            for (var i = 0; i < 50; i++)
            {
                var voiced = ChordVoicing.Apply(context, shape, 0.5);
                await Assert.That(voiced.Select(x => Math.Round(x.Mod(12), 6)).Order().ToArray())
                    .IsEquivalentTo(pitchClasses)
                    .Because(shape.Name);
            }
        }
    }

    [Test]
    public async Task Triads_AreSometimesInverted()
    {
        var triad = ChordShapes.All.First(x => x.Name == "Triad");
        var context = new GenerationContext(0);

        var layouts = Enumerable.Range(0, 1000)
            .Select(_ => string.Join(",", ChordVoicing.Apply(context, triad, 0.5)))
            .Distinct()
            .Count();

        await Assert.That(layouts).IsGreaterThan(2);
    }

    [Test]
    public async Task ThePlainestTriads_AreCloseOrInverted_AndTheWildestNeverClose()
    {
        var triad = ChordShapes.All.First(x => x.Name == "Triad");
        var context = new GenerationContext(0);
        ImmutableArray<double>[] inversions =
        [
            triad.Targets,
            [..triad.Targets.Skip(1), triad.Targets[0] + 12],
            [..triad.Targets.Skip(2), triad.Targets[0] + 12, triad.Targets[1] + 12]
        ];

        for (var i = 0; i < 500; i++)
        {
            var plain = ChordVoicing.Apply(context, triad, 0);
            var wild = ChordVoicing.Apply(context, triad, 1);

            await Assert.That(inversions.Any(x => x.Order().SequenceEqual(plain))).IsTrue();
            await Assert.That(wild.SequenceEqual(triad.Targets.Order())).IsFalse();
        }
    }
}
