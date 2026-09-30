using System.Collections.Immutable;
using Rmg.WebApi.Songs;

namespace Rmg.Tests.WebApi;

public sealed class SongSettingsTest
{
    /// <summary>Settings with every field set apart from its neighbours, the ends among them.</summary>
    public static SongSettings Mixed { get; } = new(
        64,
        true,
        100,
        Enumerable.Range(0, SongSettings.ChannelCount).Select(x => new ChannelSettings(x * 8, x % 3 != 0, 127 - x)).ToImmutableArray()
    );

    private static SongSettings Silent { get; } = new(0, false, 0, Enumerable.Repeat(new ChannelSettings(0, false, 0), SongSettings.ChannelCount).ToImmutableArray());

    private static SongSettings Loudest { get; } = new(127, true, 127, Enumerable.Repeat(new ChannelSettings(127, true, 127), SongSettings.ChannelCount).ToImmutableArray());

    [Test]
    public async Task Settings_ReadBackAsTheyWereWritten()
    {
        foreach (var settings in new[] { Mixed, Silent, Loudest })
        {
            var text = settings.Format();
            var read = SongSettings.Parse(text)!;

            await Assert.That(text.Length).IsEqualTo(1 + SongSettings.Length);
            await Assert.That(text.All(char.IsAsciiLetterOrDigit)).IsTrue();
            await Assert.That((read.Unconventionality, read.IsGiven, read.SongVolume)).IsEqualTo((settings.Unconventionality, settings.IsGiven, settings.SongVolume));
            await Assert.That(read.Channels.SequenceEqual(settings.Channels)).IsTrue();
        }
    }

    [Test]
    public async Task TheFormat_IsFixed()
    {
        // the page writes the same, so a change to either side shows here and in the page's check against it
        await Assert.That(Silent.Format()).IsEqualTo("1" + new string('0', SongSettings.Length));
        await Assert.That(Mixed.Format()).IsEqualTo(MixedText);
    }

    public const string MixedText = "1Um3dGubkFdqGtmxVKNfXg62ZN8Qk2VKbenSE6EpO66a";

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("2000000000000000000000000000000000000000000")]
    [Arguments("100000000000000000000000000000000000000000")]
    [Arguments("1000000000000000000000000000000000000000000-")]
    [Arguments("1zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    [Arguments("1000000000000000000000000000000000000000001")]
    public async Task AnythingElse_ReadsAsNone(string? text)
    {
        await Assert.That(SongSettings.Parse(text)).IsNull();
    }
}
