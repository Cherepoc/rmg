using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Events;
using Rmg.Core.Rendering;

namespace Rmg.Tests.Midis;

public sealed class MidiWriteTest
{
    [Test]
    public async Task OneTrack()
    {
        TimelineItem<RenderedNote>[] renderedNotes =
        [
            new RenderedNote(64, 1, 1).ToTimelineItem(0),
        ];
        var noteTimeline = EventTimeline.Create(1, renderedNotes);
        var track = new RenderedTrack(false, 1, noteTimeline);
        
        ImmutableArray<RenderedTrack> tracks = [
            track
        ];
        var renderedSong = new RenderedSong(1, StateTimeline<double>.Empty, tracks);
        
        var memoryStream = new MemoryStream();
        renderedSong.Write(memoryStream);
        var result = memoryStream.ToArray();

        byte[] expected =
        [
            0x4D, 0x54, 0x68, 0x64, 0x00, 0x00, 0x00, 0x06, 0x00, 0x01, 0x00, 0x02, 0x00, 0x60, 0x4D, 0x54,
            0x72, 0x6B, 0x00, 0x00, 0x00, 0x13, 0x00, 0xFF, 0x58, 0x04, 0x04, 0x02, 0x18, 0x08, 0x00, 0xFF,
            0x51, 0x03, 0x07, 0xA1, 0x20, 0x60, 0xFF, 0x2F, 0x00, 0x4D, 0x54, 0x72, 0x6B, 0x00, 0x00, 0x00,
            0x0F, 0x00, 0xC0, 0x01, 0x00, 0x90, 0x40, 0x7F, 0x60, 0x80, 0x40, 0x40, 0x00, 0xFF, 0x2F, 0x00,
        ];

        await Assert.That(result).IsEquivalentTo(expected);
    }
}