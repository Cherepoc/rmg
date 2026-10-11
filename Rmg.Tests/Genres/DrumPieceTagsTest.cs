using Rmg.Core.Composition;

namespace Rmg.Tests.Genres;

/// <summary>The drum pieces tagged by the setup that draws them, so that the page offers the setups the pieces chosen make (<see cref="DrumSetups.SelectSongDrums" />).</summary>
public sealed class DrumPieceTagsTest
{
    [Test]
    public async Task EveryPiece_IsTaggedAsTheSetupsCountIt()
    {
        // a drum the percussion's setups draw, the percussion group's, or the kit's, a tambourine's among them; one with a
        // kit's drum makes the kit and the percussion where it is the percussion's
        foreach (var drum in DrumGroups.AllDrums)
        {
            var tags = GenreChoices.DrumPieces.Options.Single(x => x.Name == DrumPieces.NameOf(drum)).Tags;
            string[] expected = DrumGroups.Percussion.Drums.Contains(drum) ? ["percussion"] : ["kit"];
            await Assert.That(tags).IsEquivalentTo(expected).Because(drum.Name);
            await Assert.That(DrumPieces.SetupOf([DrumDefinitions.AcousticSnare, drum]) == DrumSetup.KitAndPercussion).IsEqualTo(tags.Contains("percussion")).Because(drum.Name);
        }

        // a tambourine a kit's, a bongo the percussion's
        await Assert.That(GenreChoices.DrumPieces.Options.Single(x => x.Name == DrumPieces.NameOf(DrumDefinitions.Tambourine)).Tags).IsEquivalentTo(["kit"]);
        await Assert.That(GenreChoices.DrumPieces.Options.Single(x => x.Name == DrumPieces.NameOf(DrumDefinitions.Bongo)).Tags).IsEquivalentTo(["percussion"]);
    }
}
