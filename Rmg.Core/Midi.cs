using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Rendering;

namespace Rmg.Core;

public static class Midi
{
    private const uint TicksPerQuarterNote = 96;
    private const byte PercussionChannel = 9;

    internal static byte[] IntToBytesFixed(uint value, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, 4);

        var intBytes = BitConverter.GetBytes(value);
        var result = new byte[length];
        for (var i = 0; i < length; i++)
        {
            var sourceIndex = BitConverter.IsLittleEndian
                ? length - 1 - i
                : i;
            result[i] = intBytes[sourceIndex];
        }

        return result;
    }

    internal static byte[] IntToBytesVariable(uint value)
    {
        const int byteBitCount = 7;
        const uint excludeHighestBitMask = 0x7f;
        const uint includeHighestBitMask = 0x80;

        var bitCount = value > 0
            ? (int)Math.Floor(Math.Log2(value)) + 1
            : 1;
        var byteCount = (int)Math.Ceiling(bitCount / (double)byteBitCount);
        var lastByteIndex = byteCount - 1;
        var result = new byte[byteCount];
        for (var i = 0; i < byteCount; i++)
        {
            var shiftedValue = value >> (byteBitCount * (lastByteIndex - i));
            var maskedValue = i < lastByteIndex
                ? shiftedValue | includeHighestBitMask
                : shiftedValue & excludeHighestBitMask;
            result[i] = (byte)maskedValue;
        }

        return result;
    }

    private static uint AbsoluteDelta(double position) => (uint)Math.Round(position * TicksPerQuarterNote);

    private static byte ChannelMidiEventHeader(byte channel, byte eventType)
    {
        var value = (eventType << 4) | (channel & 0x0F);
        return (byte)value;
    }

    private static byte[] NoteOn(byte channel, byte note, double velocity)
    {
        var header = ChannelMidiEventHeader(channel, 0x09);
        return [header, note, (byte)(Math.Floor(velocity * 127) + 1)];
    }

    private static byte[] NoteOff(byte channel, byte note)
    {
        var header = ChannelMidiEventHeader(channel, 0x08);
        return [header, note, 0x40];
    }

    private static byte[] ProgramChange(byte channel, byte program)
    {
        var header = ChannelMidiEventHeader(channel, 0x0C);
        return [header, program];
    }

    private static byte[] TimeSignature(byte numerator, byte denominator)
    {
        return
        [
            0xff,
            0x58,
            0x04,
            numerator,
            (byte)Math.Log2(denominator),
            0x18,
            0x08,
        ];
    }

    private static byte[] Tempo(double tempo)
    {
        var tempoValue = (uint)(0.5 * 1_000_000.0 / tempo);
        return
        [
            0xff,
            0x51,
            0x03,
            ..IntToBytesFixed(tempoValue, 3)
        ];
    }

    private static byte[] EndOfTrack()
    {
        return
        [
            0xff,
            0x2f,
            0x00,
        ];
    }

    private static byte PitchTrackChannel(int pitchTrackIndex)
    {
        if (pitchTrackIndex is < 0 or > 14)
            throw new ArgumentOutOfRangeException(nameof(pitchTrackIndex), "Channel must be between 0 and 15.");

        var fixedChannelValue = pitchTrackIndex >= PercussionChannel
            ? pitchTrackIndex + 1
            : pitchTrackIndex;
        return (byte)fixedChannelValue;
    }

    private static IEnumerable<MidiEvent> ToRelativeDeltas(this IEnumerable<MidiEvent> events)
    {
        uint lastDelta = 0;
        foreach (var midiEvent in events)
        {
            var relativeDelta = midiEvent.Delta - lastDelta;
            lastDelta = midiEvent.Delta;
            yield return midiEvent with { Delta = relativeDelta };
        }
    }

    private static IEnumerable<MidiEvent> ToEvents(
        this TimelineItem<RenderedNote> renderedNoteTimelineItem,
        byte channel, uint durationDelta
    )
    {
        var (position, renderedNote) = (renderedNoteTimelineItem.Position, renderedNoteTimelineItem.Value);

        var noteOnDelta = AbsoluteDelta(position);
        var noteOnBytes = NoteOn(channel, (byte)renderedNote.Offset, renderedNote.Velocity);
        yield return new MidiEvent(noteOnDelta, noteOnBytes);

        var noteOffDelta = AbsoluteDelta(position + renderedNote.Duration);
        if (noteOffDelta > durationDelta)
            noteOffDelta = durationDelta;
        var noteOffBytes = NoteOff(channel, (byte)renderedNote.Offset);
        yield return new MidiEvent(noteOffDelta, noteOffBytes);
    }

    private static void WriteTrack(IEnumerable<MidiEvent> events, uint durationDelta, Stream stream)
    {
        var allEvents = events
            .Append(new MidiEvent(durationDelta, EndOfTrack()))
            .OrderBy(x => x.Delta)
            .ToRelativeDeltas()
            .SelectMany(x => x.ToBytes())
            .ToArray();

        stream.WriteByte(0x4D);
        stream.WriteByte(0x54);
        stream.WriteByte(0x72);
        stream.WriteByte(0x6B);

        var trackLength = IntToBytesFixed((uint)allEvents.Length, 4);
        stream.Write(trackLength, 0, trackLength.Length);

        stream.Write(allEvents, 0, allEvents.Length);
    }

    private static void WriteSystemTrack(StateTimeline<double> tempoTimeline, uint durationDelta, Stream stream)
    {
        var events = tempoTimeline.Select(x => new MidiEvent(AbsoluteDelta(x.Position), Tempo(x.Value)));
        if (tempoTimeline.IsEmpty || tempoTimeline[0].Position > 0)
            events = events.Prepend(new MidiEvent(0, Tempo(1)));

        events = events.Prepend(new MidiEvent(0, TimeSignature(4, 4)));

        WriteTrack(events, durationDelta, stream);
    }

    private static void WriteNoteTrack(
        EventTimeline<RenderedNote> noteTimeline,
        byte instrumentCode,
        byte channel,
        uint durationDelta,
        Stream stream
    )
    {
        var events = noteTimeline
            .SelectMany(x => x.ToEvents(channel, durationDelta))
            .Prepend(new MidiEvent(0, ProgramChange(channel, instrumentCode)));
        WriteTrack(events, durationDelta, stream);
    }

    private static IEnumerable<(byte channel, RenderedTrack track)> ToIndexedTracks(
        this ImmutableArray<RenderedTrack> tracks
    )
    {
        var percussionTracks = tracks
            .Where(x => x.IsPercussionInstrument)
            .ToArray();
        if (percussionTracks.Length > 1)
            throw new ArgumentException("Only one percussion track is allowed.");

        var indexedTracks = tracks
            .Where(x => !x.IsPercussionInstrument)
            .Select((track, index) => (PitchTrackChannel(index), track));

        if (percussionTracks.Length == 1)
            indexedTracks = indexedTracks
                .Append((PercussionChannel, percussionTracks[0]))
                .OrderBy(x => x.Item1);

        return indexedTracks;
    }

    public static void Write(this RenderedSong song, Stream stream)
    {
        var songDurationDelta = AbsoluteDelta(song.Duration);

        stream.WriteByte(0x4D);
        stream.WriteByte(0x54);
        stream.WriteByte(0x68);
        stream.WriteByte(0x64);
        stream.WriteByte(0x00);
        stream.WriteByte(0x00);
        stream.WriteByte(0x00);
        stream.WriteByte(0x06);
        stream.WriteByte(0x00);
        stream.WriteByte(0x01);

        var trackCountBytes = IntToBytesFixed((uint)song.Tracks.Length + 1, 2);
        stream.Write(trackCountBytes, 0, trackCountBytes.Length);

        var ticksPerQuarterNoteBytes = IntToBytesFixed(TicksPerQuarterNote, 2);
        stream.Write(ticksPerQuarterNoteBytes, 0, ticksPerQuarterNoteBytes.Length);

        WriteSystemTrack(song.TempoTimeline, songDurationDelta, stream);

        var indexedTracks = song.Tracks.ToIndexedTracks();
        foreach (var (channel, track) in indexedTracks)
        {
            var instrumentCode = (byte)track.PitchInstrumentCode;
            WriteNoteTrack(track.NoteTimeline, instrumentCode, channel, songDurationDelta, stream);
        }
    }

    private readonly record struct MidiEvent(uint Delta, byte[] Data) : IComparable<MidiEvent>
    {
        public int CompareTo(MidiEvent other)
        {
            return Delta.CompareTo(other.Delta);
        }

        public IEnumerable<byte> ToBytes()
        {
            var deltaBytes = IntToBytesVariable(Delta);
            foreach (var t in deltaBytes)
                yield return t;
            foreach (var t in Data)
                yield return t;
        }
    };
}