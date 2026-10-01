using Rmg.Core.Composition;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.SongGenerators;

public sealed class SongCancellationTest
{
    [Test]
    public async Task ASongNobodyWaitsFor_IsGivenUp()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.That(() => SongGenerator.GenerateSong(7, SongOverrides.None, cancellation.Token)).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task ARenderNobodyWaitsFor_IsGivenUp()
    {
        var song = SongGenerator.GenerateSong(7, SongOverrides.None);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.That(() => Render.RenderSong(song, SongMix.None, cancellation.Token)).Throws<OperationCanceledException>();
    }
}
