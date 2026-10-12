using Rmg.Core.Songs;
using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Rendering;

namespace Rmg.Core;

public static class Midi
{
    // fine enough for a pocket's and a double take's offsets, a few milliseconds each
    private const uint TicksPerQuarterNote = 480;
    private const byte PercussionChannel = 9;
    private const byte MainVolumeController = 7;
    private const byte ExpressionController = 11;
    private const byte PanController = 10;
    private const double MaxControllerValue = 127;

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

    private static byte[] PitchBend(byte channel, int value)
    {
        return [ChannelMidiEventHeader(channel, 0x0E), (byte)(value & 0x7F), (byte)(value >> 7 & 0x7F)];
    }

    /// <summary>The fade times the part's expression, wherever either changes; the fade alone where the part has none.</summary>
    private static IEnumerable<(double Position, double Value)> Expression(StateTimeline<double> fade, ImmutableArray<(double Position, double Value)> expression)
    {
        if (expression.IsEmpty)
            return fade.Select(x => (x.Position, x.Value));

        var fadeAt = (double position) => fade.Count == 0 || fade[0].Position > position + 1e-9 ? 1 : fade.GetEffectiveValueAt(position);
        // the expression at a position: its latest step, in its order, of those at or before it, swept in the positions'
        // order rather than looked for at each
        var byPosition = Enumerable.Range(0, expression.Length).OrderBy(i => expression[i].Position).ToArray();
        var next = 0;
        var latest = -1;
        var merged = new List<(double Position, double Value)>();
        foreach (var position in fade.Select(x => x.Position).Concat(expression.Select(x => x.Position)).Distinct().Order())
        {
            for (; next < byPosition.Length && expression[byPosition[next]].Position <= position + 1e-9; next++)
                latest = Math.Max(latest, byPosition[next]);
            merged.Add((position, fadeAt(position) * (latest < 0 ? 1 : expression[latest].Value)));
        }

        return merged;
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

    /// <param name="clocks">How many MIDI clocks a metronome click is, 24 a quarter note.</param>
    private static byte[] TimeSignature(byte numerator, byte denominator, byte clocks)
    {
        return
        [
            0xff,
            0x58,
            0x04,
            numerator,
            (byte)Math.Log2(denominator),
            clocks,
            0x08
        ];
    }

    private static byte[] Tempo(double tempo)
    {
        // microseconds a beat
        var tempoValue = (uint)Math.Round(Meter.SecondsPerBeat(tempo) * 1_000_000);
        return
        [
            0xff,
            0x51,
            0x03,
            ..IntToBytesFixed(tempoValue, 3)
        ];
    }

    /// <summary>
    ///     A Roland GS message setting the data at an address, such as the reverb's macro, its room, as a system exclusive
    ///     event: to the GS model, the address and the data, with their checksum.
    /// </summary>
    private static byte[] GsMessage(byte[] address, byte[] data)
    {
        byte[] body = [..address, ..data];
        var checksum = (byte)((128 - body.Sum(x => x) % 128) % 128);
        byte[] message = [0x41, 0x10, 0x42, 0x12, ..body, checksum, 0xf7];
        return [0xf0, ..IntToBytesVariable((uint)message.Length), ..message];
    }

    /// <summary>How much an echo's repeat keeps of the one before, as the Roland GS delay's feedback: 64 none, 127 the most, 100 about a half.</summary>
    private const byte DelayFeedback = 100;

    /// <summary>How dark an echo's repeats are, as the Roland GS delay's low-pass before it, from 0, as they were, to 7.</summary>
    private const byte DelayDarkness = 2;

    /// <summary>
    ///     A Roland GS delay's time, as its data steps it, from 0.1 ms to a second, in segments of a finer step the shorter;
    ///     a time past a second halved until it fits, as an echo a half's beat later sounds the same pulse.
    /// </summary>
    internal static byte DelayTime(double milliseconds)
    {
        while (milliseconds > 1000)
            milliseconds /= 2;
        (int Start, int End, double From, double Step)[] segments =
            [(1, 20, 0.1, 0.1), (20, 35, 2, 0.2), (35, 45, 5, 0.5), (45, 55, 10, 1), (55, 70, 20, 2), (70, 80, 50, 5), (80, 90, 100, 10), (90, 105, 200, 20), (105, 116, 500, 50)];
        var (start, _, from, step) = segments.Last(x => x.From <= milliseconds + 1e-9);
        // a time rounding up past its segment the next segment's first, which the segments run on into
        return (byte)Math.Min(segments[^1].End - 1, start + (int)Math.Round((milliseconds - from) / step));
    }

    /// <summary>A channel's part, as a Roland GS address numbers it: the drums' first, the channels before them after it.</summary>
    private static int GsPart(byte channel) => channel switch
    {
        PercussionChannel => 0,
        < PercussionChannel => channel + 1,
        _ => channel
    };

    private static byte[] Text(string text, byte type = 0x01)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        return
        [
            0xff,
            type,
            ..IntToBytesVariable((uint)bytes.Length),
            ..bytes
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
                AbsoluteDelta(Math.Max(0, x.Position + x.Value.Shift)),
                Math.Min(AbsoluteDelta(Math.Max(0, x.Position + x.Value.Shift) + x.Value.Duration), durationDelta),
                (byte)x.Value.Offset,
                x.Value.Velocity
            ))
            // each key's notes in the order they sound, which a shift may have moved past another's
            .GroupBy(x => x.Key)
            .SelectMany(x => TrimOverlaps(x.OrderBy(n => n.OnDelta)))
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

