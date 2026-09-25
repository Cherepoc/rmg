using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Rendering;

namespace Rmg.Core;

public static class Midi
{
    private const uint TicksPerQuarterNote = 96;
    private const byte PercussionChannel = 9;
    private const byte MainVolumeController = 7;

    /// <summary>What a channel plays at when nothing says otherwise, which General MIDI puts at 100 of 127.</summary>
    private const double DefaultChannelVolume = 100;

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

    private static uint AbsoluteDelta(double position)
    {
        return (uint)Math.Round(position * TicksPerQuarterNote);
    }

    private static byte ChannelMidiEventHeader(byte channel, byte eventType)
    {
        var value = (eventType << 4) | (channel & 0x0F);
        return (byte)value;
    }

    private static byte[] NoteOn(byte channel, byte note, double velocity)
    {
        var header = ChannelMidiEventHeader(channel, 0x09);
        return [header, note, (byte)Math.Min(127, Math.Floor(velocity * 127) + 1)];
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

    private static byte[] ControlChange(byte channel, byte controller, byte value)
    {
        var header = ChannelMidiEventHeader(channel, 0x0B);
        return [header, controller, value];
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
            0x08
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
            0x00
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

    /// <summary>
    ///     The notes as MIDI plays them. A key sounds once on a channel and a note-off ends whatever note of the key
    ///     is playing, so a note ends where the next note of its key starts, rather than cutting that note short with
    ///     its own note-off, and notes of a key that start on the same tick play as one.
    /// </summary>
    private static IEnumerable<MidiNote> ToMidiNotes(this EventTimeline<RenderedNote> noteTimeline, uint durationDelta)
    {
        return noteTimeline
            .Select(x => new MidiNote(
                AbsoluteDelta(x.Position),
                Math.Min(AbsoluteDelta(x.Position + x.Value.Duration), durationDelta),
                (byte)x.Value.Offset,
                x.Value.Velocity
            ))
            .GroupBy(x => x.Key)
            .SelectMany(TrimOverlaps)
            // a note that ends where the next note of its key starts is written before it, so its note-off comes first
            .OrderBy(x => x.OnDelta);
    }

    private static IEnumerable<MidiNote> TrimOverlaps(IEnumerable<MidiNote> keyNotes)
    {
        MidiNote? current = null;
        foreach (var note in keyNotes)
        {
            if (current is { } playing)
            {
                if (note.OnDelta == playing.OnDelta)
                {
                    current = playing with
                    {
                        OffDelta = Math.Max(playing.OffDelta, note.OffDelta),
                        Velocity = Math.Max(playing.Velocity, note.Velocity)
                    };
                    continue;
                }

                yield return playing with { OffDelta = Math.Min(playing.OffDelta, note.OnDelta) };
            }

            current = note;
        }

        if (current is { } last)
            yield return last;
    }

    private static IEnumerable<MidiEvent> ToEvents(this MidiNote note, byte channel)
    {
        yield return new MidiEvent(note.OnDelta, NoteOn(channel, note.Key, note.Velocity));
        yield return new MidiEvent(note.OffDelta, NoteOff(channel, note.Key));
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
        if (tempoTimeline.Count == 0 || tempoTimeline[0].Position > 0)
            events = events.Prepend(new MidiEvent(0, Tempo(1)));

        events = events.Prepend(new MidiEvent(0, TimeSignature(4, 4)));

        WriteTrack(events, durationDelta, stream);
    }

    private static void WriteNoteTrack(
        RenderedTrack track,
        byte channel,
        uint durationDelta,
        Stream stream
    )
    {
        List<MidiEvent> settings = [new MidiEvent(0, ProgramChange(channel, (byte)track.PitchInstrumentCode))];

        // a track at the volume it would play at anyway has nothing to say about its volume
        if (track.Volume < 1)
        {
            var volume = (byte)Math.Round(track.Volume * DefaultChannelVolume);
            settings.Add(new MidiEvent(0, ControlChange(channel, MainVolumeController, volume)));
        }

        var events = settings.Concat(track.NoteTimeline.ToMidiNotes(durationDelta).SelectMany(x => x.ToEvents(channel)));
        WriteTrack(events, durationDelta, stream);
    }

    /// <summary>
    ///     The channel every track is written to, in the order of <paramref name="tracks" />: the percussion
    ///     track takes the channel General MIDI keeps for it, and the pitched ones the channels around it.
    /// </summary>
    private static ImmutableArray<byte> ToChannels(this ImmutableArray<RenderedTrack> tracks)
    {
        if (tracks.Count(x => x.IsPercussionInstrument) > 1)
            throw new ArgumentException("Only one percussion track is allowed.");

        var pitchTrackIndex = 0;
        return
        [
            ..tracks.Select(track => track.IsPercussionInstrument
                ? PercussionChannel
                : PitchTrackChannel(pitchTrackIndex++))
        ];
    }

    private static IEnumerable<(byte channel, RenderedTrack track)> ToIndexedTracks(this ImmutableArray<RenderedTrack> tracks)
    {
        return tracks.ToChannels()
            .Zip(tracks)
            .OrderBy(x => x.First);
    }

    /// <summary>
    ///     The instrument every channel of the song plays, by channel. The instrument of the percussion
    ///     channel is its drum kit, which is what a program change means on that channel.
    /// </summary>
    public static ImmutableSortedDictionary<byte, int> GetChannelInstruments(this RenderedSong song)
    {
        return song.Tracks
            .ToIndexedTracks()
            .ToImmutableSortedDictionary(x => x.channel, x => x.track.PitchInstrumentCode);
    }

    /// <summary>
    ///     The song with the instrument of every channel in <paramref name="instruments" /> replaced, which
    ///     is the whole of what an instrument is to a written song: not a note of it changes.
    /// </summary>
    /// <exception cref="ArgumentException">A channel the song does not play.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An instrument outside the 0-127 of General MIDI.</exception>
    public static RenderedSong WithChannelInstruments(
        this RenderedSong song,
        IReadOnlyDictionary<byte, int> instruments
    )
    {
        if (instruments.Count == 0) return song;

        var channels = song.Tracks.ThrowIfNotPlayed(instruments.Keys, nameof(instruments));
        foreach (var instrument in instruments.Values)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(instrument, nameof(instruments));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(instrument, 127, nameof(instruments));
        }

        return song.WithTracks(song.Tracks.Select((track, index) =>
            instruments.TryGetValue(channels[index], out var instrument)
                ? new RenderedTrack(track.IsPercussionInstrument, instrument, track.NoteTimeline, track.Volume)
                : track));
    }

    /// <summary>
    ///     The song with the volume of every channel in <paramref name="volumes" /> replaced: a part of the
    ///     volume it plays at unasked, where 1 is that volume and 0 is silence. The notes keep their own
    ///     dynamics, since this is the volume the whole track plays under.
    /// </summary>
    /// <exception cref="ArgumentException">A channel the song does not play.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A volume outside 0 to 1.</exception>
    public static RenderedSong WithChannelVolumes(this RenderedSong song, IReadOnlyDictionary<byte, double> volumes)
    {
        if (volumes.Count == 0) return song;

        var channels = song.Tracks.ThrowIfNotPlayed(volumes.Keys, nameof(volumes));

        return song.WithTracks(song.Tracks.Select((track, index) =>
            volumes.TryGetValue(channels[index], out var volume)
                ? new RenderedTrack(track.IsPercussionInstrument, track.PitchInstrumentCode, track.NoteTimeline, volume)
                : track));
    }

    /// <summary>
    ///     The song without the given channels, which are not written at all: no notes, and nothing to say
    ///     they were ever there. The pitched channels left behind close up, since they are handed out in
    ///     the order the tracks are written.
    /// </summary>
    /// <exception cref="ArgumentException">A channel the song does not play.</exception>
    public static RenderedSong WithoutChannels(this RenderedSong song, IReadOnlyCollection<byte> channels)
    {
        if (channels.Count == 0) return song;

        var trackChannels = song.Tracks.ThrowIfNotPlayed(channels, nameof(channels));

        return song.WithTracks(song.Tracks.Where((_, index) => !channels.Contains(trackChannels[index])));
    }

    /// <summary>The channels of the tracks, once every channel of <paramref name="asked" /> is known.</summary>
    private static ImmutableArray<byte> ThrowIfNotPlayed(
        this ImmutableArray<RenderedTrack> tracks,
        IEnumerable<byte> asked,
        string parameterName
    )
    {
        var channels = tracks.ToChannels();
        foreach (var channel in asked.Where(channel => !channels.Contains(channel)))
            throw new ArgumentException($"The song does not play channel {channel}.", parameterName);

        return channels;
    }

    private static RenderedSong WithTracks(this RenderedSong song, IEnumerable<RenderedTrack> tracks)
    {
        return new RenderedSong(song.Duration, song.TempoTimeline, [..tracks]);
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
        foreach (var (channel, track) in indexedTracks) WriteNoteTrack(track, channel, songDurationDelta, stream);
    }

    private readonly record struct MidiNote(uint OnDelta, uint OffDelta, byte Key, double Velocity);

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
    }
}
