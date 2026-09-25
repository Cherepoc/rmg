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
            await Assert.That(ChordVoicing.Apply(context, shape)).IsEquivalentTo(shape.Targets);
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
                var voiced = ChordVoicing.Apply(context, shape);
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
            .Select(_ => string.Join(",", ChordVoicing.Apply(context, triad)))
            .Distinct()
            .Count();

        await Assert.That(layouts).IsGreaterThan(2);
    }
}
