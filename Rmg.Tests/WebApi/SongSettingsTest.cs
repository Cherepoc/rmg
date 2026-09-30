using Rmg.Core.Rendering;
using Rmg.WebApi.Songs;

namespace Rmg.Tests.WebApi;

public sealed class SongSettingsTest
{
    /// <summary>Settings with every field set apart from its neighbours, the ends among them.</summary>
    public static SongSettings Mixed { get; } = new(
        new Setting(true, 63),
        [..SongSettings.FacetOrder.Select((_, i) => new Setting(i % 2 == 0, i * 7))],
        new Setting(true, 3),
        new Setting(false, 11),
        new Setting(true, 2),
        [..SongSettings.PartOrder.Select((_, i) => new PartSettings(i % 3, new Setting(i % 2 == 1, i * 18), 63 - i, new Setting(i == 0, i * 9), i != 4))],
        3,
        [..SongMix.DrumGroupNames.Select((_, i) => new DrumGroupSettings(i % 2 == 0, i * 9))],
        50
    );

    /// <summary>Nothing given, everything at nothing.</summary>
    private static SongSettings Silent { get; } = new(
        new Setting(false, 0),
        [..SongSettings.FacetOrder.Select(_ => new Setting(false, 0))],
        new Setting(false, 0),
        new Setting(false, 0),
        new Setting(false, 0),
        [..SongSettings.PartOrder.Select(_ => new PartSettings(0, new Setting(false, 0), 0, new Setting(false, 0), false))],
        0,
        [..SongMix.DrumGroupNames.Select(_ => new DrumGroupSettings(false, 0))],
        0
    );

    private static bool AreSame(SongSettings first, SongSettings second) =>
        first.Unconventionality == second.Unconventionality && first.Facets.SequenceEqual(second.Facets)
        && (first.Tempo, first.Key, first.Meter) == (second.Tempo, second.Key, second.Meter) && first.Parts.SequenceEqual(second.Parts)
        && first.DrumSetup == second.DrumSetup && first.DrumGroups.SequenceEqual(second.DrumGroups) && first.Volume == second.Volume;

    [Test]
    public async Task Settings_ReadBackAsTheyWereWritten_ACharacterAValue()
    {
        foreach (var settings in new[] { Mixed, Silent })
        {
            var text = settings.Format();

            await Assert.That(text.Length).IsEqualTo(1 + SongSettings.Length);
            await Assert.That(text.All(x => char.IsAsciiLetterOrDigit(x) || x is '-' or '_')).IsTrue();
            await Assert.That(AreSame(SongSettings.Parse(text)!, settings)).IsTrue();
        }
    }

    [Test]
    public async Task EveryValue_IsACharacterAtAFixedPlace()
    {
        // the unconventionality first, then the facets, the tempo, the key, the meter and the volume
        var text = Mixed.Format();
        await Assert.That(text[1]).IsEqualTo('_');
        await Assert.That(text[1 + 1 + SongSettings.FacetOrder.Length]).IsEqualTo('3');
        await Assert.That(text[1 + 1 + SongSettings.FacetOrder.Length + 1]).IsEqualTo('B');
        await Assert.That(text[1 + 1 + SongSettings.FacetOrder.Length + 2]).IsEqualTo('2');
        await Assert.That(text[1 + 1 + SongSettings.FacetOrder.Length + 3]).IsEqualTo('o');
        await Assert.That(SongSettings.Length).IsEqualTo(63);
    }

    [Test]
    public async Task TheFormat_IsFixed()
    {
        // the page writes the same, so a change to either side shows here and in the page's check against it
        await Assert.That(Silent.Format()).IsEqualTo("2" + new string('0', SongSettings.Length));
        await Assert.That(Mixed.Format()).IsEqualTo(MixedText);
        await Assert.That(Mixed.Identity).IsEqualTo(MixedIdentity);
    }

    public const string MixedText = "2_07ELSZgnu3B2o_0-9zIyRxawjvsu_309IRajs002I0a2s183Q1i3-hQPh8f8hA";

    public const string MixedIdentity = "hQ1_0ESgu32012012013";

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
        await Assert.That((Silent with { Unconventionality = new Setting(false, 50) }).Identity).IsEqualTo("");
        await Assert.That((Silent with { Unconventionality = new Setting(true, 50) }).Identity).IsNotEqualTo("");
        await Assert.That((Silent with { Key = new Setting(true, 0) }).Identity).IsNotEqualTo("");
        await Assert.That((Silent with { DrumSetup = 2 }).Identity).IsNotEqualTo("");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("1")]
    [Arguments("20")]
    [Arguments("2.000000000000000000000000000000000000000000000000000000000000000")]
    public async Task AnythingElse_ReadsAsNone(string? text)
    {
        await Assert.That(SongSettings.Parse(text)).IsNull();
    }

    [Test]
    public async Task APartThatPlaysNeitherItsOwnNorInNorOut_OrAKeyPastB_ReadsAsNone()
    {
        await Assert.That(SongSettings.Parse((Mixed with { Parts = [..Mixed.Parts.Select(x => x with { Plays = 3 })] }).Format())).IsNull();
        await Assert.That(SongSettings.Parse((Mixed with { Key = new Setting(true, 12) }).Format())).IsNull();
    }
}
