using Rmg.Core.Composition;
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
        [
            ..SongSettings.PartOrder.SkipLast(1).Select((_, i) => new PartSettings(i % 3, new Setting(i % 2 == 1, i * 18), 63 - i, new Setting(i == 0, i * 9))),
            new PartSettings(1, new Setting(true, 65), 40, new Setting(true, 50))
        ],
        [..SongMix.DrumGroupNames.Select((_, i) => new DrumGroupSettings(i * 9))],
        50,
        new DrumPieceSettings(true, [..DrumPieces.All.Select((_, i) => i % 3 == 0)])
    );

    /// <summary>Nothing given, everything at nothing.</summary>
    private static SongSettings Silent { get; } = new(
        new Setting(false, 0),
        [..SongSettings.FacetOrder.Select(_ => new Setting(false, 0))],
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
        && (first.Tempo, first.Key, first.Meter) == (second.Tempo, second.Key, second.Meter) && first.Parts.SequenceEqual(second.Parts)
        && first.DrumGroups.SequenceEqual(second.DrumGroups) && first.Volume == second.Volume
        && first.DrumPieces.IsGiven == second.DrumPieces.IsGiven && first.DrumPieces.Pieces.SequenceEqual(second.DrumPieces.Pieces);

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
    public async Task EveryValueOf64Steps_IsACharacterAtAFixedPlace_AndTheRestBitsSixToACharacter()
    {
        // the unconventionality first, then the facets, the tempo and the volume, every part's volume and pan, and every
        // drum group's volume, 37 characters; then the rest's 144 bits, 24 characters
        var text = Mixed.Format();
        await Assert.That(text[1]).IsEqualTo('_');
        await Assert.That(text[1 + 1 + SongSettings.FacetOrder.Length]).IsEqualTo('3');
        await Assert.That(text[1 + 1 + SongSettings.FacetOrder.Length + 1]).IsEqualTo('o');
        await Assert.That(SongSettings.Length).IsEqualTo(37 + 24);
        // every meter fits its bits
        await Assert.That(Meter.Options.Length).IsLessThanOrEqualTo(1 << SongSettings.MeterBits);
    }

    [Test]
    public async Task TheFormat_IsFixed()
    {
        // the page writes the same, so a change to either side shows here and in the page's check against it
        await Assert.That(Silent.Format()).IsEqualTo("6" + new string('0', SongSettings.Length));
        await Assert.That(Mixed.Format()).IsEqualTo(MixedText);
        await Assert.That(Mixed.Identity).IsEqualTo(MixedIdentity);
    }

    public const string MixedText = "6_07ELSZgnu3o_0-9zIyRxawjvsu_eo09IRajshQNX0aI4Iej0fg5Od-xWJIII";

    /// <summary>The mixed settings as the format before wrote them, every value a character.</summary>
    public const string MixedTextFive = "5_07ELSZgnu3B2o_0-9zIyRxawjvsu_eo309IRajs002I0a2s183Q1i3-31hQPh8f8phE9991";

    /// <summary>The mixed settings as the format before that wrote them, its drum pieces the song's own.</summary>
    public const string MixedTextFour = "4_07ELSZgnu3B2o_0-9zIyRxawjvsu_eo309IRajs002I0a2s183Q1i3-31hQPh8f8ph2";

    /// <summary>The mixed settings as the format before wrote them, its tempo by its place among those the song draws.</summary>
    public const string MixedTextThree = "3_07ELSZgnu3B2o_0-9zIyRxawjvsu_eo309IRajs002I0a2s183Q1i3-31hQPh8f8ph2";

    /// <summary>The mixed settings as the format before that wrote them, its first eight parts.</summary>
    public const string MixedTextTwo = "2_07ELSZgnu3B2o_0-9zIyRxawjvsu_309IRajs002I0a2s183Q1i3-hQPh8f8hA";

    public const string MixedIdentity = "hQ1_0ESgu32012012010119999";

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
    public async Task TheFormatBefore_ReadsAsItWas()
    {
        await Assert.That(AreSame(SongSettings.Parse(MixedTextFive)!, Mixed)).IsTrue();
    }

    [Test]
    public async Task TheFormatBeforeThat_ReadsAsItWas_ItsDrumPiecesTheSongsOwn()
    {
        var read = SongSettings.Parse(MixedTextFour)!;

        await Assert.That(AreSame(read, Mixed with { DrumPieces = DrumPieceSettings.Drawn })).IsTrue();
        // and named as a song with its own drums is
        await Assert.That(read.Identity).IsEqualTo((Mixed with { DrumPieces = DrumPieceSettings.Drawn }).Identity);
    }

    [Test]
    public async Task AFormatBefore_ReadsAsItWas_ItsTempoAsTheSongDrawsIt()
    {
        var read = SongSettings.Parse(MixedTextThree)!;

        await Assert.That(AreSame(read, Mixed with { Tempo = new Setting(false, TempoScale.Middle), DrumPieces = DrumPieceSettings.Drawn })).IsTrue();
    }

    [Test]
    public async Task TheOldestFormat_ReadsAsItWas_ItsTempoAndSoloPartAsTheSongDrawsThem()
    {
        var read = SongSettings.Parse(MixedTextTwo)!;

        await Assert.That(read.Parts.Length).IsEqualTo(SongSettings.PartOrder.Length);
        await Assert.That(read.Parts.Take(8).SequenceEqual(Mixed.Parts.Take(8))).IsTrue();
        await Assert.That(read.Parts[^1]).IsEqualTo(new PartSettings(0, new Setting(false, 0), 63, new Setting(false, 32)));
        await Assert.That(read.Tempo).IsEqualTo(new Setting(false, TempoScale.Middle));
        // and named as it was, a part it did not name as drawn
        await Assert.That(read.Identity).IsEqualTo((Mixed with { Tempo = read.Tempo, Parts = [..Mixed.Parts.Take(8), read.Parts[^1]], DrumPieces = DrumPieceSettings.Drawn }).Identity);
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
