using Rmg.Core.Composition;
using Rmg.Core.Rendering;
using Rmg.WebApi.Songs;

namespace Rmg.Tests.WebApi;

public sealed class SongSettingsTest
{
    /// <summary>
    ///     Settings with every field set apart from its neighbours, the ends among them: the last step, the last program and
    ///     the last drum piece, whose bit is the last character's.
    /// </summary>
    public static SongSettings Mixed { get; } = new(
        new Setting(true, 61),
        [..SongSettings.FacetOrder.Select((_, i) => new Setting(i % 2 == 0, i * 7))],
        new Setting(true, 3),
        new Setting(false, 11),
        new Setting(true, 2),
        new Setting(true, 12),
        new Setting(false, 45),
        new Setting(true, 20),
        [
            ..SongSettings.PartOrder.SkipLast(1).Select((_, i) => new PartSettings(i % 3, new Setting(i % 2 == 1, i == 7 ? 127 : i * 18), 61 - i, new Setting(i == 0, i * 8))),
            new PartSettings(1, new Setting(true, 65), 40, new Setting(true, 50))
        ],
        [..SongMix.DrumGroupNames.Select((_, i) => new DrumGroupSettings(i * 9))],
        50,
        new DrumPieceSettings(true, [..DrumPieces.All.Select((_, i) => i % 3 == 0 || i == DrumPieces.All.Length - 1)])
    );

    /// <summary>Nothing given, everything at nothing.</summary>
    private static SongSettings Silent { get; } = new(
        new Setting(false, 0),
        [..SongSettings.FacetOrder.Select(_ => new Setting(false, 0))],
        new Setting(false, 0),
        new Setting(false, 0),
        new Setting(false, 0),
        new Setting(false, 0),
        new Setting(false, 0),
        new Setting(false, 0),
        [..SongSettings.PartOrder.Select(_ => new PartSettings(0, new Setting(false, 0), 0, new Setting(false, 0)))],
        [..SongMix.DrumGroupNames.Select(_ => new DrumGroupSettings(0))],
        0,
        DrumPieceSettings.Drawn
    );

    private static bool AreSame(SongSettings first, SongSettings second) =>
        first.Unconventionality == second.Unconventionality && first.Facets.SequenceEqual(second.Facets)
        && (first.Tempo, first.Key, first.Meter, first.Mode, first.Swing, first.Length) == (second.Tempo, second.Key, second.Meter, second.Mode, second.Swing, second.Length) && first.Parts.SequenceEqual(second.Parts)
        && first.DrumGroups.SequenceEqual(second.DrumGroups) && first.Volume == second.Volume
        && first.DrumPieces.IsGiven == second.DrumPieces.IsGiven && first.DrumPieces.Pieces.SequenceEqual(second.DrumPieces.Pieces);

    /// <summary>The settings name every facet and every part a song may have but the riff's twin, so that a link and an event carry each.</summary>
    [Test]
    public async Task TheSettings_NameEveryFacet_AndEveryPart()
    {
        await Assert.That(SongSettings.FacetOrder.ToHashSet().SetEquals(Enum.GetValues<Facet>())).IsTrue();
        // but the riff's twin, mixed as its riff is
        await Assert.That(SongSettings.PartOrder.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(SongParts.All.Where(x => x != Rmg.Core.Songs.TrackRole.RiffTwin).Select(x => x.ToString()))).IsTrue();
    }

    [Test]
    public async Task Settings_ReadBackAsTheyWereWritten_ACharacterAValue()
    {
        foreach (var settings in new[] { Mixed, Silent })
        {
            var text = settings.Format();

            await Assert.That(text.Length).IsEqualTo(1 + SongSettings.FixedLength);
            // letters and digits alone, which a link carries and a chat's markdown leaves as they are
            await Assert.That(text.All(char.IsAsciiLetterOrDigit)).IsTrue();
            await Assert.That(AreSame(SongSettings.Parse(text)!, settings)).IsTrue();
        }
    }

    [Test]
    public async Task EveryValueOf62Steps_IsACharacterAtAFixedPlace_AndTheRestBitsFiveToACharacter()
    {
        // the unconventionality first, then the facets, the tempo, the swing, the length and the volume, every part's
        // volume and pan, and every drum group's volume, 39 characters; then the rest's 151 bits, 31 characters
        var text = Mixed.Format();
        await Assert.That(text[1]).IsEqualTo('z');
        await Assert.That(text[1 + 1 + SongSettings.FacetOrder.Length]).IsEqualTo('3');
        await Assert.That(text[1 + 1 + SongSettings.FacetOrder.Length + 1]).IsEqualTo('j');
        await Assert.That(text[1 + 1 + SongSettings.FacetOrder.Length + 2]).IsEqualTo('K');
        await Assert.That(text[1 + 1 + SongSettings.FacetOrder.Length + 3]).IsEqualTo('o');
        await Assert.That(SongSettings.FixedLength).IsEqualTo(39 + 31);
        // every meter and every scale fits its bits
        await Assert.That(Meter.Options.Length).IsLessThanOrEqualTo(1 << SongSettings.MeterBits);
        await Assert.That(Rmg.Core.Composition.Scales.All.Length).IsLessThanOrEqualTo(1 << SongSettings.ModeBits);
    }

    [Test]
    public async Task TheFormat_IsFixed()
    {
        // the page writes the same, so a change to either side shows here and in the page's check against it
        await Assert.That(Silent.Format()).IsEqualTo("B" + new string('0', SongSettings.FixedLength));
        await Assert.That(Mixed.Format()).IsEqualTo(MixedText);
        await Assert.That(Mixed.Identity).IsEqualTo(MixedIdentity);
    }

