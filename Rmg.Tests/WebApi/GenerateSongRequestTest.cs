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
        var isRead = new GenerateSongRequest(42).TryRead(out var overrides, out var mix, out var error);

        await Assert.That(isRead).IsTrue();
        await Assert.That(error).IsNull();
        await Assert.That(overrides).IsEqualTo(SongOverrides.None);
        await Assert.That((mix.Volume, mix.Parts.Count, mix.DrumGroups.Count)).IsEqualTo((1.0, 0, 0));
    }

    [Test]
    public async Task EveryStep_ReadsAsItsShareOf127()
    {
        var request = new GenerateSongRequest(
            42,
            Unconventionality: 127,
            Facets: new Dictionary<string, int> { ["chords"] = 0 },
            Volume: 64,
            Parts: new Dictionary<string, PartRequest> { ["bass"] = new(Plays: false, Instrument: 73, Volume: 127, Pan: 0), ["counterMelody"] = new(Plays: true, Pan: 127) },
            DrumSetup: "percussion",
            DrumGroups: new Dictionary<string, DrumGroupRequest> { ["kick"] = new(Volume: 0, IsOn: false) }
        );

        await Assert.That(request.TryRead(out var overrides, out var mix, out _)).IsTrue();
        await Assert.That(overrides.Base).IsEqualTo(1.0);
        await Assert.That(overrides.Facets![Facet.Chords]).IsEqualTo(0.0);
        await Assert.That(overrides.Parts!.ToArray()).IsEquivalentTo(new[] { KeyValuePair.Create(TrackRole.Bass, false), KeyValuePair.Create(TrackRole.CounterMelody, true) });
        await Assert.That(overrides.DrumSetup).IsEqualTo(DrumSetup.Percussion);
        await Assert.That(mix.Volume).IsEqualTo(64 / 127.0);
        await Assert.That(mix.Parts[TrackRole.Bass]).IsEqualTo(new PartMix(73, 1, -1, true));
        await Assert.That(mix.Parts[TrackRole.CounterMelody].Pan).IsEqualTo(1.0);
        await Assert.That(mix.DrumGroups["Kick"]).IsEqualTo(new DrumGroupMix(0, false));
    }

    [Test]
    [Arguments(0, -1.0)]
    [Arguments(64, 0.0)]
    [Arguments(127, 1.0)]
    public async Task APan_ReachesBothSides_AndTheMiddle(int step, double pan)
    {
        var request = new GenerateSongRequest(Parts: new Dictionary<string, PartRequest> { ["melody"] = new(Pan: step) });

        request.TryRead(out _, out var mix, out _);

        await Assert.That(mix.Parts[TrackRole.Melody].Pan).IsEqualTo(pan);
        await Assert.That(GenerateSongRequest.ToPanStep(pan)).IsEqualTo(step);
    }

    [Test]
    public async Task EveryPartLeftOut_IsRefused()
    {
        var parts = Enum.GetNames<TrackRole>().ToDictionary(x => x, _ => new PartRequest(Plays: false));

        await Assert.That(new GenerateSongRequest(Parts: parts).TryRead(out _, out _, out var error)).IsFalse();
        await Assert.That(error).Contains("nothing to play");
    }

    [Test]
    [Arguments("unconventionality")]
    [Arguments("facet")]
    [Arguments("unknownFacet")]
    [Arguments("part")]
    [Arguments("instrument")]
    [Arguments("setup")]
    [Arguments("group")]
    public async Task AnythingOutOfItsRange_OrUnknown_IsRefused(string what)
    {
        var request = what switch
        {
            "unconventionality" => new GenerateSongRequest(Unconventionality: 128),
            "facet" => new GenerateSongRequest(Facets: new Dictionary<string, int> { ["feel"] = -1 }),
            "unknownFacet" => new GenerateSongRequest(Facets: new Dictionary<string, int> { ["tempo"] = 1 }),
            "part" => new GenerateSongRequest(Parts: new Dictionary<string, PartRequest> { ["kazoo"] = new() }),
            "instrument" => new GenerateSongRequest(Parts: new Dictionary<string, PartRequest> { ["bass"] = new(Instrument: 128) }),
            "setup" => new GenerateSongRequest(DrumSetup: "orchestra"),
            "group" => new GenerateSongRequest(DrumGroups: new Dictionary<string, DrumGroupRequest> { ["gong"] = new() }),
            _ => throw new ArgumentOutOfRangeException(nameof(what))
        };

        await Assert.That(request.TryRead(out _, out _, out var error)).IsFalse();
        await Assert.That(error).IsNotNull();
    }
}
