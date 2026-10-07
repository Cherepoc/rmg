using Rmg.Core.Composition;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;
using Rmg.WebApi.Songs;

namespace Rmg.Tests.WebApi;

public sealed class GenerateSongRequestTest
{
    [Test]
    public async Task NothingGiven_IsTheSongAsItsSeedMakesIt()
    {
        var isRead = new GenerateSongRequest("g").TryRead(out var seed, out var overrides, out var mix, out var error);

        await Assert.That(isRead).IsTrue();
        await Assert.That(error).IsNull();
        await Assert.That(seed).IsEqualTo(42UL);
        await Assert.That(overrides).IsEqualTo(SongOverrides.None);
        await Assert.That((mix.Volume, mix.Parts.Count, mix.DrumGroups.Count)).IsEqualTo((1.0, 0, 0));
    }

    [Test]
    public async Task StagesLeftOut_AreThoseASongMayBeMadeWithout_AnyOtherRefused()
    {
        await Assert.That(new GenerateSongRequest("g", Skip: ["Pushes", "Stop time"]).TryReadSkip(out var skipped, out _)).IsTrue();
        await Assert.That(skipped.SetEquals(["Pushes", "Stop time"])).IsTrue();
        await Assert.That(new GenerateSongRequest("g").TryReadSkip(out skipped, out _)).IsTrue();
        await Assert.That(skipped).IsEmpty();
        // the notes are a stage every song plays
        await Assert.That(new GenerateSongRequest("g", Skip: ["Notes"]).TryReadSkip(out _, out var error)).IsFalse();
        await Assert.That(error).Contains("Notes");
        await Assert.That(() => SongGenerator.GenerateSong(1, SongOverrides.None, new HashSet<string> { "Notes" }, CancellationToken.None)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task EveryStep_ReadsAsItsAmount_TheMiddleStepAHalf()
    {
        var request = new GenerateSongRequest(
            "g",
            Unconventionality: 61,
            Facets: new Dictionary<string, int> { ["chords"] = 0 },
            Volume: 31,
            Parts: new Dictionary<string, PartRequest> { ["bass"] = new(Plays: false, Instrument: 73, Volume: 61, Pan: 0), ["counterMelody"] = new(Plays: true, Pan: 61) },
            DrumGroups: new Dictionary<string, DrumGroupRequest> { ["kick"] = new(Volume: 0, IsOn: false) }
        );

        await Assert.That(request.TryRead(out _, out var overrides, out var mix, out _)).IsTrue();
        await Assert.That(overrides.Base).IsEqualTo(1.0);
        await Assert.That(overrides.Facets![Facet.Chords]).IsEqualTo(0.0);
        await Assert.That(overrides.Parts!.ToArray()).IsEquivalentTo(new[] { KeyValuePair.Create(TrackRole.Bass, false), KeyValuePair.Create(TrackRole.CounterMelody, true) });
        await Assert.That(mix.Volume).IsEqualTo(0.5);
        await Assert.That(mix.Parts[TrackRole.Bass]).IsEqualTo(new PartMix(73, 1, -1, true));
        await Assert.That(mix.Parts[TrackRole.CounterMelody].Pan).IsEqualTo(1.0);
        await Assert.That(mix.DrumGroups["Kick"]).IsEqualTo(new DrumGroupMix(0, false));
    }

    [Test]
    [Arguments(0, -1.0)]
    [Arguments(31, 0.0)]
    [Arguments(61, 1.0)]
    public async Task APan_ReachesBothSides_AndTheMiddle(int step, double pan)
    {
        var request = new GenerateSongRequest(Parts: new Dictionary<string, PartRequest> { ["melody"] = new(Pan: step) });

        request.TryRead(out _, out _, out var mix, out _);

        await Assert.That(mix.Parts[TrackRole.Melody].Pan).IsEqualTo(pan);
        await Assert.That(GenerateSongRequest.ToPanStep(pan)).IsEqualTo(step);
    }

    [Test]
    public async Task DrumPieces_AreGivenAsASet()
    {
        await Assert.That(new GenerateSongRequest(DrumPieces: ["kick", "conga", "kick"]).TryRead(out _, out var overrides, out _, out _)).IsTrue();
        await Assert.That(overrides.DrumPieces!.SetEquals(["kick", "conga"])).IsTrue();
    }

    [Test]
    [Arguments(0, 60.0)]
    [Arguments(31, 120.0)]
    [Arguments(61, 240.0)]
    public async Task ATempoStep_IsGivenAsItsTempo(int step, double beatsAMinute)
    {
        await Assert.That(new GenerateSongRequest(Tempo: step).TryRead(out _, out var overrides, out _, out _)).IsTrue();
        await Assert.That(overrides.Tempo!.Value * Meter.BaseTempo).IsEqualTo(beatsAMinute).Within(1e-9);
    }

    [Test]
    public async Task AModeAndASwing_AreGivenAsTheScaleAndAPartOfATripletsSwing()
    {
        await Assert.That(new GenerateSongRequest(Mode: 2, Swing: StepScale.Last).TryRead(out _, out var overrides, out _, out _)).IsTrue();
        await Assert.That(overrides.Scale).IsEqualTo(Core.Composition.Scales.Dorian);
        await Assert.That(overrides.Swing).IsEqualTo(1);
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, System.Text.Json.JsonElement>> Genre(string choice, string option, object amounts) =>
        new Dictionary<string, IReadOnlyDictionary<string, System.Text.Json.JsonElement>>
            { [choice] = new Dictionary<string, System.Text.Json.JsonElement> { [option] = System.Text.Json.JsonSerializer.SerializeToElement(amounts) } };

    [Test]
    public async Task AGenre_IsGivenByItsChoicesNames_ItsAmountsWholePercents_OneAlikeAtEveryEnd_OrThree()
    {
        await Assert.That(new GenerateSongRequest(Genre: Genre("parts", "pad", 50)).TryRead(out _, out var overrides, out _, out var error)).IsTrue().Because(error ?? "");
        // the pad's chances alike at every end; the riff, a part the genre leaves out, never
        await Assert.That(overrides.Genre!.Chance(GenreChoices.Parts, "pad")).IsEqualTo(new Rmg.Core.Probabilities.ByConvention(0.5, 0.5, 0.5));
        await Assert.That(overrides.Genre.Chance(GenreChoices.Parts, "riff")).IsEqualTo(default(Rmg.Core.Probabilities.ByConvention));

        await Assert.That(new GenerateSongRequest(Genre: Genre("meter", "3/4", new[] { 0, 20, 100 })).TryRead(out _, out overrides, out _, out _)).IsTrue();
        await Assert.That(overrides.Genre!.Pool<string>(GenreChoices.Meter, x => x)!("3/4")).IsEqualTo(new Rmg.Core.Probabilities.ByConvention(0, 0.2, 1));
    }

    [Test]
    public async Task EveryPartLeftOut_IsRefused()
    {
        var parts = Enum.GetNames<TrackRole>().ToDictionary(x => x, _ => new PartRequest(Plays: false));

        await Assert.That(new GenerateSongRequest(Parts: parts).TryRead(out _, out _, out _, out var error)).IsFalse();
        await Assert.That(error).Contains("nothing to play");
    }

    [Test]
    [Arguments("unconventionality")]
    [Arguments("facet")]
    [Arguments("unknownFacet")]
    [Arguments("part")]
    [Arguments("instrument")]
    [Arguments("group")]
    [Arguments("seed")]
    [Arguments("tempo")]
    [Arguments("key")]
    [Arguments("meter")]
    [Arguments("mode")]
    [Arguments("swing")]
    [Arguments("genreChoice")]
    [Arguments("genreOption")]
    [Arguments("genreStep")]
    [Arguments("genreEnds")]
    [Arguments("genreEmptyPool")]
    [Arguments("noPieces")]
    [Arguments("piece")]
    public async Task AnythingOutOfItsRange_OrUnknown_IsRefused(string what)
    {
        var request = what switch
        {
            "unconventionality" => new GenerateSongRequest(Unconventionality: 62),
            "facet" => new GenerateSongRequest(Facets: new Dictionary<string, int> { ["feel"] = -1 }),
            "unknownFacet" => new GenerateSongRequest(Facets: new Dictionary<string, int> { ["tempo"] = 1 }),
            "part" => new GenerateSongRequest(Parts: new Dictionary<string, PartRequest> { ["kazoo"] = new() }),
            "instrument" => new GenerateSongRequest(Parts: new Dictionary<string, PartRequest> { ["bass"] = new(Instrument: 128) }),
            "noPieces" => new GenerateSongRequest(DrumPieces: []),
            "piece" => new GenerateSongRequest(DrumPieces: ["kick", "gong"]),
            "group" => new GenerateSongRequest(DrumGroups: new Dictionary<string, DrumGroupRequest> { ["gong"] = new() }),
            "seed" => new GenerateSongRequest("zzzzzzzzzzzz"),
            "tempo" => new GenerateSongRequest(Tempo: 62),
            "key" => new GenerateSongRequest(Key: 12),
            "meter" => new GenerateSongRequest(Meter: -1),
            "mode" => new GenerateSongRequest(Mode: Core.Composition.Scales.All.Length),
            "swing" => new GenerateSongRequest(Swing: 62),
            "genreChoice" => new GenerateSongRequest(Genre: Genre("kazoo", "3/4", 63)),
            "genreOption" => new GenerateSongRequest(Genre: Genre("meter", "1/1", 63)),
            "genreStep" => new GenerateSongRequest(Genre: Genre("meter", "3/4", 101)),
            "genreEnds" => new GenerateSongRequest(Genre: Genre("meter", "3/4", new[] { 0, 100 })),
            "genreEmptyPool" => new GenerateSongRequest(Genre: new Dictionary<string, IReadOnlyDictionary<string, System.Text.Json.JsonElement>> { ["meter"] = new Dictionary<string, System.Text.Json.JsonElement>() }),
            _ => throw new ArgumentOutOfRangeException(nameof(what))
        };

        await Assert.That(request.TryRead(out _, out _, out _, out var error)).IsFalse();
        await Assert.That(error).IsNotNull();
    }
}
