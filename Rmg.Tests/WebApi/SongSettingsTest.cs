using System.Collections.Immutable;
using Rmg.Core.Rendering;
using Rmg.WebApi.Songs;

namespace Rmg.Tests.WebApi;

public sealed class SongSettingsTest
{
    /// <summary>Settings with every field set apart from its neighbours, the ends among them.</summary>
    public static SongSettings Mixed { get; } = new(
        new Setting(true, 64),
        [..SongSettings.FacetOrder.Select((_, i) => new Setting(i % 2 == 0, i * 16))],
        [..SongSettings.PartOrder.Select((_, i) => new PartSettings(i % 3, new Setting(i % 2 == 1, i * 20), 127 - i, new Setting(i == 0, i * 21), i != 4))],
        3,
        [..SongMix.DrumGroupNames.Select((_, i) => new DrumGroupSettings(i % 2 == 0, i * 18))],
        100
    );

    /// <summary>Nothing given, everything at nothing.</summary>
    private static SongSettings Silent { get; } = new(
        new Setting(false, 0),
        [..SongSettings.FacetOrder.Select(_ => new Setting(false, 0))],
        [..SongSettings.PartOrder.Select(_ => new PartSettings(0, new Setting(false, 0), 0, new Setting(false, 0), false))],
        0,
        [..SongMix.DrumGroupNames.Select(_ => new DrumGroupSettings(false, 0))],
        0
    );

    private static bool AreSame(SongSettings first, SongSettings second) =>
        first.Unconventionality == second.Unconventionality && first.Facets.SequenceEqual(second.Facets) && first.Parts.SequenceEqual(second.Parts)
        && first.DrumSetup == second.DrumSetup && first.DrumGroups.SequenceEqual(second.DrumGroups) && first.Volume == second.Volume;

    [Test]
    public async Task Settings_ReadBackAsTheyWereWritten()
    {
        foreach (var settings in new[] { Mixed, Silent })
        {
            var text = settings.Format();

            await Assert.That(text.Length).IsEqualTo(1 + SongSettings.Length);
            await Assert.That(text.All(char.IsAsciiLetterOrDigit)).IsTrue();
            await Assert.That(AreSame(SongSettings.Parse(text)!, settings)).IsTrue();
        }
    }

    [Test]
    public async Task TheFormat_IsFixed()
    {
        // the page writes the same, so a change to either side shows here and in the page's check against it
        await Assert.That(Silent.Format()).IsEqualTo("1" + new string('0', SongSettings.Length));
        await Assert.That(Mixed.Format()).IsEqualTo(MixedText);
        await Assert.That(Mixed.Identity).IsEqualTo(MixedIdentity);
    }

    public const string MixedText = "11mS6kycFlNX3dTaMeKDJIINcHQ1WnJyTO0Q20Kbo6sPS9HN7ZM";

    public const string MixedIdentity = "4gr5Yq9WJsBG0SZ";

    [Test]
    public async Task OnlyWhatIsGivenAndChangesTheSong_NamesIt()
    {
        await Assert.That(Silent.Identity).IsEqualTo("");
        // a part's instrument, volume and pan, a drum group's and the volume leave its notes as they are
        var heard = Silent with
        {
            Parts = [..Silent.Parts.Select(x => x with { Instrument = new Setting(true, 5), Pan = new Setting(true, 5), Volume = 5 })],
            DrumGroups = [..Silent.DrumGroups.Select(_ => new DrumGroupSettings(true, 5))],
            Volume = 5
        };
        await Assert.That(heard.Identity).IsEqualTo("");
        // a value drawn is not given
        await Assert.That((Silent with { Unconventionality = new Setting(false, 90) }).Identity).IsEqualTo("");
        await Assert.That((Silent with { Unconventionality = new Setting(true, 90) }).Identity).IsNotEqualTo("");
        await Assert.That((Silent with { DrumSetup = 2 }).Identity).IsNotEqualTo("");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("2")]
    [Arguments("10")]
    [Arguments("1-")]
    [Arguments("1zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public async Task AnythingElse_ReadsAsNone(string? text)
    {
        await Assert.That(SongSettings.Parse(text)).IsNull();
    }

    [Test]
    public async Task APartThatPlaysNeitherItsOwnNorInNorOut_ReadsAsNone()
    {
        var text = (Mixed with { Parts = [..Mixed.Parts.Select(x => x with { Plays = 3 })] }).Format();

        await Assert.That(SongSettings.Parse(text)).IsNull();
    }
}
