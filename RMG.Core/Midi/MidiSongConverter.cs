using System;
using System.Collections.Generic;
using System.Linq;
using Commons.Music.Midi;
using RMG.Core.Music;
using RMG.Core.Render;

namespace RMG.Core.Midi
{
    public static class MidiSongConverter
    {
        private const short DeltaTimeSpec = 96;

        public static MidiMusic ConvertSong(Song song)
        {
            var renderedSong = SongRenderer.RenderSong(song);

            var midiMusic = new MidiMusic
            {
                Format = 1,
                DeltaTimeSpec = DeltaTimeSpec
            };

            midiMusic.AddTrack(CreateMiscTrack(renderedSong));

            var channel = 0;
            foreach (var renderedTrack in renderedSong.Tracks)
            {
                var midiTrack = CreateChannelTrack(renderedSong, renderedTrack, channel);

                midiMusic.AddTrack(midiTrack);
                channel++;
            }

            return midiMusic;
        }

        private static MidiTrack CreateMiscTrack(RenderedSong renderedSong)
        {
            return new MidiTrack(
                new List<MidiMessage>
                {
                    new MidiMessage(0, CreateTimeSignatureEvent(4, 4)),
                    new MidiMessage(0, CreateSetTempoEvent(renderedSong.Tempo)),
                    new MidiMessage(ConvertPosition(renderedSong.Duration), CreateEndOfTrackEvent())
                });
        }

        private static MidiTrack CreateChannelTrack(RenderedSong renderedSong, RenderedTrack renderedTrack, int channel)
        {
            var midiEvents = new List<TimedEvent<MidiEvent>>();

            var midiTrack = new MidiTrack();

            midiTrack.Messages.Add(new MidiMessage(0, CreateProgramChangeEvent(channel, renderedTrack.InstrumentCode)));

            foreach (var timedEvent in renderedTrack.NoteTimeline)
            {
                var note = timedEvent.Event;
                var midiNote = GetMidiNoteOffset(note.Offset);
                var midiVolume = (byte) Math.Round(note.Volume * 127);

                midiEvents.Add(
                    new TimedEvent<MidiEvent>
                    {
                        Event = CreateChannelMidiEvent(MidiEvent.NoteOn, channel, midiNote, midiVolume),
                        Position = timedEvent.Position
                    });
                midiEvents.Add(
                    new TimedEvent<MidiEvent>
                    {
                        Event = CreateChannelMidiEvent(MidiEvent.NoteOff, channel, midiNote, 0x40),
                        Position = timedEvent.Position + note.Duration
                    });
            }

            var previousEventMidiPosition = 0;
            var endOfTrackPosition = ConvertPosition(renderedSong.Duration);
            foreach (var timedEvent in midiEvents.OrderBy(x => x.Position))
            {
                var midiPosition = ConvertPosition(timedEvent.Position);
                if (midiPosition > endOfTrackPosition)
                {
                    midiPosition = endOfTrackPosition;
                }

                midiTrack.Messages.Add(new MidiMessage(midiPosition - previousEventMidiPosition, timedEvent.Event));
                previousEventMidiPosition = midiPosition;
            }

            midiTrack.Messages.Add(
                new MidiMessage(endOfTrackPosition - previousEventMidiPosition, CreateEndOfTrackEvent()));

            return midiTrack;
        }

        private static byte GetMidiNoteOffset(int noteOffset)
        {
            var offset = 5 * 12 + noteOffset;
            while (offset < 0 || offset > 127)
            {
                if (offset < 0)
                {
                    offset += 12 * (-offset / 12 + 1);
                }

                if (offset > 127)
                {
                    offset -= 12 * ((offset - 127) / 12 + 1);
                }
            }

            return (byte) offset;
        }

        private static MidiEvent CreateSetTempoEvent(double tempo)
        {
            var tempoValue = (int) Math.Round(60 * 1_000_000 / tempo);
            var tempoValueBytes = BitConverter.GetBytes(tempoValue);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(tempoValueBytes);
            }

            return new MidiEvent(0xff, 0x51, 0x03, tempoValueBytes, 1, 3);
        }

        private static MidiEvent CreateTimeSignatureEvent(int numerator, int denominator)
        {
            var denominatorPower = (byte) Math.Log(denominator, 2);
            var data = new byte[]
            {
                (byte) numerator,
                denominatorPower,
                24,
                8
            };

            return new MidiEvent(0xff, 0x58, 0x04, data, 0, 4);
        }

        private static MidiEvent CreateEndOfTrackEvent()
        {
            return new MidiEvent(0xff, 0x2f, 0x00, Array.Empty<byte>(), 0, 0);
        }

        private static MidiEvent CreateProgramChangeEvent(int channel, int instrumentCode)
        {
            return CreateChannelMidiEvent(MidiEvent.Program, channel, (byte) instrumentCode, 0);
        }

        private static MidiEvent CreateChannelMidiEvent(int type, int channel, byte arg1, byte arg2)
        {
            return new MidiEvent(
                (byte) (type + channel),
                arg1,
                arg2,
                Array.Empty<byte>(),
                0,
                0);
        }

        private static int ConvertPosition(double position)
        {
            return (int) Math.Round(position * DeltaTimeSpec);
        }
    }
}
