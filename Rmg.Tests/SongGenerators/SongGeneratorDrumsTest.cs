using Rmg.Core.Composition;
using Rmg.Core.Rendering;

namespace Rmg.Tests.SongGenerators;

public sealed class SongGeneratorDrumsTest
{
    [Test]
    public async Task RenderedDrums_NeverMixTheSnares()
    {
        // the cross-stick is a stroke of either snare, so a snare is told by its own sound
        var mainSnareCodes = new[] { DrumDefinitions.AcousticSnare, DrumDefinitions.ElectricSnare }
            .Select(x => x.ArticulationCodes.Except([37]).ToHashSet())
            .ToArray();

        for (var seed = 0; seed < 64; seed++)
        {
            var song = TestCorpus.Get(seed).Rendered;
            var codes = song.Tracks
                .Where(x => x.IsPercussionInstrument)
                .SelectMany(x => x.NoteTimeline)
                .Select(x => x.Value.Offset)
                .ToHashSet();

            var usedMainSnareCount = mainSnareCodes.Count(x => codes.Overlaps(x));
            await Assert.That(usedMainSnareCount).IsLessThanOrEqualTo(1);
        }
    }
}
