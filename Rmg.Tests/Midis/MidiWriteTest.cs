using Rmg.Core.Songs;
using Rmg.Core.Composition;
using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Events;
using Rmg.Core.Rendering;

namespace Rmg.Tests.Midis;

/// <summary>The header and the system track of a one-track song of one beat in 4/4, as the MIDI tests expect them.</summary>
internal static class MidiBytes
{
    public static readonly byte[] OneBeatSongStart =
    [
        0x4D, 0x54, 0x68, 0x64, 0x00, 0x00, 0x00, 0x06, 0x00, 0x01, 0x00, 0x02, 0x01, 0xE0, 0x4D, 0x54,
        0x72, 0x6B, 0x00, 0x00, 0x00, 0x14, 0x00, 0xFF, 0x58, 0x04, 0x04, 0x02, 0x18, 0x08, 0x00, 0xFF,
        0x51, 0x03, 0x07, 0xA1, 0x20, 0x83, 0x60, 0xFF, 0x2F, 0x00,
    ];
}

public sealed class MidiWriteTest
{
    private static byte[] WriteOneBeatSong(params TimelineItem<RenderedNote>[] renderedNotes)
    {
        var track = new RenderedTrack(false, TrackRole.Chords, 1, EventTimeline.Create(1, renderedNotes), 0);
        var renderedSong = new RenderedSong(1, [(0, Meter.FourFour)], StateKinds.Tempo.CreateDefaultTimeline(0), StateKinds.Fade.CreateDefaultTimeline(0), [track]);

        var memoryStream = new MemoryStream();
        renderedSong.Write(memoryStream, null);
        return memoryStream.ToArray();
    }

    [Test]
    public async Task AFade_PlaysAsTheChannelsExpression()
    {
        var fade = StateTimeline.Create(1, StateKinds.Fade, [0.75.ToTimelineItem(0), 0.5.ToTimelineItem(0.5)]);
        var track = new RenderedTrack(false, TrackRole.Chords, 1, EventTimeline.Create(1, [new RenderedNote(64, 1, 1).ToTimelineItem(0)]), 0);
        var memoryStream = new MemoryStream();
        new RenderedSong(1, [(0, Meter.FourFour)], StateKinds.Tempo.CreateDefaultTimeline(0), fade, [track]).Write(memoryStream, null);

        byte[] expected =
        [
            ..MidiBytes.OneBeatSongStart,
            0x4D, 0x54, 0x72, 0x6B, 0x00, 0x00, 0x00, 0x19, 0x00, 0xC0, 0x01,
            0x00, 0xB0, 0x0B, 0x5F, 0x00, 0x90, 0x40, 0x7F, 0x81, 0x70, 0xB0, 0x0B, 0x40,
            0x81, 0x70, 0x80, 0x40, 0x40, 0x00, 0xFF, 0x2F, 0x00,
        ];

        await Assert.That(memoryStream.ToArray()).IsEquivalentTo(expected, CollectionOrdering.Matching);
    }

    [Test]
    public async Task NoteStillPlayingWhenItsKeyIsStruckAgain_ResultsIn_NoteEndingThere()
    {
        // otherwise the note-off of the first note, at the end of the beat, would cut the second one short
        var result = WriteOneBeatSong(
            new RenderedNote(64, 1, 1).ToTimelineItem(0),
            new RenderedNote(64, 1, 0.5).ToTimelineItem(0.5)
        );

        byte[] expected =
        [
            ..MidiBytes.OneBeatSongStart,
            0x4D, 0x54, 0x72, 0x6B, 0x00, 0x00, 0x00, 0x19, 0x00, 0xC0, 0x01,
            0x00, 0x90, 0x40, 0x7F, 0x81, 0x70, 0x80, 0x40, 0x40,
            0x00, 0x90, 0x40, 0x7F, 0x81, 0x70, 0x80, 0x40, 0x40,
            0x00, 0xFF, 0x2F, 0x00,
        ];

        await Assert.That(result).IsEquivalentTo(expected, CollectionOrdering.Matching);
    }

    [Test]
    public async Task NotesOfOneKeyOnOneTick_ResultsIn_OneNote()
    {
        // the longest and the loudest of them
        var result = WriteOneBeatSong(
            new RenderedNote(64, 0.5, 1).ToTimelineItem(0),
            new RenderedNote(64, 1, 0.5).ToTimelineItem(0)
        );

        byte[] expected =
        [
            ..MidiBytes.OneBeatSongStart,
            0x4D, 0x54, 0x72, 0x6B, 0x00, 0x00, 0x00, 0x10, 0x00, 0xC0, 0x01,
            0x00, 0x90, 0x40, 0x7F, 0x83, 0x60, 0x80, 0x40, 0x40,
            0x00, 0xFF, 0x2F, 0x00,
        ];

        await Assert.That(result).IsEquivalentTo(expected, CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(new[] { 6, 6 }, 6, 3, 36)]
    [Arguments(new[] { 4, 3, 3, 3 }, 13, 4, 18)]
    [Arguments(new[] { 12, 8 }, 5, 2, 24)]
    public async Task TheTimeSignature_IsTheMeters_ItsClickOnThePulse(int[] groups, int numerator, int denominatorPower, int clocks)
    {
        var track = new RenderedTrack(false, TrackRole.Chords, 1, EventTimeline.Create(1, [new RenderedNote(64, 1, 1).ToTimelineItem(0)]), 0);
        var memoryStream = new MemoryStream();
        new RenderedSong(1, [(0, new Meter([..groups]))], StateKinds.Tempo.CreateDefaultTimeline(0), StateKinds.Fade.CreateDefaultTimeline(0), [track]).Write(memoryStream, null);

        byte[] timeSignature = [0xff, 0x58, 0x04, (byte)numerator, (byte)denominatorPower, (byte)clocks, 0x08];
        await Assert.That(memoryStream.ToArray().AsSpan().IndexOf(timeSignature)).IsGreaterThan(0);
    }
}
