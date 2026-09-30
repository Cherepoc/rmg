using Rmg.Core.Songs;
using Rmg.Core.Composition;
using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Events;
using Rmg.Core.Rendering;

namespace Rmg.Tests.Midis;

public sealed class MidiPartChannelsTest
{
    private static RenderedTrack Track(bool isPercussionInstrument, int instrument, double volume = 1, TrackRole role = TrackRole.Chords)
    {
        TimelineItem<RenderedNote>[] renderedNotes =
        [
            new RenderedNote(64, 1, 1).ToTimelineItem(0),
        ];
        return new RenderedTrack(isPercussionInstrument, isPercussionInstrument ? TrackRole.Drum : role, instrument, EventTimeline.Create(1, renderedNotes), 0, volume);
    }

    private static RenderedSong Song(params RenderedTrack[] tracks)
    {
        return new RenderedSong(1, [(0, Meter.FourFour)], StateKinds.Tempo.CreateDefaultTimeline(0), StateKinds.Fade.CreateDefaultTimeline(0), [..tracks]);
    }

    [Test]
    public async Task PitchedParts_TakeTheChannelsInOrder_AndTheDrumsTheirOwn()
    {
        var song = Song(Track(false, 1, role: TrackRole.Melody), Track(true, 0), Track(false, 2, role: TrackRole.Bass));

        await Assert.That(song.GetPartChannels())
            .IsEquivalentTo(new Dictionary<TrackRole, byte> { [TrackRole.Melody] = 0, [TrackRole.Drum] = 9, [TrackRole.Bass] = 1 });
    }

    [Test]
    public async Task Write_WritesTheTracksInstrument()
    {
        var song = Song(Track(false, 0x40));

        var memoryStream = new MemoryStream();
        song.Write(memoryStream, null);
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
        var song = Song(Track(false, 1, 0.5));

        var memoryStream = new MemoryStream();
        song.Write(memoryStream, null);
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
        var song = Song(Track(false, 1, 1));

        var memoryStream = new MemoryStream();
        song.Write(memoryStream, null);

        // the song plays at that volume unasked, so nothing about it is worth a byte
        await Assert.That(memoryStream.ToArray().Contains((byte)0xB0)).IsFalse();
    }
}
