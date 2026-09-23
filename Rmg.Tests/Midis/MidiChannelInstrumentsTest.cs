using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Events;
using Rmg.Core.Rendering;

namespace Rmg.Tests.Midis;

public sealed class MidiChannelInstrumentsTest
{
    private static RenderedTrack Track(bool isPercussionInstrument, int instrument)
    {
        TimelineItem<RenderedNote>[] renderedNotes =
        [
            new RenderedNote(64, 1, 1).ToTimelineItem(0),
        ];
        return new RenderedTrack(isPercussionInstrument, instrument, EventTimeline.Create(1, renderedNotes));
    }

    private static RenderedSong Song(params RenderedTrack[] tracks)
    {
        return new RenderedSong(1, StateTimeline<double>.Empty, [..tracks]);
    }

    [Test]
    public async Task GetChannelInstruments_PitchTracksTakeTheChannelsInOrder()
    {
        var song = Song(Track(false, 1), Track(false, 2), Track(false, 3));

        var result = song.GetChannelInstruments();

        await Assert.That(result).IsEquivalentTo(new Dictionary<byte, int> { [0] = 1, [1] = 2, [2] = 3 });
    }

    [Test]
    public async Task GetChannelInstruments_PercussionTrackTakesThePercussionChannel()
    {
        var song = Song(Track(false, 1), Track(true, 16), Track(false, 2));

        var result = song.GetChannelInstruments();

        await Assert.That(result).IsEquivalentTo(new Dictionary<byte, int> { [0] = 1, [1] = 2, [9] = 16 });
    }

    [Test]
    public async Task WithChannelInstruments_ReplacesOnlyTheGivenChannels()
    {
        var song = Song(Track(false, 1), Track(true, 0), Track(false, 2));

        var result = song.WithChannelInstruments(new Dictionary<byte, int> { [1] = 40, [9] = 16 });

        await Assert.That(result.GetChannelInstruments())
            .IsEquivalentTo(new Dictionary<byte, int> { [0] = 1, [1] = 40, [9] = 16 });
    }

    [Test]
    public async Task WithChannelInstruments_KeepsEveryNote()
    {
        var song = Song(Track(false, 1), Track(false, 2));

        var result = song.WithChannelInstruments(new Dictionary<byte, int> { [0] = 40 });

        await Assert.That(result.Duration).IsEqualTo(song.Duration);
        for (var index = 0; index < song.Tracks.Length; index++)
            await Assert.That(result.Tracks[index].NoteTimeline).IsEquivalentTo(song.Tracks[index].NoteTimeline);
    }

    [Test]
    public async Task WithChannelInstruments_NoInstruments_ReturnsTheSameSong()
    {
        var song = Song(Track(false, 1));

        var result = song.WithChannelInstruments(new Dictionary<byte, int>());

        await Assert.That(result).IsSameReferenceAs(song);
    }

    [Test]
    public async Task WithChannelInstruments_ChannelTheSongDoesNotPlay_Throws()
    {
        var song = Song(Track(false, 1));

        await Assert.That(() => song.WithChannelInstruments(new Dictionary<byte, int> { [9] = 16 }))
            .Throws<ArgumentException>();
    }

