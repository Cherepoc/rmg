using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Commons.Music.Midi;
using RMG.Core.Music;

namespace RMG.Core.Midi
{
    public sealed class SongMidiWriter
    {
        private const short DeltaTimeSpec = 96;

        public void WriteMidi(Song song, string path)
        {
            var midiMusic = new MidiMusic
            {
                Format = 1,
                DeltaTimeSpec = DeltaTimeSpec
            };

            midiMusic.AddTrack(
                new MidiTrack(
                    new List<MidiMessage>
                    {
                        new MidiMessage(0, CreateTimeSignatureEvent(4, 4)),
                        new MidiMessage(0, CreateSetTempoEvent(song.Tempo)),
                        new MidiMessage(ConvertPosition(song.Part.Duration), CreateEndOfTrackEvent())
                    }));

            var channel = 0;
            foreach (var track in song.Tracks)
            {
                var midiEvents = new List<TimedEvent<MidiEvent>>();

                AddPartEvents(midiEvents, song.Part, song, track, channel, 0, song.Part.Duration);

                midiEvents.Add(
                    new TimedEvent<MidiEvent>
                    {
                        Event = CreateEndOfTrackEvent(),
                        Position = song.Part.Duration
                    });

                var midiTrack = new MidiTrack();

                midiTrack.Messages.Add(new MidiMessage(0, CreateProgramChangeEvent(channel, track.Instrument.Code)));

                var previousEventMidiPosition = 0;
                var endOfTrackPosition = ConvertPosition(song.Part.Duration);
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

                midiMusic.AddTrack(midiTrack);
                channel++;
            }

            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                var midiWriter = new SmfWriter(stream);
                midiWriter.WriteMusic(midiMusic);
            }
        }

        private static MidiEvent CreateSetTempoEvent(double tempo)
        {
            var tempoValue = (int) Math.Round(60 * 1_000_000 / tempo);
            var tempoValueBytes = BitConverter.GetBytes(tempoValue);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(tempoValueBytes);
            }

            return new MidiEvent(0xff, 0x51, 0x03, tempoValueBytes, 0, 3);
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

        private void AddPartEvents(
            List<TimedEvent<MidiEvent>> midiEvents,
            Part part,
            Song song,
            Track track,
            int channel,
            double position,
            double duration
        )
        {
            if (part.TrackPatterns != null
                && part.TrackPatterns.Count > 0
                && part.TrackPatterns.TryGetValue(track, out var trackPatternTimeline))
            {
                var itemTimeline = trackPatternTimeline
                    .OrderBy(x => x.Position)
                    .ToArray();
                for (var itemIndex = 0; itemIndex < itemTimeline.Length; itemIndex++)
                {
                    var timedItem = itemTimeline[itemIndex];
                    if (timedItem.Position >= duration)
                    {
                        return;
                    }

                    var nextItemPosition = itemIndex < itemTimeline.Length - 1
                        ? itemTimeline[itemIndex + 1].Position
                        : double.MaxValue;
                    var maxDuration = position - timedItem.Position + Math.Min(duration, nextItemPosition);
                    var itemDuration = Math.Min(maxDuration, timedItem.Event.Duration);
                    if (itemDuration <= 0)
                    {
                        continue;
                    }

                    AddPatternEvents(
                        midiEvents,
                        timedItem.Event,
                        song,
                        channel,
                        position + timedItem.Position,
                        itemDuration);
                }
            }
            else if (part.Parts != null && part.Parts.Count > 0)
            {
                var itemTimeline = part.Parts
                    .OrderBy(x => x.Position)
                    .ToArray();
                for (var itemIndex = 0; itemIndex < itemTimeline.Length; itemIndex++)
                {
                    var timedItem = itemTimeline[itemIndex];
                    if (timedItem.Position >= duration)
                    {
                        return;
                    }

                    var nextItemPosition = itemIndex < itemTimeline.Length - 1
                        ? itemTimeline[itemIndex + 1].Position
                        : double.MaxValue;

                    var maxDuration = position - timedItem.Position + Math.Min(duration, nextItemPosition);
                    var itemDuration = Math.Min(maxDuration, timedItem.Event.Duration);
                    if (itemDuration <= 0)
                    {
                        continue;
                    }

                    AddPartEvents(
                        midiEvents,
                        timedItem.Event,
                        song,
                        track,
                        channel,
                        position + timedItem.Position,
                        itemDuration);
                }
            }
        }

        private void AddPatternEvents(
            List<TimedEvent<MidiEvent>> midiEvents,
            Pattern pattern,
            Song song,
            int channel,
            double position,
            double duration
        )
        {
            foreach (var timedEvent in pattern.Notes.OrderBy(x => x.Position))
            {
                if (timedEvent.Position >= duration)
                {
                    return;
                }

                var note = timedEvent.Event;
                var scaleOffset = note.ScaleOffset;
                var scaleNote = song.Scale.RankedOffsets[scaleOffset.Rank][scaleOffset.Offset];
                var midiNote = (byte) ((note.Octave + 5) * 12 + scaleNote);
                var midiVolume = (byte) Math.Round(note.Volume * 127);

                var notePosition = position + timedEvent.Position;

                midiEvents.Add(
                    new TimedEvent<MidiEvent>
                    {
                        Event = CreateChannelMidiEvent(MidiEvent.NoteOn, channel, midiNote, midiVolume),
                        Position = notePosition
                    });
                midiEvents.Add(
                    new TimedEvent<MidiEvent>
                    {
                        Event = CreateChannelMidiEvent(MidiEvent.NoteOff, channel, midiNote, 0x40),
                        Position = notePosition + note.Duration
                    });
            }
        }

        private static int ConvertPosition(double position)
        {
            return (int) Math.Round(position * DeltaTimeSpec);
        }
    }
}