    /// <summary>A marker's meta event type, a place in the song named, which a DAW shows over its time line.</summary>
    private const byte Marker = 0x06;

    /// <param name="harmony">The chords where they change, each marked "A Dorian: Dm7", its key and mode and its chord.</param>
    private static void WriteSystemTrack(StateTimeline<double> tempoTimeline, ImmutableArray<(double Position, Meter Meter)> meters, int? room,
        (InsertionKind Kind, ImmutableArray<byte> Channels)? insertion, double? echo, ImmutableArray<SongHarmony.Change> harmony, string? label, uint durationDelta, Stream stream)
    {
        // the song's tempos, the base tempo at its start where it has none of its own there
        (double Position, double Value)[] tempos = tempoTimeline.Count == 0 || tempoTimeline[0].Position > 0
            ? [..tempoTimeline.Select(x => (x.Position, x.Value)).Prepend((0, 1))]
            : [..tempoTimeline.Select(x => (x.Position, x.Value))];
        var events = tempos.Select(x => new MidiEvent(AbsoluteDelta(x.Position), Tempo(x.Value)));

        // every meter where it starts, a click on every pulse of it, six clocks a 16th
        events = meters
            .Select(x => new MidiEvent(AbsoluteDelta(x.Position), TimeSignature((byte)x.Meter.TimeSignature.Numerator, (byte)x.Meter.TimeSignature.Denominator, (byte)(x.Meter.PulseSixteenths * 6))))
            .Concat(events);
        // the reverb's room, where the song has one of its own, and its insertion effect, its type and then every channel
        // that plays through it, as a Roland GS player takes them
        if (insertion is { } effect)
        {
            var (msb, lsb) = Insertions.TypeOf(effect.Kind);
            events = effect.Channels.Select(x => new MidiEvent(0, GsMessage([0x40, (byte)(0x40 | GsPart(x)), 0x22], [1]))).Concat(events)
                .Prepend(new MidiEvent(0, GsMessage([0x40, 0x03, 0x00], [msb, lsb])));
        }

        if (room is { } macro)
            events = events.Prepend(new MidiEvent(0, GsMessage([0x40, 0x01, 0x30], [(byte)macro])));
        // the echo, the delay's time as long as the echo waits at every tempo the song takes, and how it plays: its repeats
        // each quieter by its feedback, a little darker, from the middle alone
        if (echo is { } beats)
        {
            events = tempos.Select(x => new MidiEvent(AbsoluteDelta(x.Position), GsMessage([0x40, 0x01, 0x52], [DelayTime(beats * 1000 * Meter.SecondsPerBeat(x.Value))])))
                .Concat(events)
                .Prepend(new MidiEvent(0, GsMessage([0x40, 0x01, 0x51], [DelayDarkness])))
                .Prepend(new MidiEvent(0, GsMessage([0x40, 0x01, 0x55], [127, 0, 0, 127, DelayFeedback])));
        }
        if (label is not null)
            events = events.Prepend(new MidiEvent(0, Text(label)));
        events = events.Concat(harmony.Select(x => new MidiEvent(AbsoluteDelta(x.Position), Text($"{x.Key}: {x.Chord}", Marker))));

        WriteTrack(events, durationDelta, stream);
    }

