using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.RhythmicUnconventionalities;

public sealed class SuppliedUnconventionalityTest
{
    private static byte[] Write(Song song)
    {
        using var stream = new MemoryStream();
        Render.RenderSong(song).Write(stream, null);
        return stream.ToArray();
    }

    [Test]
    [Arguments(0.0)]
    [Arguments(0.3)]
    [Arguments(1.0)]
    public async Task ASuppliedUnconventionality_IsTheSongs(double unconventionality)
    {
        await Assert.That(SongGenerator.GenerateSong(7, new SongOverrides(Base: unconventionality)).Draws!.Unconventionality.Base).IsEqualTo(unconventionality);
    }

    [Test]
    public async Task NoneSupplied_IsTheSongItsSeedMakes()
    {
        var drawn = SongGenerator.GenerateSong(7, SongOverrides.None);

        await Assert.That(Write(drawn)).IsEquivalentTo(Write(SongGenerator.GenerateSong(7)));
        await Assert.That(drawn.Draws).IsNotNull();
    }

    [Test]
    [Arguments(-0.1)]
    [Arguments(1.1)]
    [Arguments(double.NaN)]
    public async Task AnUnconventionality_OutsideZeroToOne_Fails(double unconventionality)
    {
        await Assert.That(() => SongGenerator.GenerateSong(7, new SongOverrides(Base: unconventionality))).Throws<ArgumentOutOfRangeException>();
    }
}
