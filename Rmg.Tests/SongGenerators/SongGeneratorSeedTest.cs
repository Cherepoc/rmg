using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;

namespace Rmg.Tests.SongGenerators;

public sealed class SongGeneratorSeedTest
{
    private static byte[] GenerateMidi(int seed)
    {
        using var stream = new MemoryStream();
        Render.RenderSong(SongGenerator.GenerateSong(seed)).Write(stream);
        return stream.ToArray();
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(12345)]
    [Arguments(-7)]
    public async Task SameSeed_ResultsIn_IdenticalSong(int seed)
    {
        await Assert.That(GenerateMidi(seed)).IsEquivalentTo(GenerateMidi(seed));
    }

    [Test]
    public async Task DifferentSeeds_ResultsIn_DifferentSongs()
    {
        await Assert.That(GenerateMidi(1).SequenceEqual(GenerateMidi(2))).IsFalse();
    }

    [Test]
    public async Task GeneratingOtherSongsInBetween_DoesNotChangeASeedsSong()
    {
        var first = GenerateMidi(5);
        GenerateMidi(6);
        GenerateMidi(7);

        await Assert.That(GenerateMidi(5)).IsEquivalentTo(first);
    }

    [Test]
    public async Task ParameterlessOverload_ResultsIn_ValidSong()
    {
        var song = SongGenerator.GenerateSong();

        await Assert.That(song.Duration).IsGreaterThan(0);
    }
}
