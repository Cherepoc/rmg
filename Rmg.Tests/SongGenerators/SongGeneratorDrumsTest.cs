using Rmg.Core.Composition;
using Rmg.Core.Rendering;

namespace Rmg.Tests.SongGenerators;

public sealed class SongGeneratorDrumsTest
{
    [Test]
    public async Task RenderedDrums_NeverMixMainSnares_OrClapWithCrossStick()
    {
        var mainSnareCodes = new[] { DrumDefinitions.AcousticSnare, DrumDefinitions.ElectricSnare, DrumDefinitions.Clap }
            .Select(x => x.ArticulationCodes.ToHashSet())
            .ToArray();

        for (var seed = 0; seed < 60; seed++)
        {
            var song = Render.RenderSong(SongGenerator.GenerateSong(seed));
            var codes = song.Tracks
                .Where(x => x.IsPercussionInstrument)
                .SelectMany(x => x.NoteTimeline)
                .Select(x => x.Value.Offset)
                .ToHashSet();

            var usedMainSnareCount = mainSnareCodes.Count(x => codes.Overlaps(x));
            await Assert.That(usedMainSnareCount).IsLessThanOrEqualTo(1);
            await Assert.That(codes.Contains(DrumDefinitions.Clap.ArticulationCodes[0])
                && codes.Contains(DrumDefinitions.CrossStick.ArticulationCodes[0])).IsFalse();
        }
    }
}
