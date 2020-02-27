import { Instrument } from './instrument';
import { Note } from './note';

export interface Track {
  instrument: Instrument;
  noteBase: Note;
  minOctave: number;
  maxOctave: number;
}
