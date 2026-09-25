using Rmg.WebApi.Songs;

namespace Rmg.Tests.WebApi;

public sealed class GenerateSongRequestTest
{
    [Test]
    public async Task NoTracks_ReadsNone()
    {
        var request = new GenerateSongRequest(42);

        var result = request.TryGetChannelTracks(out var channelTracks, out var error);

        await Assert.That(result).IsTrue();
        await Assert.That(error).IsNull();
        await Assert.That(channelTracks).IsEmpty();
        await Assert.That(request.SongVolume).IsEqualTo(1);
    }

    [Test]
    public async Task Tracks_AreReadIntoTheChannelsOfAWrittenSong()
    {
        var request = new GenerateSongRequest(
            42,
            Tracks: [new TrackRequest(1, 40), new TrackRequest(10, Volume: 0.5, IsEnabled: false)]
        );

        var result = request.TryGetChannelTracks(out var channelTracks, out var error);

        await Assert.That(result).IsTrue();
        await Assert.That(error).IsNull();
        await Check.That(channelTracks.Keys).IsEquivalentTo(new byte[] { 0, 9 });
        await Assert.That(channelTracks[0].Instrument).IsEqualTo(40);
        await Assert.That(channelTracks[9].Volume).IsEqualTo(0.5);
        await Assert.That(channelTracks[9].IsEnabled).IsFalse();
    }

    [Test]
    public async Task TrackIsEnabledUnlessItSaysOtherwise()
    {
        var request = new GenerateSongRequest(42, Tracks: [new TrackRequest(1)]);

        request.TryGetChannelTracks(out var channelTracks, out _);

        await Assert.That(channelTracks[0].IsEnabled).IsTrue();
        await Assert.That(channelTracks[0].Volume).IsNull();
        await Assert.That(channelTracks[0].Instrument).IsNull();
    }

    [Test]
    [Arguments(0)]
    [Arguments(17)]
    [Arguments(-1)]
    public async Task ChannelOutsideMidi_Fails(int channel)
    {
        var request = new GenerateSongRequest(42, Tracks: [new TrackRequest(channel, 40)]);

        var result = request.TryGetChannelTracks(out _, out var error);

        await Assert.That(result).IsFalse();
        await Assert.That(error).IsEqualTo($"{channel} is not a MIDI channel. Channels are 1 to 16.");
    }

    [Test]
    [Arguments(-1)]
    [Arguments(128)]
    public async Task InstrumentOutsideGeneralMidi_Fails(int instrument)
    {
        var request = new GenerateSongRequest(42, Tracks: [new TrackRequest(1, instrument)]);

        var result = request.TryGetChannelTracks(out _, out var error);

        await Assert.That(result).IsFalse();
        await Assert.That(error)
            .IsEqualTo($"{instrument} is not a General MIDI instrument. Instruments are 0 to 127.");
    }

    [Test]
    [Arguments(-0.5)]
    [Arguments(1.5)]
    public async Task TrackVolumeOutsideItsRange_Fails(double volume)
    {
        var request = new GenerateSongRequest(42, Tracks: [new TrackRequest(1, Volume: volume)]);

        var result = request.TryGetChannelTracks(out _, out var error);

        await Assert.That(result).IsFalse();
        await Assert.That(error).IsEqualTo($"Channel 1 volume {volume} is not between 0 and 1.");
    }

    [Test]
    public async Task SongVolumeOutsideItsRange_Fails()
    {
        var request = new GenerateSongRequest(42, 2);

        var result = request.TryGetChannelTracks(out _, out var error);

        await Assert.That(result).IsFalse();
        await Assert.That(error).IsEqualTo("Song volume 2 is not between 0 and 1.");
    }

    [Test]
    public async Task ChannelAskedTwice_Fails()
    {
        var request = new GenerateSongRequest(42, Tracks: [new TrackRequest(1, 40), new TrackRequest(1, 41)]);

        var result = request.TryGetChannelTracks(out _, out var error);

        await Assert.That(result).IsFalse();
        await Assert.That(error).IsEqualTo("Channel 1 appears more than once.");
    }
}