    [Test]
    [Arguments(-1)]
    [Arguments(128)]
    public async Task WithChannelInstruments_InstrumentOutsideGeneralMidi_Throws(int instrument)
    {
        var song = Song(Track(false, 1));

        await Assert.That(() => song.WithChannelInstruments(new Dictionary<byte, int> { [0] = instrument }))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task WithChannelVolumes_ReplacesOnlyTheGivenChannels()
    {
        var song = Song(Track(false, 1), Track(true, 0));

        var result = song.WithChannelVolumes(new Dictionary<byte, double> { [9] = 0.5 });

        await Assert.That(result.Tracks[0].Volume).IsEqualTo(1);
        await Assert.That(result.Tracks[1].Volume).IsEqualTo(0.5);
        await Assert.That(result.Tracks[1].PitchInstrumentCode).IsEqualTo(0);
    }

    [Test]
    [Arguments(-0.1)]
    [Arguments(1.1)]
    public async Task WithChannelVolumes_VolumeOutsideItsRange_Throws(double volume)
    {
        var song = Song(Track(false, 1));

        await Assert.That(() => song.WithChannelVolumes(new Dictionary<byte, double> { [0] = volume }))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task WithoutChannels_LeavesTheTrackOut()
    {
        var song = Song(Track(false, 1), Track(false, 2), Track(true, 0));

        var result = song.WithoutChannels([0]);

        // the pitched channels close up behind the one that went, and the percussion one stays where it is
        await Assert.That(result.Tracks.Length).IsEqualTo(2);
        await Assert.That(result.GetChannelInstruments())
            .IsEquivalentTo(new Dictionary<byte, int> { [0] = 2, [9] = 0 });
    }

    [Test]
    public async Task WithoutChannels_EveryChannel_LeavesNoTracks()
    {
        var song = Song(Track(false, 1), Track(true, 0));

        var result = song.WithoutChannels([0, 9]);

        await Assert.That(result.Tracks).IsEmpty();
    }

    [Test]
    public async Task WithoutChannels_ChannelTheSongDoesNotPlay_Throws()
    {
        var song = Song(Track(false, 1));

        await Assert.That(() => song.WithoutChannels([9])).Throws<ArgumentException>();
    }

    [Test]
    public async Task Write_WritesTheReplacedInstrument()
    {
        var song = Song(Track(false, 1)).WithChannelInstruments(new Dictionary<byte, int> { [0] = 0x40 });

        var memoryStream = new MemoryStream();
        song.Write(memoryStream);
        var result = memoryStream.ToArray();

        byte[] expected =
        [
            0x4D, 0x54, 0x68, 0x64, 0x00, 0x00, 0x00, 0x06, 0x00, 0x01, 0x00, 0x02, 0x00, 0x60, 0x4D, 0x54,
            0x72, 0x6B, 0x00, 0x00, 0x00, 0x13, 0x00, 0xFF, 0x58, 0x04, 0x04, 0x02, 0x18, 0x08, 0x00, 0xFF,
            0x51, 0x03, 0x07, 0xA1, 0x20, 0x60, 0xFF, 0x2F, 0x00, 0x4D, 0x54, 0x72, 0x6B, 0x00, 0x00, 0x00,
            0x0F, 0x00, 0xC0, 0x40, 0x00, 0x90, 0x40, 0x7F, 0x60, 0x80, 0x40, 0x40, 0x00, 0xFF, 0x2F, 0x00,
        ];

        await Assert.That(result).IsEquivalentTo(expected);
    }

    [Test]
    public async Task Write_WritesAQuieterVolumeAsAChannelVolume()
    {
        var song = Song(Track(false, 1)).WithChannelVolumes(new Dictionary<byte, double> { [0] = 0.5 });

        var memoryStream = new MemoryStream();
        song.Write(memoryStream);
        var result = memoryStream.ToArray();

        byte[] expected =
        [
            0x4D, 0x54, 0x68, 0x64, 0x00, 0x00, 0x00, 0x06, 0x00, 0x01, 0x00, 0x02, 0x00, 0x60, 0x4D, 0x54,
            0x72, 0x6B, 0x00, 0x00, 0x00, 0x13, 0x00, 0xFF, 0x58, 0x04, 0x04, 0x02, 0x18, 0x08, 0x00, 0xFF,
            0x51, 0x03, 0x07, 0xA1, 0x20, 0x60, 0xFF, 0x2F, 0x00, 0x4D, 0x54, 0x72, 0x6B, 0x00, 0x00, 0x00,
            0x13, 0x00, 0xC0, 0x01, 0x00, 0xB0, 0x07, 0x32, 0x00, 0x90, 0x40, 0x7F, 0x60, 0x80, 0x40, 0x40,
            0x00, 0xFF, 0x2F, 0x00,
        ];

        await Assert.That(result).IsEquivalentTo(expected);
    }

    [Test]
    public async Task Write_FullVolume_WritesNoChannelVolume()
    {
        var song = Song(Track(false, 1)).WithChannelVolumes(new Dictionary<byte, double> { [0] = 1 });

        var memoryStream = new MemoryStream();
        song.Write(memoryStream);

        // the song plays at that volume unasked, so nothing about it is worth a byte
        await Assert.That(memoryStream.ToArray().Contains((byte)0xB0)).IsFalse();
    }
}