    private static void WriteNoteTrack(
        RenderedTrack track,
        byte channel,
        StateTimeline<double> fadeTimeline,
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

        // nor does one in the middle about its place
        if (track.Pan != 0)
        {
            var pan = (byte)Math.Round(64 + track.Pan * 63);
            settings.Add(new MidiEvent(0, ControlChange(channel, PanController, pan)));
        }

        // a bend's range, set once, by its registered parameter, before any bend
        if (!track.PitchBends.IsEmpty)
            settings.AddRange(
                [
                    new MidiEvent(0, ControlChange(channel, 101, 0)), new MidiEvent(0, ControlChange(channel, 100, 0)),
                    new MidiEvent(0, ControlChange(channel, 6, ExpressionRender.BendRange)), new MidiEvent(0, ControlChange(channel, 38, 0)),
                    new MidiEvent(0, ControlChange(channel, 101, 127)), new MidiEvent(0, ControlChange(channel, 100, 127))
                ]
            );

        // a fade plays as the channel's expression, under the volume the listener sets, and the part's own expression,
        // a swell or a tremolo, under the fade: the two multiplied wherever either changes
        var fade = Expression(fadeTimeline, track.Expression).Select(x => new MidiEvent(AbsoluteDelta(x.Position), ControlChange(channel, ExpressionController, (byte)Math.Round(x.Value * MaxControllerValue))));
        // a change of instrument before the note that plays it, which the stable order by time keeps before it, and the
        // controllers and bends likewise
        var programs = track.ProgramChanges.Select(x => new MidiEvent(AbsoluteDelta(x.Position), ProgramChange(channel, (byte)x.Program)));
        var controllers = track.Controllers.Select(x => new MidiEvent(AbsoluteDelta(x.Position), ControlChange(channel, (byte)x.Controller, (byte)x.Value)));
        var bends = track.PitchBends.Select(x => new MidiEvent(AbsoluteDelta(x.Position), PitchBend(channel, x.Value)));
        var events = settings.Concat(fade).Concat(programs).Concat(controllers).Concat(bends).Concat(track.NoteTimeline.ToMidiNotes(durationDelta).SelectMany(x => x.ToEvents(channel)));
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
    ///     The channel every part of the song is written to, by its role: the pitched parts in the order they are
    ///     written, the drums on the channel General MIDI keeps for them.
    /// </summary>
    public static ImmutableSortedDictionary<TrackRole, byte> GetPartChannels(this RenderedSong song)
    {
        return song.Tracks.ToChannels().Zip(song.Tracks).Where(x => !x.Second.IsDouble && !x.Second.IsRiser).ToImmutableSortedDictionary(x => x.Second.Role, x => x.First);
    }

    /// <summary>
    ///     The level every part's own track plays at under the mix's faders, by its role: a doubled part's two takes each
    ///     at <see cref="Composition.DoubleTracking.Level" />, the others whole.
    /// </summary>
    public static ImmutableSortedDictionary<TrackRole, double> GetPartLevels(this RenderedSong song)
    {
        return song.Tracks.Where(x => !x.IsDouble && !x.IsRiser).ToImmutableSortedDictionary(x => x.Role, x => x.Volume);
    }

    /// <summary>The channel a doubled part's second take is written to, by its role (<see cref="Composition.DoubleTracking" />).</summary>
    public static ImmutableSortedDictionary<TrackRole, byte> GetDoubleChannels(this RenderedSong song)
    {
        return song.Tracks.ToChannels().Zip(song.Tracks).Where(x => x.Second.IsDouble).ToImmutableSortedDictionary(x => x.Second.Role, x => x.First);
    }

    /// <summary>The channel a part's riser into a drop is written to, by its role (<see cref="Composition.Risers" />).</summary>
    public static ImmutableSortedDictionary<TrackRole, byte> GetRiserChannels(this RenderedSong song)
    {
        return song.Tracks.ToChannels().Zip(song.Tracks).Where(x => x.Second.IsRiser).ToImmutableSortedDictionary(x => x.Second.Role, x => x.First);
    }

    /// <param name="label">
    ///     What the file says it is, as a text event at its start, such as RMG's version and the song's seed; null
    ///     for none, as the songs' fingerprint writes them, so that the label never changes what it measures.
    /// </param>
    public static void Write(this RenderedSong song, Stream stream, string? label)
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

        // the insertion effect, and every channel its part plays on through it, its second take's too, but its riser's, a
        // sweep of its own
        var insertion = song.Insertion is { } effect
            ? (effect.Kind, song.Tracks.ToChannels().Zip(song.Tracks).Where(x => x.Second.Role == effect.Part && !x.Second.IsRiser).Select(x => x.First).ToImmutableArray())
            : ((InsertionKind, ImmutableArray<byte>)?)null;
        WriteSystemTrack(song.TempoTimeline, song.Meters, song.Room, insertion, song.Echo, song.Harmony, label, songDurationDelta, stream);

        var indexedTracks = song.Tracks.ToIndexedTracks();
        foreach (var (channel, track) in indexedTracks) WriteNoteTrack(track, channel, song.FadeTimeline, songDurationDelta, stream);
    }

    private readonly record struct MidiNote(uint OnDelta, uint OffDelta, byte Key, double Velocity);

    private readonly record struct MidiEvent(uint Delta, byte[] Data)
    {
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
