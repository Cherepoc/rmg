import { Scale } from './scale';
import { Note } from './note';
import { Track } from './track';
import { Section } from './section';
import { DurationEntity } from './duration-entity';
import { AnyPattern } from './pattern';
import { AnyNoteBasePattern } from './note-base-pattern';

export interface Song extends DurationEntity {
  tracks: Track[];
  scale: AnyPattern<Scale>;
  noteBase: AnyPattern<Note>;
  tempo: AnyPattern<number>;
  section: AnyNoteBasePattern<Section>;
}
