import * as fs from 'fs';
import { MidiSong } from './midi/midi-song';
import { MidiTrack } from './midi/midi-track';
import {
  EndOfTrackMidiEvent, NoteOffMidiEvent,
  NoteOnMidiEvent,
  ProgramChangeMidiEvent,
  TempoMidiEvent,
  TimeSignatureMidiEvent,
} from './midi/events';

export async function generate (): Promise<void> {
  const serviceTrack = new MidiTrack();
  serviceTrack.events = [
    {
      position: 0,
      value: new TimeSignatureMidiEvent(4, 4),
    },
    {
      position: 0,
      value: new TempoMidiEvent(120),
    },
    {
      position: 0,
      value: new EndOfTrackMidiEvent(),
    },
  ];

  const track1 = new MidiTrack();
  track1.events = [
    {
      position: 0,
      value: new ProgramChangeMidiEvent(0, 0),
    },
    {
      position: 0,
      value: new NoteOnMidiEvent(0, 0x40, 1),
    },
    {
      position: 4,
      value: new NoteOffMidiEvent(0, 0x40),
    }
  ];

  const song = new MidiSong();
  song.tracks = [
    serviceTrack,
    track1,
  ];

  const buffer = Buffer.from(song.write());
  await fs.promises.writeFile('C:\\Projects\\RMG\\songs\\' + 'song.mid', buffer);

  console.log('aaaa');
}

(async () => {
  try {
    await generate();
  } catch (e) {
    console.error(e.toString());
  }
})();