    public const string MixedText = "Bz07ELSZgnu3jKoz0y8xGwOvWuetmsueo09IRajsBLDN261GK42IGM5GKAB0RPVR1EI49I1";

    public const string MixedIdentity = "BLD1z0ESgu32CK01201201119I496";

    [Test]
    public async Task ATempoStep_RunsFrom60Through120To240BeatsAMinute_EveryStepFasterByAboutTheSame()
    {
        await Assert.That(TempoScale.Multiplier(0) * Meter.BaseTempo).IsEqualTo(60).Within(1e-9);
        await Assert.That(TempoScale.Multiplier(TempoScale.Middle) * Meter.BaseTempo).IsEqualTo(120).Within(1e-9);
        await Assert.That(TempoScale.Multiplier(TempoScale.Steps - 1) * Meter.BaseTempo).IsEqualTo(240).Within(1e-9);
        var ratios = Enumerable.Range(1, TempoScale.Steps - 1).Select(x => TempoScale.Multiplier(x) / TempoScale.Multiplier(x - 1)).ToArray();
        await Assert.That(ratios.Min()).IsGreaterThan(1.02);
        await Assert.That(ratios.Max()).IsLessThan(1.025);
        // and every step its own nearest
        foreach (var step in Enumerable.Range(0, TempoScale.Steps))
            await Assert.That(TempoScale.StepOf(TempoScale.Multiplier(step))).IsEqualTo(step);
    }

    [Test]
    public async Task SettingsOfAnotherFormat_ReadAsNone()
    {
        await Assert.That(SongSettings.Parse("9" + MixedText[1..])).IsNull();
        // and anything behind the fixed places
        await Assert.That(SongSettings.Parse(MixedText + "A")).IsNull();
    }

    [Test]
    public async Task SettingsWrittenAnotherWay_ReadAsNone_SoThatASongHasOneIdentity()
    {
        // a character of bits past V, five bits' most, and the last character's padding, past its one bit, set
        var bits = 1 + 39;
        await Assert.That(SongSettings.Parse(MixedText[..bits] + "W" + MixedText[(bits + 1)..])).IsNull();
        await Assert.That(SongSettings.Parse(MixedText[..^1] + "3")).IsNull();
        // a value past the last step
        await Assert.That(SongSettings.Parse(MixedText[..1] + "z" + MixedText[2..])).IsNotNull();
        await Assert.That(SongSettings.Parse(MixedText[..1] + "_" + MixedText[2..])).IsNull();
    }

    [Test]
    public async Task Settings_AskForTheirSong_AsThePageDoes_EveryValueGivenAsGiven_AndTheRestTheSongsOwn()
    {
        var request = Mixed.ToRequest("g");

        await Assert.That(request.Seed).IsEqualTo("g");
        await Assert.That(request.Unconventionality).IsEqualTo(Mixed.Unconventionality.IsGiven ? Mixed.Unconventionality.Value : null);
        await Assert.That(request.Facets!.Count).IsEqualTo(Mixed.Facets.Count(x => x.IsGiven));
        foreach (var (facet, setting) in SongSettings.FacetOrder.Zip(Mixed.Facets).Where(x => x.Second.IsGiven))
            await Assert.That(request.Facets[facet.ToString()]).IsEqualTo(setting.Value);
        foreach (var (name, part) in SongSettings.PartOrder.Zip(Mixed.Parts))
        {
            var asked = request.Parts![name];
            await Assert.That(asked.Plays).IsEqualTo(part.Plays switch { 1 => true, 2 => false, _ => (bool?)null });
            await Assert.That(asked.Instrument).IsEqualTo(part.Instrument.IsGiven ? part.Instrument.Value : null);
            await Assert.That(asked.Pan).IsEqualTo(part.Pan.IsGiven ? part.Pan.Value : null);
            await Assert.That(asked.Volume).IsEqualTo(part.Volume);
        }

        await Assert.That(request.DrumPieces!.Count).IsEqualTo(Mixed.DrumPieces.Pieces.Count(x => x));
        await Assert.That(request.Length).IsEqualTo(20);
        await Assert.That(request.Genre).IsNull();
        // and read as any request is
        await Assert.That(request.TryRead(out _, out _, out _, out var error)).IsTrue().Because(error ?? "");
    }

    [Test]
    public async Task OnlyWhatIsGivenAndChangesTheSong_NamesIt()
    {
        await Assert.That(Silent.Identity).IsEqualTo("");
        // a part's instrument, volume and pan, a drum group's and the volume leave its notes as they are
        var heard = Silent with
        {
            Parts = [..Silent.Parts.Select(x => x with { Instrument = new Setting(true, 5), Pan = new Setting(true, 5), Volume = 5 })],
            DrumGroups = [..Silent.DrumGroups.Select(_ => new DrumGroupSettings(5))],
            Volume = 5
        };
        await Assert.That(heard.Identity).IsEqualTo("");
        // a value drawn is not given
        await Assert.That((Silent with { Unconventionality = new Setting(false, 50) }).Identity).IsEqualTo("");
        await Assert.That((Silent with { Unconventionality = new Setting(true, 50) }).Identity).IsNotEqualTo("");
        await Assert.That((Silent with { Key = new Setting(true, 0) }).Identity).IsNotEqualTo("");
        await Assert.That((Silent with { DrumPieces = new DrumPieceSettings(true, [..DrumPieces.All.Select((_, i) => i == 0)]) }).Identity).IsNotEqualTo("");
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
